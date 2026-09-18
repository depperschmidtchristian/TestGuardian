# Plan: feature/trx-aggregation-summary

Zweites Teil-A-Feature aus `docs/plans/teil-a-overview.md`: mehrere bereits geparste `.trx`-Läufe (Ergebnis von `TrxFileReader`/`TrxDocumentParser` aus `feature/trx-domain-model`) zu einer Übersicht **je Bibliothek und gesamt** zusammenführen. Weder Datei-/Ordner-Auflösung (Feature 3) noch die Exit-Code-Entscheidung (Feature 4) sind Teil dieses Features — dieses Feature liefert nur die aggregierte Datenstruktur, auf der Feature 4 später die Entscheidung trifft und die CLI die Ausgabe formatiert.

**Basis-Branch:** `main` (nach Merge von `feature/trx-domain-model`).

## Warum "je Bibliothek" über Assembly-Namen und nicht über Dateien gruppiert wird

Die Aufgabenstellung verlangt eine Übersicht "je Bibliothek" — das ist der `AssemblyName` aus `TestCaseResult` (dem `storage`-Attribut aus der `.trx`), nicht die einlesende `.trx`-Datei selbst. Zwei `.trx`-Dateien können durchaus dieselbe Bibliothek betreffen (z.B. ein Nachtlauf und ein Tageslauf derselben Testassembly) — deren Ergebnisse sollen unter einer gemeinsamen Zeile zusammengeführt werden, nicht pro Datei getrennt ausgewiesen werden.

## Domänenmodell

```csharp
public sealed record AssemblySummary(string AssemblyName, IReadOnlyList<TestCaseResult> TestCases)
{
    public int Passed => TestCases.Count(t => t.Outcome == TestOutcome.Passed);
    public int Failed => TestCases.Count(t => t.Outcome == TestOutcome.Failed);
    public int LoadError => TestCases.Count(t => t.Outcome == TestOutcome.LoadError);
    public int Skipped => TestCases.Count(t => t.Outcome == TestOutcome.Skipped);
    public int Other => TestCases.Count(t => t.Outcome == TestOutcome.Other);
    public int Total => TestCases.Count;
    public int Executed => Passed + Failed + Other;
}

public sealed record AggregatedWarning(string RunName, string Message);

public sealed record TestRunOverview(
    IReadOnlyList<AssemblySummary> Assemblies,
    IReadOnlyList<TrxReadFailure> FileFailures,
    IReadOnlyList<AggregatedWarning> Warnings)
{
    public AssemblySummary Total => new("Gesamt", Assemblies.SelectMany(a => a.TestCases).ToList());
}
```

**Warum Zähl-Properties statt gespeicherter Zahlen:** `AssemblySummary` speichert nur die rohe `TestCases`-Liste; `Passed`/`Failed`/`Total`/… sind berechnete Properties darüber. Damit gibt es nur eine Quelle der Wahrheit — keine Möglichkeit, dass gespeicherte Zählwerte und die zugrunde liegende Liste auseinanderlaufen. Gleiches Prinzip bei `TestRunOverview.Total`, das einfach alle `AssemblySummary.TestCases` zusammenfasst statt eigene Summen mitzuführen.

**Warum `Executed` weder `LoadError` noch `Skipped` mitzählt:** Konsistent mit der bereits für Feature 4 beschlossenen Regel (LoadErrors zählen nicht als "ausgeführt", weil der Testkörper nie lief). `Skipped` (vstest-Outcome `NotExecuted`) ist vom Wortsinn her ebenfalls kein ausgeführter Test — sagt bitte Bescheid, falls du das anders siehst, aber ich würde das ohne Rückfrage so umsetzen, da es sich direkt aus der bestehenden Regel ableitet.

**Warum `FileFailures` vom Typ `IReadOnlyList<TrxReadFailure>` ist (kein neuer Wrapper-Typ):** `TrxReadFailure` aus Feature 1 trägt bereits `FilePath`, `Reason` und `Detail` — alles, was für die Anzeige einer nicht lesbaren Datei nötig ist. Ein zusätzlicher Typ wäre nur eine unnötige Hülle um dieselben Daten.

