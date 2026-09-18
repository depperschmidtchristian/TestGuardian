# Plan: feature/trx-domain-model

Umsetzung des ersten Teil-A-Features aus `docs/plans/teil-a-overview.md`: Domänenmodell, Parser und Klassifizierung für eine **einzelne** `.trx`-Datei. Aggregation über mehrere Dateien (Feature 2), Eingabe-Auflösung (Feature 3) und Exit-Code/CLI (Feature 4) sind **nicht** Teil dieses Features.

**Basis-Branch:** Wir sind aktuell auf `chore/restructure-project-layout` (noch nicht committet/gemerged). `feature/trx-domain-model` wird von diesem Branch abzweigen, da das neue `TestGuardian.Core`/`TestGuardian.Console`-Grundgerüst dort liegt und in `main` noch nicht existiert.

## Betroffene Projekte

- `TestGuardian.Core` — neuer Ordner/Namespace `TestGuardian.Core.Trx` für das gesamte Feature.
- Neues Testprojekt (siehe Abschnitt "Testframework — braucht deine Zustimmung").

## Domänenmodell

```csharp
public enum TestOutcome
{
    Passed,
    Failed,
    LoadError,
    Skipped,
    Other // bewusste Auffangkategorie, siehe Klassifizierungsregeln unten
}

public sealed record TestCaseResult(
    string AssemblyName,
    string TestName,
    TestOutcome Outcome,
    string RawOutcome,        // Original-vstest-Outcome, für Other/Diagnose
    string? ErrorMessage);

public sealed record RunWarning(string Message);

public sealed record TestRunResult(
    string RunName,
    IReadOnlyList<TestCaseResult> TestCases,
    IReadOnlyList<RunWarning> Warnings);
```

**Warum `RawOutcome` zusätzlich zu `TestOutcome`:** Wenn ein vstest-Outcome nicht in unsere bekannten Kategorien passt, landet er in `Other` — aber `RawOutcome` sorgt dafür, dass die tatsächliche Information nicht verloren geht und in der Ausgabe sichtbar bleibt, statt in einer nichtssagenden Sammelkategorie zu verschwinden.

### Lese-Ergebnis (Fehlerfälle explizit, kein stiller Erfolg)

```csharp
public enum TrxReadFailureReason
{
    FileNotFound,
    EmptyFile,
    MalformedXml
}

public sealed record TrxReadFailure(string FilePath, TrxReadFailureReason Reason, string Detail);

public abstract record TrxReadResult
{
    public sealed record Success(TestRunResult Run) : TrxReadResult;
    public sealed record Failure(TrxReadFailure Error) : TrxReadResult;

    private TrxReadResult() { }
}
```

**Warum ein eigener Ergebnistyp statt Exceptions:** Eine kaputte/fehlende Datei ist im Kontext dieses Tools ein **erwarteter, normaler Fall** (Teil der Pflichtanforderung), kein Ausnahmezustand. Ein Result-Typ zwingt jeden Aufrufer (später: Feature 2/3), den Fehlerfall explizit zu behandeln, statt ihn versehentlich mit einem ungefangenen `try/catch` zu verschlucken.

## Aufteilung in zwei Klassen

- **`TrxDocumentParser.Parse(string xmlContent) : TrxReadResult`** — rein, keine Datei-I/O. Bekommt den XML-Inhalt als String, parst und klassifiziert. Fängt `XmlException` intern ab → `Failure(MalformedXml)`.
- **`TrxFileReader.Read(string filePath) : TrxReadResult`** — prüft Existenz (`FileNotFound`) und Leerheit (`EmptyFile`) der Datei, liest den Inhalt und delegiert an `TrxDocumentParser.Parse`.

**Warum diese Trennung:** Die Klassifizierungslogik (der eigentliche fachliche Kern) lässt sich so testen, ohne echte Dateien anzulegen — nur die zwei dateibezogenen Fehlerfälle (fehlt/leer) brauchen echte oder simulierte Dateisystem-Interaktion.

## Klassifizierungsregeln

Namespace der `.trx`-Datei: `http://microsoft.com/schemas/VisualStudio/TeamTest/2010`.

1. Aufbau einer Zuordnung `testId → storage` (Assembly) aus `<TestDefinitions><UnitTest id="..." storage="..."/></TestDefinitions>`.
2. Für jedes `<Results><UnitTestResult testId="..." testName="..." outcome="..."/></Results>`:
   - Assembly über die Zuordnung aus Schritt 1 auflösen. Fehlt der Eintrag (sollte laut Format nicht vorkommen, aber wird nicht angenommen) → Assembly `"<unbekannt>"` + `RunWarning`.
   - Fehlermeldung: `Output/ErrorInfo/Message`, falls vorhanden.
   - Klassifizierung von `outcome`:
     - `"Passed"` → `Passed`
     - `"Failed"` mit Fehlermeldung, die `"Failed to load the test assembly or its dependencies"` enthält → `LoadError`
     - `"Failed"` (sonst) → `Failed`
     - `"NotExecuted"` → `Skipped`
     - alles andere (`Inconclusive`, `Timeout`, `Aborted`, `Error`, `NotRunnable`, `Disconnected`, `Warning`, `PassedButRunAborted`, `Pending`, `InProgress`, unbekannte Werte) → `Other` + `RunWarning` mit dem rohen Outcome-Text.

