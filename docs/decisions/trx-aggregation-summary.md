# Entscheidungen: feature/trx-aggregation-summary

Ergänzt `docs/plans/trx-aggregation-summary.md` um Details, die erst bei der Umsetzung konkret wurden.

## `StringComparer.Ordinal` für die Gruppierung/Sortierung nach Assembly-Namen

`.trx`-`storage`-Attribute sind Dateipfade/-namen, keine benutzersprachlichen Texte — ein kultureller String-Vergleich (z.B. türkisches "I") hätte hier keinen Sinn und nur eine unnötige Abhängigkeit von der Locale der Maschine eingeführt, auf der der Wächter läuft. `GroupBy` selbst nutzt implizit Ordinalvergleich für `string`-Keys; die anschließende Sortierung wurde explizit mit `StringComparer.Ordinal` versehen, um das nicht dem Default zu überlassen.

## Aggregator wirft nichts, gibt aber auch keinen eigenen Fehlerfall zurück

`TestRunAggregator.Aggregate` hat keinen eigenen Fehlerpfad — er kann mit jeder Kombination aus `Success`/`Failure`-Einträgen umgehen, auch mit einer leeren Liste. Ein "leeres Ergebnis" (keine Assemblies, `Total.Total == 0`) ist hier bewusst kein Fehler auf dieser Ebene, sondern nur eine Zahl, die Feature 4 gemäß der bereits beschlossenen "0 Tests ausgeführt"-Regel bewertet. Die Aggregation soll rein strukturell bleiben, nicht schon selbst Entscheidungen über grün/rot treffen.

## `TestRunOverview.Total` und `AssemblySummary`-Zählwerte sind berechnet, nicht gespeichert

Wie im Plan begründet: eine einzige Quelle der Wahrheit (die rohe `TestCases`-Liste), keine Möglichkeit für Zahlen, die von der zugrunde liegenden Liste abweichen. Das hat sich bei der Umsetzung bestätigt — `Total` ist buchstäblich nur `Assemblies.SelectMany(a => a.TestCases)`, keine eigene Summierungslogik nötig.

## Korrektur: `Executed` aufgeteilt in `Attempted` und `CorrectlyExecuted`

Beim Review hat der Nutzer zurecht bemängelt, dass der Test `AssemblySummary_Executed_ExcludesLoadErrorAndSkipped` einen falschen Assert-Wert hatte (`2` statt der laut eigener Formel korrekten `3`) — und dass nie klar entschieden war, ob `TestOutcome.Other` überhaupt als "ausgeführt" zählen soll. Das ursprüngliche `Executed`-Feld (`Passed + Failed + Other`) wurde durch zwei benannte Felder ersetzt:

- `Attempted` (`Passed + Failed + Other`): der Testkörper wurde gestartet, auch bei uneindeutigem Ergebnis.
- `CorrectlyExecuted` (`Passed + Failed`): striktere Variante ohne `Other`, für Fälle, in denen ein eindeutiges Verdikt gefordert ist.

Der zugehörige Test wurde in zwei klar benannte Tests aufgeteilt statt einen mehrdeutigen zu behalten. Welches Feld Feature 4 tatsächlich für die Exit-Code-Entscheidung nutzt, ist dort zu klären, nicht hier.

## Build vs. Testlauf

Wie schon bei `feature/trx-domain-model`: `dotnet build` lief erfolgreich (0 Fehler, 0 Warnungen). `dotnet test` wurde bewusst nicht ausgeführt — das bleibt deiner Prüfung vorbehalten.
