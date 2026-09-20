# Plan: feature/known-failures-allowlist

Ziel: Kür-Feature "Liste bekannter, geduldeter Fehlschläge (z. B. Tests, die eine Installation brauchen)". Neuer Schalter `--known-failures <Pfad>`: Tests, die dort namentlich gelistet sind, zählen bei rot/Ladefehler nicht mehr automatisch als Grund für ein rotes Urteil — bleiben aber immer sichtbar, nie stillschweigend verschwunden.

## Abgleich nur über den Testnamen, bewusst nicht über Assembly+Testname

`BaselineComparer` (voriges Feature) gleicht über `AssemblyName` **und** `TestName` ab. Für die Allowlist gehe ich bewusst anders vor: nur der reine `TestName`.

**Warum:** `AssemblyName` in `TestCaseResult` ist das rohe `storage`-Attribut aus der `.trx` — bei euren echten `vstest.console.exe`-Läufen ein **voller, maschinenspezifischer Pfad** (`c:\users\deppe\desktop\testguardianrepo\...\x64\debug\guardianproof.tests.dll`), kein einfacher Dateiname. Eine von Hand gepflegte Allowlist-Datei mit exakten Pfaden wäre extrem unhandlich (bricht bei jedem Rechnerwechsel, jedem Build-Konfigurationswechsel Debug/Release) und für eine 15-Minuten-Live-Demo schlecht geeignet. Testname-only ist robust genug für den Zweck und macht die Datei trivial von Hand schreibbar. (Dasselbe Problem hat `BaselineComparer` im Grunde auch — dort aber bewusst nicht angefasst, siehe "Nicht Teil dieses Features".)

## Dateiformat

Reiner Text, eine Zeile pro Testname, `#`-Kommentare und Leerzeilen erlaubt — kein JSON, keine zusätzliche Parsing-Komplexität für eine simple Namensliste:

```
# Bekannte, geduldete Fehlschlaege - ein Testname pro Zeile.
# Diese Tests zaehlen bei Fehlschlag/Ladefehler nicht mehr als Grund fuer ein rotes Urteil.
Test_RequiresLicensedFeature
```

## Neue Bausteine (`TestGuardian.Core/Allowlist/`, analog zu `Baseline/`, `Json/`)

`Allowlist/KnownFailureAllowlist.cs` (Logik, kleine Utility-Klasse mit Verhalten, kein reines Datenmodell — bleibt deshalb außerhalb von `Models/`, wie `SynchronousProgress<T>`):
```csharp
namespace TestGuardian.Core.Allowlist;

public sealed class KnownFailureAllowlist
{
    public static readonly KnownFailureAllowlist Empty = new([]);

    private readonly HashSet<string> _testNames;

    private KnownFailureAllowlist(HashSet<string> testNames) => _testNames = testNames;

    public bool Contains(string testName) => _testNames.Contains(testName);

    public static KnownFailureAllowlist Parse(IEnumerable<string> lines) =>
        new(lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal));
}
```

`Allowlist/Models/KnownFailureAllowlistLoadResult.cs` (Ergebnistyp statt Exception, gleiches Prinzip wie `BaselineLoadResult`/`TrxReadResult` — fehlende Datei ist ein erwarteter Fall):
```csharp
namespace TestGuardian.Core.Allowlist.Models;

public abstract record KnownFailureAllowlistLoadResult
{
    private KnownFailureAllowlistLoadResult() { }

    public sealed record Success(KnownFailureAllowlist Allowlist) : KnownFailureAllowlistLoadResult;

    public sealed record Failure(string Reason) : KnownFailureAllowlistLoadResult;
}
```

`Allowlist/KnownFailureAllowlistLoader.cs`:
```csharp
namespace TestGuardian.Core.Allowlist;

public static class KnownFailureAllowlistLoader
{
    public static KnownFailureAllowlistLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return new KnownFailureAllowlistLoadResult.Failure($"Die Allowlist-Datei '{path}' existiert nicht.");
        }

        return new KnownFailureAllowlistLoadResult.Success(KnownFailureAllowlist.Parse(File.ReadAllLines(path)));
    }
}
```

