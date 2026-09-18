# Plan: feature/multi-file-input

Drittes Teil-A-Feature aus `docs/plans/teil-a-overview.md`: Eingabe auflösen — einzelne Datei, Ordner oder Suchmuster → Liste tatsächlicher `.trx`-Dateipfade — **und** diese Dateien einlesen, mit Live-Fortschritt für die CLI. Die eigentliche CLI-Argument-Verdrahtung (inkl. der dreistufigen `--max-depth`-Logik) sowie die Exit-Code-Entscheidung bleiben Feature 4.

**Basis-Branch:** `main` (nach Merge von `feature/trx-aggregation-summary`).

**Änderungshistorie:** Ursprünglich nur Pfad-Auflösung mit einem `recursive: bool`-Schalter. Nach Rückfrage (2026-09-18) um zwei Dinge erweitert: eine `--max-depth`-Tiefenbegrenzung statt eines reinen Ein/Aus-Schalters, und einen Live-Fortschritt beim Einlesen. Details unten.

## Neuer Namensraum

`TestGuardian.Core.Input` für die Pfad-Auflösung (`TrxInputResolver`) — fachlich unabhängig vom `.trx`-Format. Der Batch-Reader (`TrxBatchReader`) bleibt in `TestGuardian.Core.Trx`, weil er direkt auf `TrxFileReader`/`TrxReadResult` aufbaut.

## Teil 1: Pfad-Auflösung mit Tiefenbegrenzung

```csharp
public sealed record UnresolvedInput(string RawInput, string Reason);

public sealed record InputResolutionResult(
    IReadOnlyList<string> ResolvedFilePaths,
    IReadOnlyList<UnresolvedInput> UnresolvedInputs);

public static class TrxInputResolver
{
    public static InputResolutionResult Resolve(IEnumerable<string> rawInputs, int maxDepth = 0)
    {
        // Für jeden rawInput:
        // 1. Existierende Datei -> direkt übernehmen.
        // 2. Existierender Ordner -> eigener rekursiver Walk (s.u.) bis maxDepth Ebenen tief.
        //    Keine .trx-Treffer -> UnresolvedInput.
        // 3. Sonst: Verzeichnis+Dateimuster (Path.GetDirectoryName/GetFileName), nur die
        //    angegebene Ebene (kein maxDepth für Suchmuster, siehe Begründung unten).
        //    Verzeichnisteil existiert nicht oder keine Treffer -> UnresolvedInput.
        // Ergebnis: Path.GetFullPath normalisieren, deduplizieren, alphabetisch sortieren.
    }
}
```

**Warum ein eigener rekursiver Walk statt `Directory.GetFiles(..., SearchOption.AllDirectories)`:** `AllDirectories` kennt keine Tiefenbegrenzung und kann sich unter Windows bei symbolischen Links/Junctions in einer Schleife verfangen — genau das Risiko, das `--max-depth` ausschließen soll. Ein eigener, einfacher rekursiver Walk (pro Ebene `Directory.GetFiles(dir, "*.trx", TopDirectoryOnly)` + `Directory.GetDirectories(dir)`, mit einem Tiefen-Zähler, der bei 0 abbricht) gibt uns diese Kontrolle:

```csharp
private static void CollectTrxFiles(string directory, int depthRemaining, List<string> results)
{
    results.AddRange(Directory.GetFiles(directory, "*.trx", SearchOption.TopDirectoryOnly));
    if (depthRemaining <= 0)
    {
        return;
    }

    foreach (var subdirectory in Directory.GetDirectories(directory))
    {
        CollectTrxFiles(subdirectory, depthRemaining - 1, results);
    }
}
```

`maxDepth = 0` durchsucht nur den angegebenen Ordner selbst (keine Unterordner) — das ist der neue Default, wenn der Aufrufer nichts angibt. `maxDepth = N` durchsucht `N` Ebenen von Unterordnern zusätzlich.