**Warum `AggregatedWarning` ein eigener Typ ist (und keine String-Verkettung):** `RunWarning` aus Feature 1 kennt nur die eigene Meldung, nicht den Namen des Laufs, aus dem sie stammt. Da diese Aggregation mehrere Läufe zusammenführt, würde eine unattribuierte Warnung ("Kein Test entspricht dem Filter") bei mehreren Dateien nicht mehr erkennen lassen, woher sie kommt. Statt die Herkunft in den Text hineinzuschreiben (verlustbehaftet, schwer wieder zu trennen), trägt `AggregatedWarning` `RunName` und `Message` getrennt — Feature 4/die CLI entscheidet dann selbst, wie sie das formatiert.

## Aggregations-Logik

```csharp
public static class TestRunAggregator
{
    public static TestRunOverview Aggregate(IEnumerable<TrxReadResult> results)
    {
        // 1. Ergebnisse in Success/Failure aufteilen (Pattern-Matching auf TrxReadResult).
        // 2. Aus allen Success-Läufen: TestCases nach AssemblyName gruppieren (case-sensitive,
        //    exakter String-Vergleich des storage-Attributs) -> je Gruppe ein AssemblySummary.
        // 3. Assemblies alphabetisch nach AssemblyName sortieren (deterministische, lesbare Ausgabe).
        // 4. Aus allen Success-Läufen: Warnings einsammeln, je Warnung mit dem RunName des
        //    jeweiligen TestRunResult zu AggregatedWarning verpacken.
        // 5. Aus allen Failure-Ergebnissen: TrxReadFailure direkt in FileFailures übernehmen.
    }
}
```

Keine Sonderbehandlung für eine leere Eingabe (`results` ist leer oder enthält nur Failures): das Ergebnis ist dann einfach ein `TestRunOverview` mit leerer `Assemblies`-Liste und `Total.Total == 0` — das ist korrekt und wird von Feature 4 über die ohnehin bestehende "0 Tests ausgeführt"-Regel abgefangen, keine zusätzliche Logik hier nötig.

## Geplante Unit-Tests

| Test | Grundlage | Prüft |
|---|---|---|
| `Aggregate_MultipleRunsOfSameAssembly_MergesCountsAcrossFiles` | `lauf-a.trx` + `lauf-d.trx` (beide `widgetlib.testcpp.dll`) | Eine gemeinsame `AssemblySummary` mit summierten Zählwerten (7 Passed, 3 Failed) statt zwei getrennter Einträge |
| `Aggregate_LoadErrorAndPassed_ProducesSeparateAssemblySummaries` | `lauf-c.trx` (`widgetlib.testcpp.dll` + `dienstlib.testcpp.dll`) | Zwei `AssemblySummary`-Einträge, `dienstlib` mit `LoadError == 3`, `Failed == 0` |
| `Aggregate_UnreadableFile_AddsToFileFailuresWithoutCrashing` | synthetisch: ein `Success` + ein `Failure` | `FileFailures` enthält den Fehler, `Assemblies` bleibt unberührt vom Fehlerfall |
| `Aggregate_NoResults_ProducesEmptyOverviewWithZeroTotal` | leere Eingabe | `Assemblies` leer, `Total.Total == 0` |
| `Aggregate_Warnings_AreTaggedWithOriginatingRunName` | `lauf-b.trx` | `AggregatedWarning.RunName == "Beispiellauf Leer"`, Message enthält den Filtertext |
| `Aggregate_Assemblies_AreSortedAlphabetically` | synthetisch: zwei Läufe mit vertauschter Assembly-Reihenfolge | Ausgabereihenfolge ist alphabetisch, unabhängig von Eingabereihenfolge |
| `AssemblySummary_Executed_ExcludesLoadErrorAndSkipped` | synthetisch: eine Liste mit je einem Passed/Failed/LoadError/Skipped/Other | `Executed == 2` (nur Passed+Failed+Other), `Total == 5` |
| `TestRunOverview_Total_SumsAcrossAllAssemblySummaries` | `lauf-a.trx` + `lauf-c.trx` | `Total.Total` entspricht der Summe aller `TestCases` über beide Läufe |

## Nicht Teil dieses Features

- Datei-/Ordner-/Muster-Auflösung, also wie die `IEnumerable<TrxReadResult>` überhaupt entsteht (Feature 3).
- Exit-Code-Entscheidung, `--min-tests`-Auswertung, "mind. ein LoadError → Fehlschlag"-Regel (Feature 4) — dieses Feature liefert nur die Zahlen, auf denen Feature 4 entscheidet.
- Ausgabeformatierung für die Konsole (Feature 4) bzw. JSON-Export (Kür).
