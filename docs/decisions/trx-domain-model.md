# Entscheidungen: feature/trx-domain-model

Ergänzt `docs/plans/trx-domain-model.md` um Entscheidungen, die erst während der Umsetzung konkret wurden.

## Geschlossene Ergebnis-Hierarchie statt Exceptions

`TrxReadResult` ist ein `abstract record` mit privatem Konstruktor und zwei genesteten `sealed record`-Typen (`Success`, `Failure`). Nur diese beiden können von `TrxReadResult` erben, weil der Basiskonstruktor `private` ist. Damit zwingt der Typ jeden Aufrufer, per Pattern-Matching (`is TrxReadResult.Success`/`is TrxReadResult.Failure`) beide Fälle zu behandeln — es gibt keinen Weg, den Fehlerfall zu vergessen und versehentlich mit einem rohen Erfolgswert weiterzuarbeiten, so wie es der Aufgabenstellung zufolge beim naiven Auswerter passiert ist.

## `TrxDocumentParser.Parse` kennt keine Dateipfade

`Parse(string xmlContent)` ist bewusst rein und weiß nichts von Dateien. Ein vom Parser erzeugtes `TrxReadFailure` hat deshalb `FilePath == null`. `TrxFileReader.Read(string filePath)` hängt den Pfad nachträglich per `with`-Ausdruck an (`failure.Error with { FilePath = filePath }`), statt den Pfad durch die ganze Parsing-Logik durchzureichen. Das hält den Parser unabhängig testbar (siehe `TrxDocumentParserTests`, die ausschließlich mit XML-Strings arbeiten) und die Datei-Fehlerbehandlung an einer einzigen Stelle.

## `RawOutcome` zusätzlich zu `TestOutcome`

Nur `Passed`, `Failed` (inkl. LoadError-Erkennung) und `NotExecuted` werden explizit erkannt. Alles andere (`Inconclusive`, `Timeout`, `Aborted`, unbekannte künftige vstest-Werte, …) landet in `TestOutcome.Other` **plus** einer `RunWarning` mit dem Klartext. Bewusst keine Vermutung, ob ein unbekannter Status eher wie "bestanden" oder "durchgefallen" zu werten ist — lieber sichtbar machen und die Entscheidung offenlassen, als still falsch zu kategorisieren.

## Counters-Abgleich nur auf total/passed/failed

Die `<Counters>` haben deutlich mehr Felder (`error`, `timeout`, `aborted`, `inconclusive`, …). Verglichen werden nur `total`, `passed`, `failed`, weil das die Felder sind, die die Aufgabenstellung und die Beispieldaten tatsächlich adressieren (der Hinweis in `Readme.pdf` bezieht sich genau auf diese Kopfzahlen). Die übrigen Felder in den Vergleich aufzunehmen hätte ohne ein Beispiel, das sie tatsächlich falsch befüllt, nur Rätselraten über ihre Bedeutung bedeutet.

## Kein Sonderfall für I/O-Fehler beim Lesen

`TrxFileReader.Read` fängt nur `File.Exists == false` (→ `FileNotFound`) und leeren Inhalt (→ `EmptyFile`) ab; ein `IOException` beim eigentlichen Lesen (z.B. gesperrte Datei, Berechtigungsproblem) wird **nicht** gefangen und propagiert als echte Exception nach oben. Das sind die drei Pflichtfälle aus der Aufgabenstellung ("Datei fehlt, XML kaputt, Datei leer") — ein zusätzlicher, nicht geforderter vierter Fehlermodus hätte an dieser Stelle nur Scope-Creep bedeutet. Sollte sich das in der Praxis als nötig erweisen, ist das eine bewusste Erweiterung für später, keine vergessene Sonderbehandlung.

## Testprojekt: net8.0 statt net9.0

`dotnet new mstest` hat das Projekt standardmäßig auf `net9.0` gesetzt. Umgestellt auf `net8.0`, um mit `TestGuardian.Core` und `TestGuardian.Console` konsistent zu bleiben — ein gemischtes Target-Framework innerhalb einer so kleinen Solution hätte keinen Vorteil gebracht.

## Ein Test mehr als geplant

Zusätzlich zu den 9 in `docs/plans/trx-domain-model.md` geplanten Tests kam `Read_MalformedFile_AttachesFilePathToParserFailure` dazu — er prüft gezielt das Zusammenspiel zwischen `TrxDocumentParser` (kennt keinen Pfad) und `TrxFileReader` (hängt ihn per `with` an), das im Plan zwar beschrieben, aber noch nicht durch einen eigenen Test abgedeckt war.

## Build vs. Testlauf

Ich habe `dotnet build` ausgeführt, um Kompilierfehler auszuschließen — **nicht** `dotnet test`. Ob die Tests inhaltlich grün oder rot sind, habe ich bewusst nicht selbst geprüft; das obliegt dir.
