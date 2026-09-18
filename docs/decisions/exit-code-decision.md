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

## Manuelle Verifikation

Wie in `docs/plans/exit-code-decision.md` festgehalten, führe ich `Program.cs` nicht selbst gegen die `Fixtures/Sample Data/lauf-*.trx`-Dateien aus und werte auch keine Testergebnisse selbst. `dotnet build` für die gesamte `src.sln` lief erfolgreich (0 Fehler, 0 Warnungen, alle vier Projekte inkl. des neuen Testprojekts) — das ist reines Kompilieren, keine Testausführung. `dotnet test` wurde bewusst nicht ausgeführt; die eigentliche Prüfung (Tests laufen lassen, Exit-Codes gegen `lauf-a.trx` bis `lauf-e.trx` kontrollieren) bleibt bei dir.
