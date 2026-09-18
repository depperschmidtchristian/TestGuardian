# Entscheidungen: feature/exit-code-decision

Ergänzt `docs/plans/exit-code-decision.md` um Details, die erst bei der Umsetzung konkret wurden.

## `TestRunVerdict`, `CliArgumentParser`, `ConsoleReportPrinter` 1:1 wie geplant umgesetzt

Alle drei Bausteine entsprechen dem im Plan bereits abgestimmten Code — keine inhaltlichen Abweichungen bei der Entscheidungslogik, dem CLI-Parsing oder der Ausgabe-Struktur.

## `SynchronousProgress<T>` nach `TestGuardian.Core` verschoben

Wie im Plan angekündigt: war zuvor eine private verschachtelte Klasse in `TrxBatchReaderTests.cs`, ist jetzt `TestGuardian.Core/SynchronousProgress.cs` (public, im Wurzel-Namespace `TestGuardian.Core`, nicht `TestGuardian.Core.Trx`, weil sie generisch ist und nichts mit `.trx`-Verarbeitung zu tun hat). `Program.cs` und die Tests nutzen jetzt dieselbe Klasse statt einer Duplizierung.

## Stolperfalle: Namespace `TestGuardian.Console` verdeckt `System.Console`

Nicht im Plan vorhergesehen, beim Bauen aufgefallen: Der Namespace `TestGuardian.Console` (vom Konsolen-Projekt so vorgegeben) enthält als letztes Segment denselben Namen wie der BCL-Typ `System.Console`. Innerhalb dieses Namespace (und auch in jedem darunter verschachtelten, z.B. `TestGuardian.Console.Tests`) löst der unqualifizierte Bezeichner `Console` nicht auf `System.Console` auf, sondern auf den umgebenden Namespace selbst (`TestGuardian.Console` als Namespace-Mitglied von `TestGuardian`) — das ergibt `CS0234`. Das gilt unabhängig davon, ob zusätzlich `using System;` oder sogar ein expliziter `using Console = System.Console;`-Alias vorhanden ist; die Namespace-Mitgliedssuche in der einschließenden Namespace-Deklaration gewinnt in jedem Fall gegen Using-Direktiven auf einer tieferen Ebene. Verifiziert mit einem isolierten Minimalbeispiel (drei Varianten: kein Using, explizites `using System;`, expliziter Alias — alle drei scheitern identisch).

**Konsequenz:** In `ConsoleReportPrinter.cs` und `ConsoleReportPrinterTests.cs` wird `System.Console` überall voll qualifiziert, mit einem kurzen Kommentar an der Klasse, der das begründet. `Program.cs` ist davon nicht betroffen, weil Top-Level-Statements nie in einem expliziten `namespace`-Block liegen (das ist ohnehin sprachlich nicht erlaubt) und deshalb im globalen Namespace kompiliert werden, wo `Console` ganz normal via `using System;` auflöst.

## Neues Test-Projekt `TestGuardian.Console.Tests`

Der Plan sah Tests für `CliArgumentParser` und `ConsoleReportPrinter` vor — beide gehören laut Architektur ins Konsolen-Projekt, nicht in `TestGuardian.Core.Tests`. Da es dafür noch kein Testprojekt gab, wurde `TestGuardian.Console.Tests` neu angelegt (gleicher Zuschnitt wie `TestGuardian.Core.Tests`: MSTest, `ProjectReference` auf das zu testende Projekt — das funktioniert auch für ein `Exe`-Projekt problemlos) und der Solution hinzugefügt (`dotnet sln add`).

**Bewusst ohne `[assembly: Parallelize(MethodLevel)]`**, anders als `TestGuardian.Core.Tests`: Die beiden `ConsoleReportPrinterTests` müssen für die Dauer ihrer Ausführung `System.Console.Out` umleiten (`Console.SetOut`), um die Ausgabe abzufangen — das ist ein prozessweiter, geteilter Zustand. Liefe das parallel zu einem anderen Test, der ebenfalls `Console.Out` umleitet (oder selbst gerade zurücksetzt), wäre das Ergebnis eine Race Condition mit nicht-deterministisch vertauschter/verlorener Ausgabe. Bei nur einer Handvoll Tests in diesem Projekt gibt es keinen Performance-Grund, dieses Risiko einzugehen.

## `TestGuardianCore.cs` bewusst nicht gelöscht

`TestGuardian.Core/TestGuardianCore.cs` (das ursprüngliche Platzhalter-"Hello World") wird von `Program.cs` nach diesem Feature nicht mehr referenziert. Nicht gelöscht, weil Löschungen laut Absprache deine ausdrückliche Bestätigung brauchen — das steht noch aus.

## Nachbesserung nach deinem manuellen Test (2026-09-19)