**Warum nur Passed/Failed/NotExecuted whitelisten und der Rest in `Other` + Warnung landet:** Bewusstes Misstrauen statt Vermutung — für seltene/unerwartete vstest-Outcomes raten wir nicht, ob sie "wie Passed" oder "wie Failed" zu werten sind, sondern machen sie sichtbar und überlassen die Wertung (in Feature 4) einer expliziten Entscheidung.

## Counters-Validierung und RunInfo-Warnungen

- Deklarierte `<Counters total="..." executed="..." passed="..." failed="..." .../>` werden gelesen, aber **nicht** als Wahrheit übernommen (siehe `Readme.pdf`-Hinweis zu den Beispieldaten).
- Errechnete Werte aus der tatsächlichen `<Results>`-Liste werden mit den deklarierten verglichen (`total`, `executed`, `passed`, `failed`). Bei Abweichung → `RunWarning` mit den konkreten abweichenden Zahlen (z.B. `"Deklariert: total=5, tatsächlich gefunden: 2"`).
- `<ResultSummary><RunInfos><RunInfo outcome="Warning"><Text>...</Text></RunInfo></RunInfos>` — jeder `RunInfo`-Text wird 1:1 als `RunWarning` übernommen (das ist z.B. die Filter-Meldung in `lauf-b.trx`).

## Geplante Unit-Tests (Zuordnung zu den Beispieldaten)

| Test | Fixture | Prüft |
|---|---|---|
| `Parse_MixedRun_ClassifiesPassedAndFailed` | `lauf-a.trx` | 4 Passed, 3 Failed, keine Warnungen, Counters stimmen |
| `Parse_EmptyFilterRun_ZeroTestsWithFilterWarning` | `lauf-b.trx` | 0 `TestCases`, genau eine `RunWarning` mit dem Filtertext |
| `Parse_LoadErrorRun_SeparatesLoadErrorFromFailed` | `lauf-c.trx` | 1 Passed, 0 Failed, 3 LoadError |
| `Parse_CleanGreenRun_AllPassedNoWarnings` | `lauf-d.trx` | 3 Passed, keine Warnungen |
| `Parse_TruncatedXml_ReturnsMalformedXmlFailure` | `lauf-e.trx` | `Failure` mit `Reason == MalformedXml`, kein Absturz |
| `Read_MissingFile_ReturnsFileNotFoundFailure` | synthetisch | `TrxFileReader` mit nicht existierendem Pfad |
| `Read_EmptyFile_ReturnsEmptyFileFailure` | synthetisch | 0-Byte-Datei |
| `Parse_CountersMismatch_AddsWarning` | synthetisch (wohlgeformtes XML, aber falsche `<Counters>`-Werte) | Warnung mit den abweichenden Zahlen, da `lauf-e.trx` selbst wegen Truncation nicht bis zur Counters-Prüfung kommt |
| `Parse_UnknownOutcome_MapsToOtherWithRawOutcomePreserved` | synthetisch | z.B. `outcome="Inconclusive"` → `Other`, `RawOutcome == "Inconclusive"`, Warnung vorhanden |

Fixture-Zugriff: Ein kleiner Test-Helper löst den Pfad zu `Fixtures/Sample Data/` relativ zum Repo-Root auf (Suche nach `TestGuardian.sln`/`src.sln` ausgehend vom Testassembly-Verzeichnis nach oben), statt die Dateien zu duplizieren.

## Testframework — braucht deine Zustimmung

Um diese Tests zu schreiben, braucht es ein Testprojekt mit NuGet-Paketen (Test-SDK + Framework + Test-Adapter). Das fällt unter "nichts installieren ohne Einverständnis". Zwei sinnvolle Optionen:

- **MSTest** (`MSTest.TestFramework`, `MSTest.TestAdapter`): gleiches Framework wie in Teil B (C++) vorgeschrieben — ein einheitliches mentales Modell über beide Sprachen hinweg.
- **xUnit**: in der .NET-Welt aktuell der gebräuchlichste Standard für neue Projekte, etwas schlankere Syntax (`[Fact]`/`[Theory]` statt `[TestMethod]`).

Ich tendiere zu MSTest wegen der Konsistenz mit Teil B, aber das ist deine Entscheidung — bitte bestätige eine Option, bevor ich das Testprojekt anlege.

## Nicht Teil dieses Features (bewusst ausgeklammert)

- Aggregation mehrerer Dateien zu einer Gesamtübersicht (Feature 2).
- Datei-/Ordner-/Muster-Auflösung (Feature 3).
- Exit-Code-Entscheidung, `--min-tests`, CLI-Ausgabe (Feature 4).
- JSON-Ausgabe, Baseline-Vergleich, Liste geduldeter Fehlschläge (Kür, falls überhaupt, erst nach den Pflichtteilen).