## `TestOutcomeEntry` wandert von `Baseline.Models` nach `Trx.Models`

Wird jetzt von zwei Features gebraucht (Baseline-Vergleich **und** geduldete Fehlschläge) — gehört als allgemeiner "benannter Testausgang" fachlich zu `Trx.Models` neben `TestCaseResult`/`TestOutcome`, nicht mehr baseline-spezifisch unter `Baseline.Models`. Reine Verschiebung, keine inhaltliche Änderung; `BaselineComparison` referenziert danach `TestGuardian.Core.Trx.Models.TestOutcomeEntry`.

## `TestRunVerdict.Evaluate` bekommt einen vierten, optionalen Parameter

Bisher zählte `Evaluate` `overview.Total.Failed`/`.LoadError` (vorberechnete Zahlen). Für die Allowlist muss es jetzt die **einzelnen** `TestCaseResult`s durchgehen, um pro Test gegen die Allowlist zu prüfen:

```csharp
public static GuardianVerdict Evaluate(
    TestRunOverview overview,
    InputResolutionResult inputResolution,
    int? minTests,
    KnownFailureAllowlist? knownFailures = null)
{
    var allowlist = knownFailures ?? KnownFailureAllowlist.Empty;
    var toleratedFailures = new List<TestOutcomeEntry>();
    var unexpectedFailed = 0;
    var unexpectedLoadErrors = 0;

    foreach (var testCase in overview.Total.TestCases)
    {
        if (testCase.Outcome is not (TestOutcome.Failed or TestOutcome.LoadError))
        {
            continue;
        }

        if (allowlist.Contains(testCase.TestName))
        {
            toleratedFailures.Add(new TestOutcomeEntry(testCase.AssemblyName, testCase.TestName, testCase.Outcome));
        }
        else if (testCase.Outcome == TestOutcome.Failed)
        {
            unexpectedFailed++;
        }
        else
        {
            unexpectedLoadErrors++;
        }
    }

    var reasons = new List<VerdictReason>();
    if (unexpectedFailed > 0) { reasons.Add(new VerdictReason(VerdictReasonKind.RealTestFailures, $"{unexpectedFailed} Test(s) fehlgeschlagen.")); }
    if (unexpectedLoadErrors > 0) { reasons.Add(new VerdictReason(VerdictReasonKind.LoadErrors, $"{unexpectedLoadErrors} Test(s) konnten nicht geladen werden (Bibliothek nicht ladbar).")); }
    // ZeroTestsExecuted / BelowMinimumTestCount / UnreadableFiles / UnresolvedInputs unveraendert,
    // sie bleiben komplett unabhaengig von der Allowlist.
    ...

    return new GuardianVerdict(severity, reasons, toleratedFailures);
}
```

`overview.Total.CorrectlyExecuted` (Grundlage für `ZeroTestsExecuted`/`--min-tests`) bleibt unangetastet — ein geduldeter Fehlschlag hat den Testkörper ja tatsächlich ausgeführt, zählt also weiterhin als "korrekt ausgeführt" im Sinne von "hat ein eindeutiges Ergebnis geliefert". Nur die Rot-Einstufung selbst wird gefiltert.

## `GuardianVerdict` bekommt `ToleratedFailures`

```csharp
public sealed record GuardianVerdict(
    VerdictSeverity Severity,
    IReadOnlyList<VerdictReason> Reasons,
    IReadOnlyList<TestOutcomeEntry> ToleratedFailures = []);
```
Default `= []`, damit bestehender Code/Tests, die `new GuardianVerdict(severity, reasons)` mit nur zwei Argumenten aufrufen, unverändert weiterlaufen.

## CLI, Konsole, JSON

- `CliOptions` bekommt `string? KnownFailuresPath`; `CliArgumentParser` erkennt `--known-failures <Pfad>` analog zu `--baseline` (Wert ist Pflicht, kein Default-Mechanismus).
- `Program.cs` lädt die Allowlist im selben frühen try/catch-Block wie Baseline/JSON-Pfad-Checks; eine fehlende Datei ist ein Bedienfehler (WARNUNG, exit 1), genau wie bei `--baseline`.
- `ConsoleReportPrinter`: neue Sektion `PrintSectionIfAny("Bekannte, geduldete Fehlschläge", verdict.ToleratedFailures, FormatTestOutcomeEntry)`.
- JSON: `ToleratedFailures` hängt an `GuardianVerdict`, taucht also automatisch im bestehenden `verdict`-Feld von `JsonReport` auf — keine extra Verdrahtung nötig.

