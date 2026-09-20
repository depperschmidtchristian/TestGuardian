# Plan: feature/baseline-comparison

Ziel: Kür-Feature "Vergleich gegen eine gespeicherte Grundlinie" ("war dieser Test gestern schon rot?"). Neuer Schalter `--baseline <Pfad>`: lädt einen zuvor mit `--to-json` erzeugten Report und vergleicht die aktuell fehlgeschlagenen/Ladefehler-Tests dagegen — reine Zusatzoption auf dem bestehenden Aufruf, **keine** Subcommand-Umstellung (`test`/`compare`), nach Rücksprache wegen des Zeitbudgets bis zur Abgabe verworfen.

Branch zweigt von `feature/json-output` ab (noch nicht in `main`), nicht von `main` — Baseline-Vergleich braucht `JsonReport`.

## Datumsfeld in `JsonReport`

`JsonReport` bekommt ein viertes Feld, vom Aufrufer (`Program.cs`) gesetzt, nicht vom Writer selbst (hält `JsonReportWriter` frei von "aktuelle Uhrzeit"-Abhängigkeit, bleibt rein input-basiert und leicht testbar):

```csharp
public sealed record JsonReport(
    TestRunOverview Overview,
    InputResolutionResult InputResolution,
    GuardianVerdict Verdict,
    DateTimeOffset GeneratedAt,
    BaselineComparison? BaselineComparison = null);
```

`BaselineComparison` ist `null`, wenn kein `--baseline` angegeben wurde — taucht dann auch nicht im JSON auf (`JsonIgnoreCondition.WhenWritingNull` für dieses eine Feld, oder einfach `null` im JSON stehen lassen; siehe Tests-Abschnitt, Ermessensfrage unten).

## Warum das Laden der Baseline anders behandelt wird als das Schreiben bei `--to-json`

Bei `--to-json` galt: nie öffnen, wenn schon etwas da ist (Datenverlust-Risiko). Bei `--baseline` ist es umgekehrt: die Datei muss **existieren und lesbar sein**, Lesen ist ungefährlich. Deshalb kein zweistufiger Check-dann-schreib-Mechanismus nötig — ein einziger Ladevorgang genügt, und zwar als Ergebnistyp statt Exception, konsistent mit `TrxReadResult` (missing/malformed ist ein erwarteter, kein außergewöhnlicher Fall):

`TestGuardian.Core/Json/Models/BaselineLoadResult.cs`:
```csharp
namespace TestGuardian.Core.Json.Models;

public abstract record BaselineLoadResult
{
    private BaselineLoadResult() { }

    public sealed record Success(JsonReport Report) : BaselineLoadResult;

    public sealed record Failure(string Reason) : BaselineLoadResult;
}
```

`TestGuardian.Core/Json/BaselineReportLoader.cs`:
```csharp
namespace TestGuardian.Core.Json;

public static class BaselineReportLoader
{
    public static BaselineLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return new BaselineLoadResult.Failure($"Die Baseline-Datei '{path}' existiert nicht.");
        }

        try
        {
            var report = JsonSerializer.Deserialize<JsonReport>(File.ReadAllText(path), JsonReportWriter.Options);
            return report is null
                ? new BaselineLoadResult.Failure($"Die Baseline-Datei '{path}' enthält kein gültiges JSON-Objekt.")
                : new BaselineLoadResult.Success(report);
        }
        catch (JsonException ex)
        {
            return new BaselineLoadResult.Failure($"Die Baseline-Datei '{path}' ist kein gültiger TestGuardian-JSON-Bericht: {ex.Message}");
        }
    }
}
```
(`JsonReportWriter.Options` wird dafür von `private` auf `internal`/`public static readonly` angehoben, damit dieselben Serialisierungsregeln — camelCase, Enums als Strings — auch beim Rücklesen gelten.)

**In `Program.cs`**, im selben `try`/`catch (ArgumentException)`-Block wie der `--to-json`-Check:
```csharp
JsonReport? baseline = null;
if (options.BaselinePath is { } baselinePath)
{
    var loadResult = BaselineReportLoader.Load(baselinePath);
    baseline = loadResult switch
    {
        BaselineLoadResult.Success success => success.Report,
        BaselineLoadResult.Failure failure => throw new ArgumentException(failure.Reason),
        _ => throw new ArgumentOutOfRangeException()
    };
}
```
Ein kaputter/fehlender Baseline-Pfad ist damit ein Bedienfehler wie jeder andere CLI-Fehler — WARNUNG-Balken, exit 1, kein Testlauf startet.

## Vergleichslogik

`TestGuardian.Core/Baseline/` (neue Namensraum-Gruppe, analog zu `Trx/`/`Input/`/`Json/`):

`Baseline/Models/BaselineComparison.cs`:
```csharp
namespace TestGuardian.Core.Baseline.Models;

public sealed record TestOutcomeEntry(string AssemblyName, string TestName, TestOutcome Outcome);

public sealed record BaselineComparison(
    DateTimeOffset BaselineGeneratedAt,
    IReadOnlyList<TestOutcomeEntry> NewlyFailed,
    IReadOnlyList<TestOutcomeEntry> AlreadyFailingInBaseline,
    IReadOnlyList<TestOutcomeEntry> FixedSinceBaseline);
```

