# Plan: feature/exit-code-decision

Viertes und letztes Pflicht-Feature aus `docs/plans/teil-a-overview.md`: die eigentliche Grün/Rot-Entscheidung, `--min-tests`/`--max-depth`-CLI-Parsing, Live-Fortschritt in der Konsole, lesbare Übersicht und der tatsächliche Exit-Code. Verdrahtet alle drei vorherigen Features (`TrxInputResolver` + `TrxBatchReader` + `TestRunAggregator`) im `TestGuardian.Console`-Projekt zu einem lauffähigen Programm.

**Basis-Branch:** `main` (nach Merge von `feature/multi-file-input`).

## Geklärte Entscheidungen (2026-09-18)

1. **`CorrectlyExecuted` (nicht `Attempted`)** ist die Grundlage für die "0 Tests ausgeführt"- und `--min-tests`-Regel. Begründung: passt zum Misstrauens-Prinzip — ein Haufen uneindeutiger `Other`-Ergebnisse soll `--min-tests` nicht künstlich erfüllen können.
2. **`UnresolvedInputs` lösen für sich allein einen Fehlschlag aus**, symmetrisch zur `LoadError`-Regel — ein Teil dessen, was geprüft werden sollte, wurde gar nicht erst gefunden.

## Architektur: drei neue, unabhängig testbare Bausteine

```
TestGuardian.Core/Trx/TestRunVerdict.cs        (Kernentscheidung, testbar ohne Konsole)
TestGuardian.Console/CliArgumentParser.cs      (Argument-Parsing, testbar ohne Konsole)
TestGuardian.Console/ConsoleReportPrinter.cs   (Ausgabeformatierung)
TestGuardian.Console/Program.cs                (nur noch Verdrahtung der obigen Bausteine)
```

**Warum die Entscheidungslogik in `TestGuardian.Core` bleibt, nicht in der Console:** Es ist die fachlich heikelste Stelle der ganzen Aufgabe — sie verdient denselben Testrahmen wie der Rest der Kernlogik (MSTest, keine Konsolen-Abhängigkeit). `CliArgumentParser` und `ConsoleReportPrinter` sind reine Verdrahtung/Formatierung und gehören ins Console-Projekt.

## Baustein 1: `TestRunVerdict` (Kernentscheidung)

```csharp
namespace TestGuardian.Core.Trx;

public enum VerdictReasonKind
{
    RealTestFailures,
    LoadErrors,
    ZeroTestsExecuted,
    BelowMinimumTestCount,
    UnreadableFiles,
    UnresolvedInputs
}

public sealed record VerdictReason(VerdictReasonKind Kind, string Message);

public sealed record GuardianVerdict(bool IsSuccessful, IReadOnlyList<VerdictReason> Reasons);

public static class TestRunVerdict
{
    public static GuardianVerdict Evaluate(
        TestRunOverview overview,
        InputResolutionResult inputResolution,
        int? minTests)
    {
        var reasons = new List<VerdictReason>();

        if (overview.Total.Failed > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.RealTestFailures,
                $"{overview.Total.Failed} Test(s) fehlgeschlagen."));
        }

        if (overview.Total.LoadError > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.LoadErrors,
                $"{overview.Total.LoadError} Test(s) konnten nicht geladen werden (Bibliothek nicht ladbar)."));
        }

        if (overview.Total.CorrectlyExecuted == 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.ZeroTestsExecuted,
                "Es wurden 0 Tests mit eindeutigem Ergebnis ausgeführt."));
        }

        if (minTests is int min && overview.Total.CorrectlyExecuted < min)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.BelowMinimumTestCount,
                $"Nur {overview.Total.CorrectlyExecuted} von mindestens {min} erwarteten Tests liefen."));
        }

        if (overview.FileFailures.Count > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.UnreadableFiles,
                $"{overview.FileFailures.Count} Datei(en) konnten nicht gelesen werden."));
        }

        if (inputResolution.UnresolvedInputs.Count > 0)
        {
            reasons.Add(new VerdictReason(VerdictReasonKind.UnresolvedInputs,
                $"{inputResolution.UnresolvedInputs.Count} Eingabe(n) konnten nicht aufgelöst werden."));
        }

        return new GuardianVerdict(reasons.Count == 0, reasons);
    }
}
```

**Warum `VerdictReasonKind` als Enum statt nur Freitext:** Tests sollen gegen eine stabile Kategorie prüfen (`Kind == VerdictReasonKind.LoadErrors`), nicht gegen Texte, die sich später aus rein sprachlichen Gründen ändern könnten. `Message` bleibt für die tatsächliche Konsolenausgabe.

**Warum alle zutreffenden Gründe gesammelt werden statt beim ersten Treffer abzubrechen:** Wenn z.B. gleichzeitig echte Fehlschläge UND ein Ladefehler auftreten, soll der Nutzer beides sehen, nicht nur den zuerst geprüften Grund — wichtig für "Urteilsvermögen"/Nachvollziehbarkeit laut Aufgabenstellung.

