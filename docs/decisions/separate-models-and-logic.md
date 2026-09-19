# Entscheidung: refactor/separate-models-and-logic

Umsetzung des in `docs/plans/separate-models-and-logic.md` beschriebenen Plans, nach Bestätigung.

## Namensraum wird an die neue Ordnerstruktur angepasst

Deine Entscheidung (Option B aus dem Plan): `Trx/Models/`, `Input/Models/` und `Console/Models/` bekommen jeweils einen eigenen, verschachtelten Namensraum (`TestGuardian.Core.Trx.Models`, `TestGuardian.Core.Input.Models`, `TestGuardian.Console.Models`) statt den bisherigen Namensraum unverändert zu übernehmen. Ordner und Namensraum entsprechen sich damit wieder 1:1 — konsistent mit dem Rest des Projekts (`Trx/` ↔ `TestGuardian.Core.Trx`, `Input/` ↔ `TestGuardian.Core.Input`).

**Konsequenz:** C# durchsucht bei der Namensauflösung automatisch nur die *umschließenden* Namensräume einer Datei (z.B. sieht eine Datei in `TestGuardian.Core.Trx.Models` automatisch alles in `TestGuardian.Core.Trx` darüber) — nicht aber Kind- oder Geschwister-Namensräume. Jede Logik-Datei, die einen der verschobenen Modelltypen beim Namen nennt, brauchte deshalb eine zusätzliche `using`-Zeile: `TestRunAggregator.cs`, `TestRunVerdict.cs`, `TrxBatchReader.cs`, `TrxDocumentParser.cs`, `TrxFileReader.cs`, `TrxInputResolver.cs` in `TestGuardian.Core`; `CliArgumentParser.cs`, `ConsoleReportPrinter.cs`, `Program.cs` in `TestGuardian.Console`; sowie sechs Testdateien (`TestRunVerdictTests.cs`, `ConsoleReportPrinterTests.cs`, `TrxDocumentParserTests.cs`, `TrxBatchReaderTests.cs`, `TestRunAggregatorTests.cs`, `TrxFileReaderTests.cs`). `TrxInputResolverTests.cs` und `CliArgumentParserTests.cs` brauchten keine Änderung — sie benennen die verschobenen Typen nirgends explizit (nur über `var`/Inferenz).

## Zwei Dateien mussten aufgeteilt werden, nicht nur verschoben werden

- **`TestRunVerdict.cs`** enthielt `VerdictReasonKind`, `VerdictReason`, `VerdictSeverity` und `GuardianVerdict` zusammen mit der Auswertungslogik. Die vier Modelltypen wandern gemeinsam in eine neue `Trx/Models/GuardianVerdict.cs` (benannt nach dem Haupttyp, wie bei `TrxReadResult.cs`, das ja auch `TrxReadFailureReason`-Zwischentypen mitträgt); `TestRunVerdict.cs` enthält danach nur noch die statische Klasse.
- **`CliArgumentParser.cs`** enthielt den `CliOptions`-Record zusammen mit der Parser-Klasse. `CliOptions` wandert in eine neue `Console/Models/CliOptions.cs`.

Keine Verhaltensänderung an keiner Stelle — reine Datei-Aufteilung/Verschiebung plus die dadurch nötigen `using`-Ergänzungen.

## `SynchronousProgress<T>` bleibt an der Core-Wurzel

Ist eine kleine Utility-Klasse mit echtem Verhalten (implementiert `IProgress<T>`), kein reines Datenobjekt — gehört fachlich nicht in einen `Models`-Ordner. Da an der Core-Wurzel ohnehin kein Modelltyp daneben liegt, gibt es dort auch kein Vermischungsproblem, das dieses Refactoring lösen sollte.

## Bewusst nicht angefasst

- `TestGuardian.Core.Tests` / `TestGuardian.Console.Tests` behalten ihre bisherige, flache Struktur — deine Anfrage bezog sich explizit auf "die 2 Komponenten" (die beiden Hauptprojekte).
- Keine Verhaltens-/Logikänderung irgendeiner Art.

## Build vs. Testlauf

`dotnet clean` + vollständiger `dotnet build src.sln` (alle vier Projekte, `bin`/`obj` vorher gelöscht, um einen inkrementellen Fehlalarm auszuschließen): 0 Fehler, 0 Warnungen. `dotnet test` wie immer bewusst nicht von mir ausgeführt.
