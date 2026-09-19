# Plan: feature/loaderror-multiple-markers

Kleine, präzise umrissene Erweiterung von `TrxDocumentParser` (Teil A): `LoadError` soll nicht nur bei der Meldung aus den Beispieldaten (`lauf-c.trx`) erkannt werden, sondern auch bei der Meldung, die ein echter `vstest.console.exe`-Lauf gegen `NachtlaufDemo.Tests` (Teil B) tatsächlich erzeugt, wenn eine Abhängigkeits-DLL fehlt.

## Anlass

Beim Nachstellen des Ladefehlers in `NachtlaufDemo.Tests` (siehe `docs/decisions/nachtlauf-demo-skeleton.md`, Abschnitt "Erweiterung: MathModule...") kam heraus, dass `vstest.console.exe` in diesem Fall **nicht** `"Failed to load the test assembly or its dependencies"` meldet, sondern pro Test einzeln `"Failed to set up the execution context to run the test"`. Beide Meldungen beschreiben denselben grundsätzlichen Fall (der Testkörper lief nie, weil die Bibliothek/ihre Abhängigkeiten nicht geladen werden konnten) und müssen beide als `LoadError` klassifiziert werden, nicht als echter Fehlschlag.

## Änderung

In `TestGuardian.Core/Trx/TrxDocumentParser.cs`:

- `LoadFailureMarker` (einzelner `const string`) wird zu einer kleinen, schreibgeschützten Liste `LoadFailureMarkers` mit beiden bekannten Formulierungen.
- Die Klassifizierungsregel in `ClassifyOutcome` prüft `errorMessage` gegen **jeden** Marker (`Any(...)`), statt nur gegen einen.

Keine Änderung an der Signatur, am Domänenmodell oder an anderen Klassifizierungsregeln.

## Tests

Neuer synthetischer Testfall (Muster wie `Parse_CountersMismatch_AddsWarning`: Inline-XML statt neuer Fixture-Datei, da es sich um einen einzelnen, klar abgegrenzten Zusatzfall handelt): ein `outcome="Failed"` mit der Meldung `"Failed to set up the execution context to run the test"` muss als `TestOutcome.LoadError` klassifiziert werden, nicht als `Failed`. Der bestehende Test gegen `lauf-c.trx` (erste Formulierung) bleibt unverändert grün.

## Bewusst nicht Teil dieser Änderung

- Keine Suche nach weiteren, noch unbekannten Formulierungen — nur die zwei jetzt konkret belegten.
- Keine Änderung an der generellen "Failed, aber kein bekannter Marker → echter Fehlschlag"-Regel; unbekannte Ladefehler-Formulierungen fallen weiterhin (bewusst) unter `Failed`, nicht unter eine geratene `LoadError`-Vermutung.