**Warum `--max-depth` nicht auch für Suchmuster (Fall 3) gilt:** Ein Suchmuster wie `C:\ergebnisse\lauf-*.trx` bezieht sich laut Aufgabenstellung auf eine konkrete Ebene (Dateiname mit Wildcard in einem bestimmten Verzeichnis), nicht auf einen zu durchsuchenden Baum. Rekursion ist über die explizite Ordner-Eingabe (Fall 2) abgedeckt.

**Warum die dreistufige `--max-depth`-Semantik (Schalter fehlt / Schalter ohne Wert / Schalter mit Wert) nicht hier, sondern in Feature 4 sitzt:** Das ist reines CLI-Argument-Parsing (`System.CommandLine` o.ä. muss zwischen "Option nicht angegeben", "Option ohne Wert" und "Option mit Wert" unterscheiden). `TrxInputResolver.Resolve` bekommt am Ende einfach eine fertige Ganzzahl (`maxDepth = 0` als Default, wenn gar nichts angegeben wird) und muss diese Unterscheidung nicht selbst kennen. Die konkrete Zuordnung (fehlt → 0, ohne Wert → 10, mit Wert → dieser Wert) wird in `docs/plans/teil-a-overview.md` unter "Eingabe-Auflösung" festgehalten, zur Umsetzung in Feature 4.

## Teil 2: Batch-Reader mit Live-Fortschritt

```csharp
namespace TestGuardian.Core.Trx;

public sealed record ReadProgress(int FilesRead, int TotalFiles, int TestsReadSoFar);

public static class TrxBatchReader
{
    public static IReadOnlyList<TrxReadResult> ReadAll(
        IReadOnlyList<string> filePaths,
        IProgress<ReadProgress>? progress = null)
    {
        var results = new List<TrxReadResult>(filePaths.Count);
        var testsReadSoFar = 0;

        for (var i = 0; i < filePaths.Count; i++)
        {
            var result = TrxFileReader.Read(filePaths[i]);
            results.Add(result);

            if (result is TrxReadResult.Success success)
            {
                testsReadSoFar += success.Run.TestCases.Count;
            }

            progress?.Report(new ReadProgress(i + 1, filePaths.Count, testsReadSoFar));
        }

        return results;
    }
}
```

**Warum Fortschritt pro Datei statt "X von Y Tests" mit vorab bekanntem Y:** Die Gesamtzahl der Tests steht erst fest, wenn wirklich jede Datei geparst wurde — sie vorab zu kennen würde entweder einen kompletten Vorab-Durchlauf (der den Zweck der Anzeige untergräbt) oder eine Schätzung aus den deklarierten `<Counters total>`-Werten bedeuten. Letzteres hieße, sich ausgerechnet für eine reine UI-Anzeige auf genau die Zahlen zu verlassen, denen der Wächter fachlich explizit misstraut. Stattdessen: `TotalFiles` steht nach der Pfad-Auflösung exakt fest (keine Schätzung nötig), und `TestsReadSoFar` wächst live und akkurat mit jeder tatsächlich gelesenen Datei — die CLI kann daraus z.B. "Datei 3 von 10 gelesen (137 Tests bisher)" formatieren. Eine hängende vs. eine nur langsame Ausführung lässt sich daran genauso gut unterscheiden (der Zähler bewegt sich oder er tut es nicht), ohne mit unsicheren Schätzwerten zu arbeiten.

**Warum `IProgress<T>` aus dem BCL statt einer eigenen Callback-Schnittstelle:** Standardmechanismus in .NET genau für diesen Zweck, keine zusätzliche Abstraktion nötig, keine Installation erforderlich. Der Parameter ist optional (`null`-Default), damit `ReadAll` auch ganz ohne CLI/Fortschrittsanzeige (z.B. in Tests) einfach aufrufbar bleibt.

**Warum eine fehlgeschlagene Einzeldatei den Batch nicht stoppt:** Konsistent mit der bereits in Feature 1/2 etablierten Regel — eine nicht lesbare Datei ist ein eigener, sichtbarer Fehlerfall (`TrxReadResult.Failure`), kein Abbruchgrund für die übrigen Dateien.