`Baseline/BaselineComparer.cs`:
```csharp
namespace TestGuardian.Core.Baseline;

public static class BaselineComparer
{
    public static BaselineComparison Compare(TestRunOverview current, JsonReport baseline)
    {
        var baselineOutcomes = baseline.Overview.Assemblies
            .SelectMany(a => a.TestCases)
            .ToDictionary(t => (t.AssemblyName, t.TestName), t => t.Outcome);

        var currentlyRed = current.Assemblies
            .SelectMany(a => a.TestCases)
            .Where(t => t.Outcome is TestOutcome.Failed or TestOutcome.LoadError);

        var newlyFailed = new List<TestOutcomeEntry>();
        var alreadyFailing = new List<TestOutcomeEntry>();

        foreach (var test in currentlyRed)
        {
            var wasRedBefore = baselineOutcomes.TryGetValue((test.AssemblyName, test.TestName), out var oldOutcome)
                && oldOutcome is TestOutcome.Failed or TestOutcome.LoadError;

            var entry = new TestOutcomeEntry(test.AssemblyName, test.TestName, test.Outcome);
            (wasRedBefore ? alreadyFailing : newlyFailed).Add(entry);
        }

        var fixedTests = baselineOutcomes
            .Where(kv => kv.Value is TestOutcome.Failed or TestOutcome.LoadError)
            .Where(kv => !currentlyRed.Any(t => (t.AssemblyName, t.TestName) == kv.Key))
            .Select(kv => new TestOutcomeEntry(kv.Key.AssemblyName, kv.Key.TestName, kv.Value))
            .ToList();

        return new BaselineComparison(baseline.GeneratedAt, newlyFailed, alreadyFailing, fixedTests);
    }
}
```

**Bewusste Vereinfachung (Ermessensfrage, dein OK nötig):** "war das schon rot" wird nur für *aktuell* rote Tests (`Failed`/`LoadError`) beantwortet. Neu hinzugekommene Tests, die in der Baseline gar nicht existierten, zählen automatisch als `NewlyFailed`, wenn sie jetzt rot sind (kein extra "neuer Test"-Fall). Umbenannte/entfernte Tests werden nicht gesondert erkannt (Abgleich ist `AssemblyName` + `TestName`). Das deckt die eigentliche Frage der Aufgabenstellung ab, ohne ein komplettes Test-Set-Diffing zu bauen.

## Verdikt/Exit-Code bleiben unverändert

Baseline-Vergleich ist reine Zusatzinformation — er verändert `TestRunVerdict.Evaluate` **nicht** und hat keinen Einfluss auf Rot/Gelb/Grün oder den Exit-Code. Ein Test, der "schon gestern rot war", ist immer noch rot und muss immer noch als Rot gemeldet werden — das Feature beantwortet nur "ist das neu", nicht "ist das schlimm".

## CLI-Verdrahtung

`CliOptions` bekommt `string? BaselinePath` (Pflicht-Wert wie `--min-tests`, kein Default-Mechanismus wie bei `--to-json` — für eine Baseline-Datei ergibt „automatisch einen Namen erraten" keinen Sinn):

```csharp
case "--baseline" when i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal):
    baselinePath = args[i + 1];
    i++;
    break;
case "--baseline":
    throw new ArgumentException("--baseline erwartet einen Pfad zu einer JSON-Datei als Wert.");
```

## Konsolenausgabe

Neuer Abschnitt in `ConsoleReportPrinter`, nur wenn eine Baseline geladen wurde, z.B.:
```
Baseline-Vergleich (Bericht vom 2026-09-20 14:32 UTC):
  Neu rot (2): widgetlib.testcpp.dll/Test_X, dienstlib.testcpp.dll/Test_Y
  Bereits vorher rot (1): widgetlib.testcpp.dll/Test_Z
  Seither behoben (1): widgetlib.testcpp.dll/Test_W
```
Leere Kategorien werden weggelassen (wie schon bei `PrintSectionIfAny` für Warnungen/nicht lesbare Dateien).

## JSON-Ausgabe

Wenn `--to-json` **und** `--baseline` zusammen angegeben werden, landet `BaselineComparison` im geschriebenen Report (das neue optionale Feld auf `JsonReport`). Nur `--baseline` ohne `--to-json` ist ebenfalls gültig (Vergleich nur in der Konsole).

## Tests

- `BaselineComparerTests` (Core.Tests): neu rot / bereits vorher rot / behoben, je ein Fall; Test ohne Gegenstück in der Baseline zählt als neu rot.
- `BaselineReportLoaderTests` (Core.Tests, Temp-Dateien): gültige, von `JsonReportWriter` erzeugte Datei lädt erfolgreich und roundtripped die Werte; fehlende Datei → `Failure`; kaputtes JSON → `Failure`.
- `CliArgumentParserTests`: `--baseline <Pfad>` setzt `BaselinePath`; ohne Wert wirft (Pflicht-Wert, anders als `--to-json`).
- `ConsoleReportPrinterTests`: neuer Fall für die Baseline-Sektion (enthalten, wenn übergeben; fehlt ganz, wenn nicht).

## Nicht Teil dieses Features

- Keine `test`/`compare`-Subcommands — verworfen wegen Zeitbudget, siehe oben.
- Kein Vergleich zweier bereits existierender JSON-Dateien ohne frischen Testlauf (bräuchte einen eigenen Modus ganz ohne `.trx`-Eingabe).
- Keine Änderung an Rot/Gelb/Grün oder Exit-Code durch den Vergleich selbst.
- Kein Tracking umbenannter Tests (Abgleich ist rein namensbasiert).
