# Entscheidung: feature/known-failures-allowlist

Umsetzung des in `docs/plans/known-failures-allowlist.md` beschriebenen Plans.

## Abgleich nur über den Testnamen

Bewusst anders als `BaselineComparer` (Assembly+Testname): `AssemblyName` in `TestCaseResult` ist das rohe `storage`-Attribut aus der `.trx` und bei echten `vstest.console.exe`-Läufen ein voller, maschinenspezifischer Pfad. Eine von Hand gepflegte Allowlist-Datei mit solchen Pfaden wäre bei jedem Rechner-/Build-Konfigurationswechsel hinfällig. Testname-only ist robust genug für den Zweck (eine überschaubare Anzahl bekannter Tests) und macht die Datei trivial von Hand schreib- und lesbar — genau das, was für eine Live-Demo zählt. `BaselineComparer` hat dieselbe grundsätzliche Fragilität, wurde hier aber nicht mit angefasst (separates Feature, siehe Plan).

## `TestOutcomeEntry` von `Baseline.Models` nach `Trx.Models` verschoben

Wird jetzt von zwei Features gebraucht (Baseline-Vergleich, geduldete Fehlschläge) — gehört als allgemeiner "benannter Testausgang" fachlich neben `TestCaseResult`/`TestOutcome`, nicht mehr baseline-spezifisch. Reine Verschiebung, keine inhaltliche Änderung.

## `TestRunVerdict.Evaluate` iteriert jetzt einzelne Testfälle statt nur Summen zu lesen

Vorher: `overview.Total.Failed`/`.LoadError` (vorberechnete Zahlen aus `AssemblySummary`) direkt für die Rot-Gründe verwendet. Für die Allowlist muss jeder einzelne `TestCaseResult` gegen die Liste geprüft werden — die Methode iteriert jetzt `overview.Total.TestCases` selbst und zählt unerwartete Fehlschläge/Ladefehler getrennt von tolerierten. `overview.Total.CorrectlyExecuted` (Grundlage für `ZeroTestsExecuted`/`--min-tests`) bleibt davon unberührt: ein tolerierter Fehlschlag hat den Testkörper tatsächlich ausgeführt und ein eindeutiges Ergebnis geliefert, zählt also weiterhin dazu — nur die Rot-Einstufung selbst wird gefiltert.

## `GuardianVerdict.ToleratedFailures`: Default-Wert über eine neu deklarierte Property, nicht `= []`

Der naheliegende Weg (`IReadOnlyList<T> ToleratedFailures = []` direkt am positional Record-Parameter) hätte bei `null`-Übergabe trotzdem `null` durchgereicht, nicht automatisch `[]`. Stattdessen ist der Parameter nullable (`IReadOnlyList<TestOutcomeEntry>? ToleratedFailures = null`) und die tatsächliche, nicht-nullable Property wird im Record-Body neu deklariert mit `= ToleratedFailures ?? []` — ein Standard-Idiom für Records, bei dem der Parameter nur zur Initialisierung dient und die "echte" Property davon abweichendes Verhalten (hier: nie `null`) haben darf. Aufrufer müssen nie auf `null` prüfen, nur auf `Count == 0`.

## Geduldete Fehlschläge bleiben immer sichtbar, auch wenn sie das Urteil nicht mehr beeinflussen

`ConsoleReportPrinter` bekommt eine eigene Sektion "Bekannte, geduldete Fehlschläge" (`PrintSectionIfAny`, wie die bestehenden Sektionen), unabhängig vom Urteil selbst gedruckt. Landet automatisch auch im JSON-Export, weil `ToleratedFailures` Teil von `GuardianVerdict` ist und `GuardianVerdict` schon Teil von `JsonReport` war — keine zusätzliche Verdrahtung nötig. Konsistent mit der Grundregel des ganzen Projekts: nichts, was tatsächlich passiert ist, verschwindet stillschweigend aus der Ausgabe.

## Manuell nachvollziehbar mit `GuardianProof`

`docs/plans/known-failures-allowlist.md` enthält eine konkrete `TEST_METHOD`-Vorlage für `GuardianProof/GuardianProof.Tests/NachtlaufTests.cpp` (immer fehlschlagend, simuliert einen Test, der eine fehlende Lizenz/Hardware braucht) plus die Schritt-für-Schritt-Demo (ohne Allowlist → FEHLERHAFT, mit Allowlist → ERFOLGREICH, aber weiterhin sichtbar) — auf Wunsch, damit du das selbst nachbauen und live vorführen kannst.

## Tests

- `KnownFailureAllowlistTests`: Parsing ignoriert Kommentare/Leerzeilen, trimmt Whitespace; `Contains` korrekt für gelistete/nicht gelistete Namen; `Empty` enthält nichts.
- `KnownFailureAllowlistLoaderTests` (Temp-Dateien): existierende Datei lädt; fehlende Datei → `Failure`.
- `TestRunVerdictTests`: alle Fehlschläge einer echten Fixture (`lauf-a.trx`, 3 echte Fehlschläge) auf der Allowlist → Grün, `ToleratedFailures.Count == 3`; nur einer von dreien gelistet → bleibt Rot, Grund-Meldung zählt nur die 2 unerwarteten; ganz ohne Allowlist bleibt `ToleratedFailures` leer (Regressionsschutz, bestehende Tests unverändert grün).
- `CliArgumentParserTests`: `--known-failures <Pfad>` setzt `KnownFailuresPath`; ohne Wert wirft; ohne Schalter bleibt `null`.
- `ConsoleReportPrinterTests`: neue Sektion erscheint bei vorhandenen `ToleratedFailures` (und das Urteil bleibt trotzdem ERFOLGREICH), fehlt komplett, wenn keine vorhanden sind.
- Wie immer: `dotnet build` (Clean-Rebuild aller vier Projekte) lief bei mir sauber durch, 0 Fehler/Warnungen; `dotnet test` bewusst nicht — das übernimmst du.
