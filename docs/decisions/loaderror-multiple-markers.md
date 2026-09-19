# Entscheidung: feature/loaderror-multiple-markers

Umsetzung des in `docs/plans/loaderror-multiple-markers.md` beschriebenen, vom Nutzer direkt vorgegebenen Plans.

## Warum eine Liste statt eines zweiten Sonderfalls in der switch-Anweisung

`LoadFailureMarker` war ein einzelner `const string`. Statt einen zweiten, separaten `case`-Zweig für die neue Formulierung einzuführen (der die Klassifizierungsregel dupliziert hätte), wurde daraus `LoadFailureMarkers` (ein kleines `string[]`) und die bestehende Bedingung prüft jetzt `Any(...)` darüber. Ein Zweig, eine Regel, beliebig viele bekannte Formulierungen — künftige weitere Marker (falls nötig) sind eine Zeile, keine neue Verzweigung.

## Warum kein Versuch, weitere/unbekannte Formulierungen zu raten

Es werden bewusst nur die zwei konkret belegten Formulierungen erkannt (aus `lauf-c.trx` und aus dem echten `vstest.console.exe`-Lauf gegen `NachtlaufDemo.Tests`, siehe `docs/decisions/nachtlauf-demo-skeleton.md`). Eine `Failed`-Meldung, die keinen der beiden Marker enthält, bleibt ein echter Fehlschlag — genau das Prinzip aus `docs/decisions/trx-domain-model.md` ("bewusstes Misstrauen statt Vermutung"): lieber eine dritte, tatsächlich beobachtete Formulierung später gezielt ergänzen, als jetzt spekulativ zu raten, was noch vorkommen könnte.

## Tests

Ein neuer synthetischer Test (`Parse_ExecutionContextLoadFailure_ClassifiesAsLoadError`) deckt die neue Formulierung ab, nach demselben Muster wie die bestehenden Inline-XML-Tests. Der bestehende Test gegen `lauf-c.trx` prüft weiterhin die erste Formulierung unverändert. Wie in [[workflow-testguardian]] Punkt 1 festgehalten: ich habe `dotnet test` dafür nicht selbst ausgeführt — das bestätigst du.
