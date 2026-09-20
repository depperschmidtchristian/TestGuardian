# Entscheidung: feature/baseline-comparison

Umsetzung des in `docs/plans/baseline-comparison.md` beschriebenen Plans, nach Bestätigung.

## `--baseline` statt `test`/`compare`-Subcommands

Dein ursprünglicher Vorschlag (zwei Aufrufmodi, `TestGuardian test ...` / `TestGuardian compare ...`) wurde bewusst verworfen — mit 10 Stunden bis zur Abgabe wäre das ein Breaking Change am CLI-Interface gewesen (jeder bestehende Aufruf hätte sich geändert), hätte `CliArgumentParser` grundlegend umgebaut und die noch zu schreibende README verkompliziert. `--baseline <Pfad>` als reiner Zusatzschalter auf dem unveränderten Aufruf erreicht dieselbe Kernfrage ("war das schon rot?") mit einem Bruchteil des Diffs. Einzige Einschränkung: zwei bereits existierende JSON-Dateien ohne frischen Testlauf zu vergleichen geht damit nicht — bewusst nicht Teil dieses Features (siehe Plan).

## Laden der Baseline: Ergebnistyp statt Exception, aber trotzdem ein Ladevorgang statt zweistufigem Check

Bei `--to-json` galt Zweistufigkeit (Check ohne Öffnen, dann `FileMode.CreateNew`), weil Schreiben gefährlich ist (Datenverlust). Beim Laden einer Baseline ist Lesen ungefährlich — deshalb reicht ein einziger `BaselineReportLoader.Load`-Aufruf. Der gibt trotzdem `BaselineLoadResult` (Success/Failure) statt zu werfen, konsistent mit `TrxReadResult`: eine fehlende oder kaputte Baseline-Datei ist ein erwarteter Fall, keine Ausnahme. `Program.cs` übersetzt eine `Failure` in eine `ArgumentException`, die über denselben WARNUNG-Pfad läuft wie jeder andere CLI-Fehler.

## Wiederverwendung von `JsonReportWriter.Options`

`BaselineReportLoader` deserialisiert mit exakt denselben `JsonSerializerOptions` (camelCase, `JsonStringEnumConverter`), mit denen `JsonReportWriter` serialisiert — dafür wurde das Feld von `private` auf `internal` angehoben, keine zweite, potenziell abweichende Options-Instanz. Ein früherer, eigener Bug (fehlende `PropertyNamingPolicy`, siehe `docs/decisions/json-output.md`) hätte sich bei zwei unabhängigen Options-Objekten wiederholen können.

## `GeneratedAt` wird vom Aufrufer gesetzt, nicht vom Writer

`Program.cs` übergibt `DateTimeOffset.Now` explizit an `new JsonReport(...)`, statt dass `JsonReportWriter.Write` selbst die Uhrzeit liest. Hält den Writer weiterhin rein input-basiert und ohne Seiteneffekt außer dem eigentlichen Schreiben — leichter zu testen (siehe `JsonReportWriterTests`, die jetzt einen festen Zeitstempel durchreichen und exakt zurückprüfen, statt "irgendeine Zeit nach Testbeginn" tolerieren zu müssen).

## Vergleichslogik: nur aktuell rote Tests, Abgleich per Assembly+Testname

`BaselineComparer.Compare` beantwortet ausschließlich "ist dieser *aktuell rote* Test neu oder bekannt rot" plus "was wurde seither behoben" — kein vollständiges Test-Set-Diffing (neue/umbenannte/entfernte Tests werden nicht gesondert erkannt). Ein aktuell roter Test ohne Gegenstück in der Baseline (z.B. ein neuer Test) zählt automatisch als `NewlyFailed`, was inhaltlich korrekt ist: er war in der Baseline nicht bekanntermaßen rot. `LoadError` zählt für diesen Vergleich genauso als "rot" wie `Failed` — beide sind aus Sicht "war das schon ein Problem" gleichwertig, auch wenn `TestRunVerdict` sie für die Rot/Gelb/Grün-Einstufung unterschiedlich behandelt (Ladefehler sind dort immer Rot, Testfehlschläge auch — kein Unterschied an dieser Stelle, aber die Trennung composed sauber, falls sich das mal ändert).

## Verdikt/Exit-Code bleiben unverändert

`BaselineComparer.Compare` wird komplett unabhängig von `TestRunVerdict.Evaluate` aufgerufen und fließt nirgends in dessen Berechnung ein. Ein "schon gestern roter" Test ist heute immer noch rot und muss weiterhin als Rot gemeldet werden — das Feature beantwortet nur "ist das neu", nicht "ist das schlimm". Exit-Code bleibt exakt wie zuvor: `verdict.IsSuccessful ? 0 : 1`.

## Tests

- `BaselineComparerTests` (Core.Tests): je ein Fall für "aktuell rot + in Baseline rot", "aktuell rot + in Baseline grün", "aktuell rot ohne Baseline-Gegenstück", "in Baseline rot + jetzt grün (behoben)", plus einen Fall, der bestätigt, dass `LoadError` genauso wie `Failed` behandelt wird, und einen, der den durchgereichten Zeitstempel prüft.
- `BaselineReportLoaderTests` (Core.Tests, Temp-Dateien): eine mit `JsonReportWriter` tatsächlich geschriebene Datei lädt erfolgreich und roundtripped Zeitstempel und Testergebnis; fehlende Datei → `Failure`; kaputtes JSON → `Failure` statt Exception.
- `CliArgumentParserTests`: `--baseline <Pfad>` setzt `BaselinePath`; ohne Wert wirft (kein Default-Mechanismus wie bei `--to-json`, siehe Plan); ohne den Schalter bleibt `BaselinePath` `null`.
- `ConsoleReportPrinterTests`: neuer Fall prüft, dass alle drei Kategorien (neu rot/bereits rot/behoben) in der Ausgabe auftauchen, wenn eine `BaselineComparison` übergeben wird; ein zweiter Fall prüft, dass die Baseline-Sektion komplett fehlt, wenn keine übergeben wird.
- Wie immer: `dotnet build` (Clean-Rebuild aller vier Projekte) lief bei mir sauber durch, 0 Fehler/Warnungen; `dotnet test` bewusst nicht — das übernimmst du.