## So kannst du es selbst mit `GuardianProof` testen

In `GuardianProof/GuardianProof.Tests/NachtlaufTests.cpp`, innerhalb von `TEST_CLASS(NachtlaufDemoTests) { public: ... }`, einen neuen, absichtlich immer roten Test ergänzen (simuliert "braucht eine Lizenz/Hardware, die hier fehlt"):

```cpp
TEST_METHOD(Test_RequiresLicensedFeature)
{
    // Simuliert einen Test, der eine hier nicht verfuegbare Lizenz/Hardware braucht -
    // bewusst immer rot, um --known-failures zu demonstrieren.
    Assert::Fail(L"Dieser Test braucht eine Lizenz/Hardware, die hier nicht verfuegbar ist.");
}
```

Ablauf für die Demo:
1. Projekt neu bauen, `vstest.console.exe` laufen lassen (wie bei den bisherigen `GuardianProof`-Läufen) → neue `.trx` mit `Test_RequiresLicensedFeature` als `Failed`.
2. **Ohne** `--known-failures`: `TestGuardian <neue-trx> ` → URTEIL: FEHLERHAFT, `Test_RequiresLicensedFeature` taucht als Grund auf.
3. Datei `known-failures.txt` anlegen:
   ```
   Test_RequiresLicensedFeature
   ```
4. **Mit** `--known-failures known-failures.txt`: derselbe Lauf → URTEIL: ERFOLGREICH (sofern sonst alles grün ist), aber die Konsole zeigt trotzdem "Bekannte, geduldete Fehlschläge: Test_RequiresLicensedFeature" — sichtbar, nur nicht mehr alarmauslösend. Genau der Unterschied, den die Aufgabenstellung mit "geduldete Fehlschläge" meint.

## Tests

- `KnownFailureAllowlistTests` (Core.Tests): `Parse` ignoriert Kommentare/Leerzeilen; `Contains` case-/whitespace-sensitives Verhalten wie erwartet.
- `KnownFailureAllowlistLoaderTests` (Core.Tests, Temp-Dateien): existierende Datei lädt; fehlende Datei → `Failure`.
- `TestRunVerdictTests`: ein allowlisteter Fehlschlag allein → Grün, taucht in `ToleratedFailures` auf, nicht in `Reasons`; ein allowlisteter **und** ein nicht-gelisteter Fehlschlag zusammen → weiterhin Rot (nur der nicht gelistete zählt), beide erscheinen aber unterschiedlich (einer als Grund, einer als toleriert); ohne Allowlist unverändertes bisheriges Verhalten (Regressionsschutz für alle bestehenden Tests).
- `CliArgumentParserTests`: `--known-failures <Pfad>` setzt `KnownFailuresPath`; ohne Wert wirft.
- `ConsoleReportPrinterTests`: neue Sektion erscheint nur, wenn `ToleratedFailures` nicht leer ist.

## Nicht Teil dieses Features

- Kein echtes Flaky-Handling (mehrfach ausführen, Mehrheitsentscheidung bei nicht-deterministischen Tests) — die Aufgabenstellung meint mit dem Beispiel "Tests, die eine Installation brauchen" einen deterministischen, umgebungsbedingten Fehlschlag, kein Nichtdeterminismus.
- `BaselineComparer` bleibt bei seinem bisherigen `AssemblyName`+`TestName`-Abgleich (volle `storage`-Pfade) — dieselbe Fragilität wie oben beschrieben besteht dort weiterhin, wird hier aber nicht mit angefasst (eigenes Feature, eigener Zeitpunkt, falls je gewünscht).
- Keine Mustererkennung/Wildcards in der Allowlist-Datei (z.B. `Test_*`) — exakte Namen reichen für den Zweck.
