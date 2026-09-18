# Plan: Teil A – Der Wächter (C#)

Dieser Plan beschreibt den groben Rahmen für Teil A der Aufgabe (siehe `Fixtures/docs/Bewerberaufgabe-Testwaechter.pdf`). Er wird in vier Feature-Branches umgesetzt; jeder einzelne bekommt vor seiner Umsetzung noch einen eigenen, engeren Plan unter `docs/plans/`, sobald dieser grobe Rahmen bestätigt ist.

## Ziel

Ein Werkzeug, das `.trx`-Dateien einliest und verlässlich entscheidet, ob ein Testlauf wirklich als "grün" gelten darf — inklusive der Fälle, die ein naiver Auswerter übersieht (0 ausgeführte Tests, Ladefehler die wie Testfehlschläge aussehen, kaputte Eingabedateien).

## Architekturüberblick

- **`TestGuardian.Core`**: Domänenmodell, Parsing, Klassifizierung, Aggregation, Entscheidungslogik. Keine Konsolen-/Ausgabe-Concerns außer dem reinen Dateizugriff zum Einlesen.
- **`TestGuardian.Console`**: CLI-Einstiegspunkt, Argument-Parsing (`--min-tests` etc.), Ausgabeformatierung, Exit-Code.

Begründung: Trennung erlaubt, die Kernlogik unabhängig von der Konsole zu testen, und hält die Entscheidungsregeln (was zählt als "rot") an einer Stelle statt verstreut in der CLI.

## Domänenmodell (Kern)

- `TestOutcome`-Enum: `Passed`, `Failed`, `LoadError`, `Skipped`, `Other` (Auffangkategorie für unbekannte vstest-Outcomes — bewusst nicht stillschweigend als "Passed" oder "Failed" einsortiert).
- `TestCaseResult`: Assembly (aus `storage`-Attribut), qualifizierter Testname (`className.testName`), `TestOutcome`, optionale Fehlermeldung.
- `TestRunResult`: Ergebnis eines geparsten `.trx` — Laufname, Liste von `TestCaseResult`, die deklarierten `<Counters>`-Werte (nur zu Referenz-/Warnzwecken), etwaige `RunInfo`-Warnungen aus der Datei.
- `TrxReadResult`: Discriminated Result (Erfolg mit `TestRunResult`, oder Fehlschlag mit Grund: Datei fehlt, XML kaputt, Datei leer). Kein Ausnahmefall wird stillschweigend verschluckt.

## Klassifizierungsregeln

- `outcome="Passed"` → `Passed`.
- `outcome="Failed"` **und** Fehlermeldung enthält `"Failed to load the test assembly or its dependencies"` → `LoadError` (kein echter Testfehlschlag – der Testkörper lief nie).
- `outcome="Failed"` (sonst) → `Failed`.
- `outcome` in `{NotExecuted, Skipped, ...}` → `Skipped`.
- alles andere → `Other`, sichtbar ausgewiesen statt versteckt.

## Umgang mit den Kopf-Zahlen (`<Counters>`)

Laut Hinweis im `Readme.pdf` der Beispieldaten: Die deklarierten `<Counters>` sind nicht die Quelle der Wahrheit. Maßgeblich ist die tatsächliche Liste der `<UnitTestResult>`-Elemente. Weichen deklarierte und errechnete Zahlen voneinander ab, wird das als sichtbare Warnung ausgegeben (siehe `lauf-e.trx`, wo `Counters total="5"` nicht zu den tatsächlichen Testdefinitionen passt).

## Sonderfälle (Pflicht laut Aufgabe)

| Fall | Verhalten |
|---|---|
| Datei fehlt | Expliziter Fehlerfall pro Datei, kein stiller Erfolg |
| XML kaputt / unparsbar (z.B. `lauf-e.trx`) | Gleich behandelt wie "Datei fehlt" |
| Datei leer (0 Bytes) | Gleich behandelt |
| 0 Tests ausgeführt, aber `outcome="Completed"` (der Kernfall, `lauf-b.trx`) | Wird unabhängig vom deklarierten `<ResultSummary outcome>` erkannt — die Bedingung ist schlicht `executed == 0` |

Eine einzelne kaputte Datei innerhalb eines Mehrdatei-Laufs soll nicht den gesamten Durchlauf zum Absturz bringen, sondern als eigener, sichtbarer Fehlerfall in der Übersicht auftauchen und am Ende zum Gesamt-Fehlschlag beitragen.