**Kein separater Exit-Code je Fehlergrund:** Die Aufgabenstellung verlangt nur "Rückgabewert ≠ 0". Eine feinere Kodierung (z.B. Bitmaske je Fehlerart) wäre zusätzliche Komplexität ohne konkrete Anforderung dahinter — bewusst weggelassen.

## Baustein 2: `CliArgumentParser`

```csharp
namespace TestGuardian.Console;

public sealed record CliOptions(IReadOnlyList<string> Inputs, int MaxDepth, int? MinTests);

public static class CliArgumentParser
{
    private const int DefaultMaxDepthWhenSwitchGivenWithoutValue = 10;

    public static CliOptions Parse(string[] args)
    {
        var inputs = new List<string>();
        var maxDepth = 0; // Schalter komplett fehlt -> keine Rekursion (siehe teil-a-overview.md)
        int? minTests = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--max-depth" when i + 1 < args.Length && int.TryParse(args[i + 1], out var explicitDepth):
                    maxDepth = explicitDepth;
                    i++;
                    break;
                case "--max-depth":
                    maxDepth = DefaultMaxDepthWhenSwitchGivenWithoutValue;
                    break;
                case "--min-tests" when i + 1 < args.Length && int.TryParse(args[i + 1], out var explicitMin):
                    minTests = explicitMin;
                    i++;
                    break;
                case "--min-tests":
                    throw new ArgumentException("--min-tests erwartet eine ganze Zahl als Wert.");
                default:
                    inputs.Add(args[i]);
                    break;
            }
        }

        return new CliOptions(inputs, maxDepth, minTests);
    }
}
```

**Warum ein handgeschriebener Parser statt einer Bibliothek (z.B. `System.CommandLine`):** Die Optionsfläche ist klein (zwei Schalter + positionale Eingaben) und die dreistufige `--max-depth`-Semantik (fehlt/ohne Wert/mit Wert) ist ohnehin ein Sonderfall, den generische Bibliotheken nicht immer direkt unterstützen. Eine zusätzliche Installation dafür wäre nicht durch den Umfang gerechtfertigt — und du hast gebeten, nichts ohne dein Einverständnis zu installieren.

**Warum `--min-tests` ohne Wert eine Exception wirft statt eines stillen Defaults:** Anders als bei `--max-depth` gibt es für `--min-tests` keinen sinnvollen "Schalter ohne Wert bedeutet X"-Default — ein Nutzer, der `--min-tests` ohne Zahl angibt, hat sich vermutlich vertippt; das früh und laut zu melden passt besser zum Misstrauens-Prinzip als zu raten.

## Baustein 3: `ConsoleReportPrinter`

Reine Formatierung, keine Entscheidungslogik. Gibt aus:
1. Fortschritt während des Einlesens (aus `IProgress<ReadProgress>`, live überschreibend via `\r`).
2. Je Bibliothek: Name, Total/Passed/Failed/LoadError/Skipped.
3. Gesamt-Zeile (`overview.Total`).
4. Warnungen (`AggregatedWarning`, mit Run-Namen), Datei-Fehler (`FileFailures`), nicht aufgelöste Eingaben (`UnresolvedInputs`) — jeweils nur, wenn nicht leer.
5. Das Urteil (`GuardianVerdict`) mit allen Gründen.

**Warum `IProgress<ReadProgress>` in `Program.cs` NICHT als `System.Progress<T>` instanziiert wird:** Wie in `docs/decisions/multi-file-input.md` festgehalten, marshalt `System.Progress<T>` `Report`-Aufrufe über den `SynchronizationContext` bzw. den ThreadPool, wenn keiner gesetzt ist — in einer Konsolenanwendung ohne eigenen `SynchronizationContext` hieße das, dass die Fortschrittsausgabe **asynchron und außer der Reihe** erscheinen könnte, was der ausdrücklich gewünschten Echtzeit-Anzeige widerspräche. Es kommt dieselbe synchrone `IProgress<T>`-Implementierung zum Einsatz wie in den Tests — dafür wird `SynchronousProgress<T>` aus `TestGuardian.Core.Tests` nach `TestGuardian.Core` verschoben (kleine Aufräumarbeit an bestehendem Code aus `feature/multi-file-input`), damit Console **und** Tests dieselbe Klasse verwenden statt sie zu duplizieren.

## Baustein 4: `Program.cs` (reine Verdrahtung)

```csharp
var options = CliArgumentParser.Parse(args);

var inputResolution = TrxInputResolver.Resolve(options.Inputs, options.MaxDepth);

var progress = new SynchronousProgress<ReadProgress>(p =>
    Console.Write($"\rDatei {p.FilesRead}/{p.TotalFiles} gelesen ({p.TestsReadSoFar} Tests bisher)..."));
var readResults = TrxBatchReader.ReadAll(inputResolution.ResolvedFilePaths, progress);
Console.WriteLine();

var overview = TestRunAggregator.Aggregate(readResults);
var verdict = TestRunVerdict.Evaluate(overview, inputResolution, options.MinTests);

ConsoleReportPrinter.Print(overview, inputResolution, verdict);

return verdict.IsSuccessful ? 0 : 1;
```

