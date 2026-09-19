# Entscheidung: feature/json-output

Umsetzung des in `docs/plans/json-output.md` beschriebenen Plans, inklusive der kritischen Nachbesserung zum Nie-Überschreiben.

## Zwei getrennte Sicherheitsebenen gegen Datenverlust, nicht nur eine

`JsonOutputPathValidator.EnsureCanCreate` (Console, läuft ganz am Anfang, direkt nach dem CLI-Parsing) prüft nur `File.Exists`/`Directory.Exists` — öffnet die Zieldatei nie. Das ist bewusst *nicht* die eigentliche Garantie, sondern nur eine schnelle, freundliche Rückmeldung (WARNUNG-Balken statt rohem Absturz), bevor der ganze restliche Lauf überhaupt losgeht.

Die eigentliche, harte Garantie sitzt in `JsonReportWriter.Write` (Core): `FileMode.CreateNew` schlägt atomar fehl, wenn die Datei doch existiert — unabhängig davon, ob der frühe Check sie schon für nicht-existent befunden hatte. Damit gibt es keine Lücke zwischen "geprüft" und "geschrieben" (TOCTOU), in der eine zwischenzeitlich angelegte Datei stillschweigend überschrieben werden könnte. Beide Ebenen sind nötig: die erste für gute UX (klare Meldung, kein Programmstart auf Verdacht), die zweite als tatsächliche, nicht umgehbare Garantie.

## Warum der TOCTOU-Fall am Ende nicht als roher Stacktrace endet

Ursprünglich offen gelassen (siehe Plan), jetzt entschieden: `Program.cs` fängt `IOException` gezielt um den finalen `JsonReportWriter.Write`-Aufruf ab, gibt eine klare Meldung auf `Console.Error` aus und beendet mit `return 1` — unabhängig vom sonstigen Testverdikt. Konsistent mit jeder anderen I/O-Fehlerstelle im Projekt (`TrxFileReader`, `CliArgumentParser`, `JsonOutputPathValidator`): kein unbehandelter Absturz, aber auch kein stiller Erfolg, wenn das explizit angeforderte Ergebnis (die JSON-Datei) tatsächlich nicht entstanden ist.

## Warum `JsonReport` das volle Bild trägt, nicht nur `TestRunOverview`

Nach Rückfrage bestätigt: "Overview" meinte den gesamten Output. `JsonReport` bündelt `Overview`, `InputResolution` und `Verdict` — exakt dieselben drei Werte, die `ConsoleReportPrinter.Print` als Parameter bekommt. Ohne das Verdikt müsste ein Konsument der JSON-Datei die Rot/Gelb/Grün-Klassifizierungsregeln selbst nachbauen, nur um zu wissen, ob dem Lauf zu trauen ist — das widerspräche dem eigentlichen Zweck des Wächters.

## Warum kein paralleles DTO-Modell

`System.Text.Json` serialisiert (schreibt) Records rein property-basiert — das für Records bei *Deserialisierung* nötige Konstruktor-Matching betrifft nur das Einlesen, das hier nie passiert (`--to-json` ist eine reine Ausgabe, nichts wird je wieder eingelesen). Die Domain-Records (`TestRunOverview`, `GuardianVerdict`, `InputResolutionResult`, …) werden deshalb direkt serialisiert. Nebeneffekt: berechnete Properties wie `AssemblySummary.Total`/`.CorrectlyExecuted` oder `GuardianVerdict.IsSuccessful` erscheinen automatisch mit im JSON, ohne dass sie irgendwo verdoppelt werden müssten.

## Enums als Strings

`JsonSerializerOptions` mit `JsonStringEnumConverter`: `"severity": "Red"` statt `"severity": 2`. Robuster für Weiterverarbeitung (ein CI-Skript, das `severity == "Red"` prüft, bricht nicht, falls die Enum-Reihenfolge sich mal ändert).

## Warum die Schreibbarkeits-Prüfung in `TestGuardian.Console`, nicht in `TestGuardian.Core`

`JsonOutputPathValidator` ist reine CLI-Vorab-Validierung (wie `CliArgumentParser` selbst) — sie kennt gar keinen `JsonReport`, keine Testdaten, nichts Fachliches. Nur der eigentliche Schreibvorgang (`JsonReportWriter`, der den fertigen `JsonReport` kennt) gehört fachlich zu Core, analog zu `TrxFileReader`.

## Tests

- `CliArgumentParserTests`: `--to-json <Pfad>` setzt `JsonOutputPath`; ohne Wert wirft; ohne den Schalter bleibt `JsonOutputPath` `null`.
- `JsonOutputPathValidatorTests` (Temp-Verzeichnisse wie `TrxInputResolverTests`): schreibbarer, noch nicht belegter Pfad wirft nicht; **bereits existierende Datei wirft, Inhalt bleibt exakt erhalten** (der Kernfall dieses Features); nicht existierendes Zielverzeichnis wirft.
- `JsonReportWriterTests` (Core.Tests): geschriebene Datei per `JsonDocument.Parse` inhaltlich geprüft (Assemblies, `severity` als String, `unresolvedInputs`) statt exaktem String-Vergleich; zusätzlich ein Test, der `Write` gegen einen bereits existierenden Pfad aufruft und sowohl die geworfene `IOException` als auch den unveränderten Ursprungsinhalt prüft.
- Wie immer: `dotnet build` (Clean-Rebuild aller vier Projekte) lief bei mir sauber durch, 0 Fehler/Warnungen; `dotnet test` bewusst nicht — das übernimmst du.