## Geplante Unit-Tests

### `TrxInputResolver` (alle mit echten temporären Dateien/Ordnern, Aufräumen in `finally`)

| Test | Prüft |
|---|---|
| `Resolve_SingleExistingFile_ReturnsThatPath` | Einzelne, existierende Datei wird 1:1 übernommen |
| `Resolve_DirectoryWithDefaultMaxDepth_ReturnsOnlyTopLevelTrxFiles` | `maxDepth: 0` (Default) liefert nur `.trx`-Dateien direkt im Ordner, keine aus Unterordnern |
| `Resolve_DirectoryWithMaxDepth_RespectsDepthLimit` | Struktur `root/a.trx`, `root/sub1/b.trx`, `root/sub1/sub2/c.trx`; `maxDepth: 1` liefert `a.trx` und `b.trx`, **nicht** `c.trx` |
| `Resolve_WildcardPattern_ReturnsMatchingFilesOnly` | Ordner mit `lauf-a.trx`, `lauf-b.trx`, `andere.txt`; Muster `<ordner>\lauf-*.trx` liefert nur die zwei passenden `.trx`-Dateien |
| `Resolve_NonexistentPath_AddsUnresolvedInput` | Nicht existierender Pfad landet in `UnresolvedInputs`, nicht in `ResolvedFilePaths` |
| `Resolve_DirectoryWithNoTrxFiles_AddsUnresolvedInput` | Existierender, aber `.trx`-loser Ordner → `UnresolvedInputs` |
| `Resolve_DuplicateInputs_DeduplicatesResolvedPaths` | Dieselbe Datei einmal direkt und einmal über einen Ordner-Scan erfasst → nur einmal im Ergebnis |
| `Resolve_MultipleInputs_CombinesAndSortsResults` | Zwei verschiedene Eingaben (Datei + Ordner) kombiniert, alphabetisch sortiertes Ergebnis |

### `TrxBatchReader`

| Test | Prüft |
|---|---|
| `ReadAll_MultipleFiles_ReturnsResultsInSameOrderAsInput` | `[lauf-a.trx, lauf-d.trx]` → Ergebnisliste in derselben Reihenfolge |
| `ReadAll_ReportsProgressAfterEachFile_WithRunningTestCount` | Ein Test-`IProgress<ReadProgress>` sammelt alle Reports; `FilesRead` zählt 1,2,…, `TotalFiles` bleibt konstant, `TestsReadSoFar` wächst kumulativ (z.B. nach Datei 1: 7, nach Datei 2: 10) |
| `ReadAll_UnreadableFileAmongOthers_ContinuesBatchAndReportsZeroTestsForIt` | Eine nicht existierende Datei zwischen zwei guten → Batch läuft weiter, `TestsReadSoFar` steigt für die kaputte Datei nicht |
| `ReadAll_EmptyFileList_ReturnsEmptyResultAndNoProgressReports` | Randfall: leere Eingabeliste |

## Verhältnis zu Feature 4 (Hinweis, keine Entscheidung in diesem Feature)

- Ob `UnresolvedInputs` allein (z.B. Tippfehler im Pfad) einen Fehlschlag auslöst — analog zur `LoadError`-Regel.
- Die dreistufige `--max-depth`-Parsing-Logik (fehlt/ohne Wert/mit Wert) und wie `IProgress<ReadProgress>` konkret in Konsolenausgabe übersetzt wird.

## Nicht Teil dieses Features

- CLI-Argument-Parsing selbst (`--max-depth`, `--min-tests`) — Feature 4.
- Ob `UnresolvedInputs` die Exit-Code-Entscheidung beeinflusst — Feature 4.
- Echtes mehrstufiges Glob-Pattern-Matching (`**`) — nur bei Bedarf und mit Zustimmung zu einer zusätzlichen Bibliothek.