Zwei Punkte aus deiner Rückmeldung nach dem ersten Ausprobieren:

### 1. Ausgabe unübersichtlich, ROT/GRUEN sollte auch als Farbe sichtbar sein

`ConsoleReportPrinter` gruppiert die Ausgabe jetzt in klar getrennte, mit Leerzeile eingeleitete Abschnitte (Bibliotheken, Trennlinie, Gesamt, dann nur die tatsächlich vorhandenen Abschnitte für Warnungen/nicht lesbare Dateien/nicht aufgelöste Eingaben, zuletzt das Urteil) statt einer einzigen Komma-Zeile pro Bibliothek. Die Urteilszeile bekommt zusätzlich zum Text ("GRUEN"/"ROT" bleibt aus Gründen der Barrierefreiheit und weil bestehende Tests danach suchen) eine echte Konsolenfarbe (`ConsoleColor.Green`/`.Red`) und einen `#`-Balken als bewusste Anspielung auf den titelgebenden "grünen Balken" der Aufgabenstellung.

**Warum in `try`/`catch (IOException)` eingepackt:** `Console.ForegroundColor` wirft `IOException`, wenn keine echte Konsole angehängt ist (z.B. wenn ein Test-Host `stdout` umleitet). Ohne den Fang wäre das genau die Art von zusätzlicher, aber unnötiger Fragilität, die die Ausgabe in solchen Umgebungen zum Absturz bringen könnte, obwohl der eigentliche Report völlig in Ordnung wäre — die Ausgabe fällt in dem Fall einfach auf Klartext ohne Farbe zurück.

### 2. Ein falscher Schalter führte zu einem falschen ROT

Ursache rekonstruiert: Wenn `TestGuardian ... --min-tests 3 echo $LASTEXITCODE` (oder ähnlich) **ohne** `;` als Trenner in einer PowerShell-Zeile eingegeben wird, ist das für PowerShell ein einziger Befehl — `echo` und der (zu diesem Zeitpunkt meist leere oder aus einem vorherigen Befehl stammende) Wert von `$LASTEXITCODE` werden als zusätzliche, unbekannte Positionsargumente an `TestGuardian` durchgereicht. `CliArgumentParser` hat solche unbekannten Tokens bisher stillschweigend als zu prüfende Datei-Eingaben behandelt (`default: inputs.Add(...)`) — die konnten dann natürlich nicht aufgelöst werden und lösten über `UnresolvedInputs` (bewusst symmetrisch zu `LoadErrors`, siehe oben) ein ROT aus, obwohl die eigentlich gemeinte `.trx`-Datei tadellos grün war.

**Das ist kein Fehlverhalten der schon abgestimmten `UnresolvedInputs`-Regel** — die soll ja genau verhindern, dass eine nicht auffindbare Eingabe stillschweigend ignoriert wird (dieselbe Logik, die einen echten Tippfehler in einem CI-Skript fangen würde). Ein einzelnes Wort ohne Bindestriche (wie `echo`) ist für den Parser grundsätzlich nicht von einem echten, gemeinten Dateinamen zu unterscheiden — das bleibt bewusst so.

**Was tatsächlich behoben wurde:** Tokens, die mit `--` beginnen, aber zu keinem bekannten Schalter passen (z.B. ein Tippfehler wie `--min-tets`), lösten vorher denselben stillschweigenden Rutsch in `UnresolvedInputs` aus — obwohl klar erkennbar ist, dass es sich um einen missglückten Schalter und nicht um einen Dateinamen handelt. `CliArgumentParser` wirft dafür jetzt sofort eine `ArgumentException` mit klarer Meldung, bevor überhaupt eine Eingabe-Auflösung versucht wird. `Program.cs` fängt das ab und gibt "Fehlerhafter Aufruf: ..." auf `Console.Error` aus statt eines rohen Stacktraces — bewusst getrennt von der eigentlichen `URTEIL: ROT/GRUEN`-Ausgabe, damit ein missglückter Aufruf nie mit einem echten (roten) Testergebnis verwechselt werden kann.

## Manuelle Verifikation

Wie in `docs/plans/exit-code-decision.md` festgehalten, führe ich `Program.cs` nicht selbst gegen die `Fixtures/Sample Data/lauf-*.trx`-Dateien aus und werte auch keine Testergebnisse selbst. `dotnet build` für die gesamte `src.sln` lief erfolgreich (0 Fehler, 0 Warnungen, alle vier Projekte inkl. des neuen Testprojekts) — das ist reines Kompilieren, keine Testausführung. `dotnet test` wurde bewusst nicht ausgeführt; die eigentliche Prüfung (Tests laufen lassen, Exit-Codes gegen `lauf-a.trx` bis `lauf-e.trx` kontrollieren) bleibt bei dir.