**Warum keine gesonderte Behandlung für "gar keine Eingabe angegeben":** Fließt `options.Inputs` leer durch `TrxInputResolver.Resolve` (keine `rawInputs`), entstehen weder aufgelöste Pfade noch `UnresolvedInputs` (die Schleife läuft schlicht nicht), `TrxBatchReader.ReadAll([])` liefert eine leere Ergebnisliste, und `TestRunAggregator.Aggregate([])` liefert `Total.CorrectlyExecuted == 0` — die bereits bestehende "0 Tests ausgeführt"-Regel greift automatisch. Ein gesonderter Sonderfall dafür wäre redundant.

## Geplante Unit-Tests

### `TestRunVerdict` (Kernlogik, am wichtigsten)

| Test | Prüft |
|---|---|
| `Evaluate_AllPassedNoMinTests_IsSuccessful` | Sauberer Grünlauf (`lauf-d.trx`) → `IsSuccessful == true`, keine Gründe |
| `Evaluate_RealFailuresPresent_IsUnsuccessfulWithRealTestFailuresReason` | `lauf-a.trx` (3 echte Fehlschläge) → `Kind == RealTestFailures` |
| `Evaluate_LoadErrorAlonePresent_IsUnsuccessfulEvenWithoutRealFailures` | `lauf-c.trx` (1 Passed, 3 LoadError, 0 echte Failed) → trotzdem `IsSuccessful == false`, `Kind == LoadErrors` |
| `Evaluate_ZeroCorrectlyExecuted_IsUnsuccessfulWithZeroTestsReason` | `lauf-b.trx` (0 Tests) → `Kind == ZeroTestsExecuted` |
| `Evaluate_BelowMinTests_IsUnsuccessful` | `lauf-d.trx` (3 Tests) mit `minTests: 5` → `Kind == BelowMinimumTestCount` |
| `Evaluate_AtOrAboveMinTests_IsSuccessful` | `lauf-d.trx` (3 Tests) mit `minTests: 3` → erfüllt |
| `Evaluate_FileFailurePresent_IsUnsuccessful` | Ein `TrxReadResult.Failure` in der Eingabe → `Kind == UnreadableFiles` |
| `Evaluate_UnresolvedInputPresent_IsUnsuccessful` | Ein `UnresolvedInput` in `InputResolutionResult` → `Kind == UnresolvedInputs` |
| `Evaluate_MultipleReasonsCanCoexist` | Kombination aus echten Fehlschlägen und Ladefehlern → beide `Kind`s in `Reasons` vorhanden |

### `CliArgumentParser`

| Test | Prüft |
|---|---|
| `Parse_OnlyInputs_DefaultsMaxDepthToZeroAndMinTestsToNull` | Keine Schalter → `MaxDepth == 0`, `MinTests == null` |
| `Parse_MaxDepthWithoutValue_UsesDefaultTen` | `--max-depth` ohne folgende Zahl → `MaxDepth == 10` |
| `Parse_MaxDepthWithValue_UsesGivenValue` | `--max-depth 3` → `MaxDepth == 3` |
| `Parse_MinTestsWithValue_SetsMinTests` | `--min-tests 5` → `MinTests == 5` |
| `Parse_MinTestsWithoutValue_Throws` | `--min-tests` ohne folgende Zahl → wirft `ArgumentException` |
| `Parse_MixedInputsAndOptions_SeparatesCorrectly` | z.B. `["a.trx", "--max-depth", "2", "b.trx"]` → `Inputs == ["a.trx", "b.trx"]`, `MaxDepth == 2` |

### `ConsoleReportPrinter`

Kein exaktes Format-Pinning (zu brittle), stattdessen inhaltliche Stichproben mit umgeleitetem `Console.Out`:

| Test | Prüft |
|---|---|
| `Print_SuccessfulRun_OutputContainsAssemblyNameAndGreenVerdict` | Enthält Bibliotheksnamen und ein erkennbares "grün"/Erfolgs-Signal |
| `Print_FailedRun_OutputContainsAllReasonMessages` | Jede `VerdictReason.Message` taucht in der Ausgabe auf |

## Manuelle Verifikation statt automatisiertem End-to-End-Test

Das eigentliche `Program.cs` (CLI-Einstiegspunkt, Exit-Code) wird **nicht** von mir automatisiert oder manuell ausgeführt und geprüft — das ist genau die Art von Selbstverifikation, die du mir für dieses Projekt untersagt hast, und zufällig auch inhaltlich der "Lauf 2"-Fall aus Teil B (den lügenden grünen Balken selbst herstellen und fangen). Die naheliegende Prüfung — den Wächter gegen `Fixtures/Sample Data/lauf-a.trx` bis `lauf-e.trx` laufen lassen und die Exit-Codes/Ausgabe kontrollieren — liegt bei dir.

## Nicht Teil dieses Features

- `--help`-Ausgabe oder sonstige CLI-Komfortfunktionen — nicht gefordert, aus Zeitgründen bewusst weggelassen.
- JSON-Ausgabe, Baseline-Vergleich, Liste geduldeter Fehlschläge (Kür) — eigene, spätere Features, falls Zeit bleibt.
- Feinere Exit-Code-Kodierung je Fehlerart.