## Aggregation

- Pro Bibliothek (Assembly): total, passed, failed, loadError, skipped.
- Gesamt über alle eingelesenen Dateien.
- Ausgabe: lesbare Tabelle in der Konsole (Pflicht). JSON-Export ist Kür und würde als eigenes, späteres Feature kommen.

## Eingabe-Auflösung

Einzeldatei, Ordner oder Suchmuster. Bei Ordner-Eingabe wird standardmäßig **rekursiv** in Unterordner nach `.trx`-Dateien gesucht; ein Schalter (z.B. `--recursive` / `--no-recursive`) erlaubt es, das explizit umzuschalten.

## LoadError-Zählung (Entscheidung, mit Beispiel)

Beispiel: Bibliothek A liefert 3 echte `Passed`-Tests, Bibliothek B kann nicht geladen werden und vstest trägt dafür 9 `Failed`-Einträge ein (einen pro dort definiertem Test, Meldung "Failed to load the test assembly...", wie in `lauf-c.trx`).

- **LoadErrors zählen nicht zur "ausgeführt"-Zahl.** Sie fließen weder in die 0-Tests-Regel noch in `--min-tests` als "ausgeführt" ein. In obigem Beispiel ist "ausgeführt" = 3, nicht 12. Damit scheitert der Lauf bei `--min-tests 10` oder `--min-tests 12` (3 < 10 bzw. 3 < 12), ist aber über diese Regel erfolgreich, sobald `--min-tests` < 3 gesetzt ist (bestätigt anhand des Nutzerbeispiels).
- **Das bloße Vorhandensein mindestens eines LoadErrors löst zusätzlich, unabhängig von `--min-tests`, für sich allein einen Fehlschlag aus** — auch ganz ohne gesetzten `--min-tests`-Schalter. Eine nicht ladbare Bibliothek bedeutet ungeprüften Code; das darf nicht als "grün" durchgehen, selbst wenn andere Bibliotheken sauber liefen. Dieser Fall wird mit einer eigenen, von "X Tests fehlgeschlagen" getrennten Meldung ausgewiesen (z.B. "1 Bibliothek konnte nicht geladen werden"), damit echte Fehlschläge und Ladefehler nicht in einen Topf geworfen werden.

## Entscheidungslogik (Exit Code)

Exit-Code ≠ 0, wenn eine der folgenden Bedingungen zutrifft:

1. mindestens ein echter Testfehlschlag (`Failed`, nicht `LoadError`) liegt vor, ODER
2. mindestens ein `LoadError` liegt vor (eigene Meldung, siehe oben), ODER
3. die Gesamtzahl echter ausgeführter Tests (ohne LoadErrors) ist 0, ODER
4. weniger echte Tests liefen als über `--min-tests <n>` erwartet (Summe über alle eingelesenen Dateien), ODER
5. mindestens eine Datei konnte nicht gelesen werden (Fehlerfall: fehlt, kaputtes XML, leer).

## Geplante Feature-Branches (Umsetzungsreihenfolge)

1. `feature/trx-domain-model` — Domänenmodell + Parser + Klassifizierung + Sonderfälle für eine einzelne Datei.
2. `feature/trx-aggregation-summary` — Mehrere geparste Läufe zu einer Übersicht je Bibliothek + gesamt zusammenführen.
3. `feature/multi-file-input` — Eingabe auflösen: Datei / Ordner / Suchmuster → Liste von `.trx`-Pfaden.
4. `feature/exit-code-decision` — Entscheidungslogik + `--min-tests` + Verdrahtung in `Program.cs`.

## Entscheidungen (bestätigt, 2026-09-18)

1. **Reine Ladefehler**: Lösen unabhängig von allem anderen immer einen Fehlschlag aus (siehe oben) — auch ohne gesetzten `--min-tests`.
2. **Ordner-Eingabe**: Standardmäßig rekursiv, per Schalter umschaltbar.
3. **`--min-tests` Bezugsgröße**: Gesamtzahl echter ausgeführter Tests über alle eingelesenen Dateien zusammen.
4. **Zählweise von LoadErrors**: Werden bei "ausgeführten Tests" herausgerechnet (zählen nicht mit).
