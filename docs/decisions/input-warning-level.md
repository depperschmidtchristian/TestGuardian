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

## Tests

- `TestRunVerdictTests`: bestehender `UnreadableFiles`-Test um eine explizite `Severity == Red`-Prüfung ergänzt; `UnresolvedInputPresent`-Test umbenannt/erweitert auf `Severity == Yellow`; neuer Test `Evaluate_UnresolvedInputAndRealFailureCoexist_SeverityIsRed` für die Vorrangregel (Rot gewinnt, Gelb-Grund bleibt trotzdem in `Reasons`).
- `ConsoleReportPrinterTests`: neuer Fall für `VerdictSeverity.Yellow` (Ausgabe enthält "WARNUNG", nicht "URTEIL: ROT"/"URTEIL: GRUEN") sowie ein neuer Fall für `PrintUsageError`. Der bestehende "FailedRun"-Test wurde von `UnresolvedInputs` (jetzt Gelb) auf `RealTestFailures` (Rot) umgestellt, damit er weiterhin tatsächlich den Rot-Pfad prüft.
- Wie immer per [[workflow-testguardian]] Punkt 1: `dotnet build` habe ich selbst laufen lassen (reiner Kompilierfehler-Check, keine Testaussage), `dotnet test` bewusst nicht — das bestätigst du.
