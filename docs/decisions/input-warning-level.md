# Entscheidung: feature/input-warning-level

Umsetzung des in `docs/plans/input-warning-level.md` beschriebenen Plans, nach deiner Rückmeldung zu den drei offenen Fragen ("Kaputte Dateien bleiben Rot, CLI-Fehler auch gelb, Exit-Code kann bei 1 bleiben.").

## Warum `GuardianVerdict.Severity` statt eines zweiten `bool`-Flags

Ein zweites Flag (z.B. `IsInputWarning`) neben `IsSuccessful` hätte vier statt drei erreichbare Zustände erlaubt (`(true, true)` wäre unsinnig) und hätte an jeder Verzweigungsstelle beide Flags konsistent gehalten werden müssen. Ein `VerdictSeverity`-Enum (`Green < Yellow < Red`) macht "genau eine von drei Stufen" strukturell unmöglich zu verletzen und die Vorrangregel wird zu einem einzeiligen `Reasons.Max(...)` über die Enum-Ordinalwerte — kein manuelles Prioritäts-if nötig. `IsSuccessful` bleibt als berechnete Property erhalten (`Severity == Green`), damit der Exit-Code-Aufrufer in `Program.cs` unverändert bleibt.

## Warum `UnreadableFiles` bei Rot bleibt

Deine Entscheidung, entgegen meiner Tendenz im Plan: eine gefundene, aber inhaltlich kaputte Datei (abgeschnittenes XML, 0 Byte) ist nicht eindeutig genug ein bloßer Bedien-/Tippfehler — sie kann genauso gut ein echtes Symptom eines kaputten Testlaufs sein (Prozess stürzt beim Schreiben der `.trx` ab). Im Zweifel die strengere Einstufung. Nur `UnresolvedInputs` (Pfad/Filter trifft nichts) gilt als eindeutiges Eingabeproblem und wird Gelb.

## Warum `PrintUsageError` denselben `PrintBanner`-Helper nutzt wie `PrintVerdict`

Der CLI-Parse-Fehlerpfad in `Program.cs` (unbekannte Option, z.B. Tippfehler `--minTests`) läuft nie durch `TestRunVerdict.Evaluate` — er hat gar keinen `GuardianVerdict`, weil das Programm abbricht, bevor überhaupt eine `.trx`-Datei gelesen wird. Trotzdem soll er optisch identisch zum gelben Warnungs-Balken aussehen. Statt die Balken-Logik zu duplizieren, wurde sie aus `PrintVerdict` in eine private `PrintBanner(label, color, messages)`-Methode extrahiert, die beide Aufrufer teilen — eine Formatierungsregel für "Balken + Zeilen + Trennlinie", nicht zwei beinahe identische Kopien.

Nebenbei: die Ausgabe wandert dabei von `Console.Error` (bisher) nach `Console.Out` (jetzt, wie der Rest des Reports) — bewusst, damit sie sich optisch/strömungstechnisch wirklich wie ein Teil desselben Reports verhält und nicht wie eine separate Fehlerausgabe. Der Exit-Code (`1`) ändert sich dadurch nicht.

## Warum kein eigener Exit-Code für Gelb

Deine Entscheidung: `1` bleibt für Rot und Gelb gleich. Die Pflichtanforderung verlangt nur "≠ 0 bei kaputter Eingabe/Testlauf", kein CI-Skript im Rahmen dieser Aufgabe braucht die Unterscheidung automatisiert — ein zweiter Code (`2`) wäre spekulative Vorbereitung auf einen nicht belegten Bedarf.

## Nachbesserung: `ZeroTestsExecuted` nur bei mindestens einer aufgelösten Datei

Ausgelöst durch deine Rückfrage zu `TestGuardian *.trx --max-depth 5` (ausgeführt in einem Ordner ohne `.trx` direkt darin, nur in Unterordnern): `*.trx` wird von `TrxInputResolver` als Suchmuster behandelt (nicht als Ordner), und `--max-depth` gilt laut `docs/plans/multi-file-input.md` bewusst **nicht** für Suchmuster — das Muster trifft 0 Dateien → `UnresolvedInputs` (Gelb). Weil dadurch aber gar keine Datei gelesen wurde, war `overview.Total.CorrectlyExecuted` ebenfalls 0, was zusätzlich den Rot-Grund `ZeroTestsExecuted` auslöste. Per Vorrangregel gewann Rot — URTEIL: ROT statt der erwarteten WARNUNG, obwohl die einzige tatsächliche Ursache ein Eingabeproblem war.

Das war eine unbeabsichtigte Doppel-Meldung derselben Ursache: einmal als `UnresolvedInputs` (korrekt), einmal als `ZeroTestsExecuted` (irreführend, weil es suggeriert, ein echter Testlauf hätte 0 Ergebnisse geliefert). `TestRunVerdict.Evaluate` prüft jetzt zusätzlich `inputResolution.ResolvedFilePaths.Count > 0`, bevor `ZeroTestsExecuted` hinzugefügt wird:

- **Mindestens eine Datei wurde gelesen, Ergebnis trotzdem 0 Tests** (der eigentliche "lügende grüne Balken", z.B. `lauf-b.trx`: Filter trifft nichts, der Lauf selbst meldet 0) → bleibt Rot, unverändert.
- **Gar keine Datei wurde aufgelöst** (wie im `*.trx`-Fall) → `ZeroTestsExecuted` entfällt, `UnresolvedInputs` bleibt als einziger, zutreffender Grund stehen → WARNUNG.

Bewusst **nicht** angefasst: `BelowMinimumTestCount` (Schalter `--min-tests`) hat dieselbe Doppel-Meldungs-Anfälligkeit (weniger als `n` Tests liefen, weil nichts aufgelöst wurde), wurde aber nicht Teil dieser Entscheidung — eigene Ermessensfrage, die noch offen ist, falls sie konkret auftritt.

## Nachbesserung 2: `TestGuardian` ganz ohne Argumente meldete GRUEN

Von dir gefunden: `TestGuardian` ganz ohne jede Eingabe (egal in welchem Ordner ausgeführt) meldete immer GRUEN, obwohl 0 Tests liefen. Ursache: `TrxInputResolver.Resolve` iteriert über die übergebenen `rawInputs` — bei einer leeren Liste läuft diese Schleife einfach gar nicht. Damit bleiben sowohl `ResolvedFilePaths` **als auch** `UnresolvedInputs` leer, und `TestRunVerdict.Evaluate` hat buchstäblich keinen Grund, den es hinzufügen könnte — auch nicht `ZeroTestsExecuted`, seit der Nachbesserung oben genau dafür einen `ResolvedFilePaths.Count > 0`-Wächter verlangt.

Wichtig: Dieser Fall war auch **vorher** schon falsch eingeordnet, nur unauffälliger — mit der alten, unbedingten `ZeroTestsExecuted`-Prüfung wäre er ROT gewesen, aber aus dem falschen Grund (suggeriert einen echten Testfehlschlag, obwohl schlicht keine Eingabe angegeben wurde). "Keine Eingabe angegeben" ist inhaltlich dieselbe Kategorie wie eine unbekannte Option oder ein `--min-tests` ohne Wert — ein reiner Bedienfehler, kein Testergebnis. Deshalb: **`CliArgumentParser.Parse` wirft jetzt eine `ArgumentException`, wenn nach dem Parsen keine positionalen Eingaben übrig sind** — läuft über denselben, bereits etablierten `catch (ArgumentException)`-Pfad in `Program.cs` wie eine unbekannte Option, also `ConsoleReportPrinter.PrintUsageError` (WARNUNG-Balken) statt eines `GuardianVerdict`.

Das geht über die ursprüngliche Abgrenzung im Plan hinaus ("Keine Änderung an der CLI-Parsing-Logik selbst") — bewusst, weil sich erst beim manuellen Testen zeigte, dass die reine Darstellungs-Umkategorisierung diesen Fall nicht abdeckt; die Alternative (noch ein Sonderfall in `TestRunVerdict`) hätte dieselbe Fehlerklasse an zwei Stellen behandelt, obwohl `CliArgumentParser` bereits exakt dafür da ist.

## Tests

- `TestRunVerdictTests`: bestehender `UnreadableFiles`-Test um eine explizite `Severity == Red`-Prüfung ergänzt; `UnresolvedInputPresent`-Test umbenannt/erweitert auf `Severity == Yellow`; neuer Test `Evaluate_UnresolvedInputAndRealFailureCoexist_SeverityIsRed` für die Vorrangregel (Rot gewinnt, Gelb-Grund bleibt trotzdem in `Reasons`); `Evaluate_ZeroCorrectlyExecuted_IsUnsuccessfulWithZeroTestsReason` bekommt jetzt eine echte `InputResolutionResult` mit der tatsächlich gelesenen Datei (statt der bisher zweckentfremdeten `NoUnresolvedInputs`-Platzhalter-Konstante, die `ResolvedFilePaths` immer leer ließ); neuer Test `Evaluate_NothingResolvedAtAll_ZeroTestsIsNotAddedOnTopOfUnresolvedInputs` deckt genau deinen `*.trx`-Fall ab.
- `CliArgumentParserTests`: neue Tests `Parse_NoInputsAtAll_Throws` und `Parse_OnlyOptionsNoPositionalInputs_Throws` für die neue Validierung.
- `ConsoleReportPrinterTests`: neuer Fall für `VerdictSeverity.Yellow` (Ausgabe enthält "WARNUNG", nicht "URTEIL: ROT"/"URTEIL: GRUEN") sowie ein neuer Fall für `PrintUsageError`. Der bestehende "FailedRun"-Test wurde von `UnresolvedInputs` (jetzt Gelb) auf `RealTestFailures` (Rot) umgestellt, damit er weiterhin tatsächlich den Rot-Pfad prüft.
- Wie immer per [[workflow-testguardian]] Punkt 1: `dotnet build` habe ich selbst laufen lassen (reiner Kompilierfehler-Check, keine Testaussage), `dotnet test` bewusst nicht — das bestätigst du.
