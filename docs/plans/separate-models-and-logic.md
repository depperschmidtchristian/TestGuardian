# Plan: refactor/separate-models-and-logic

Ziel: innerhalb der zwei Komponenten `TestGuardian.Core` und `TestGuardian.Console` eine Ordnerstruktur, die reine Datentypen ("Objekte": records/enums ohne nennenswertes Verhalten) von Implementierungsdateien (statische Klassen mit tatsächlicher Logik, Einstiegspunkt) trennt — beide liegen aktuell in denselben Ordnern (`Trx/`, `Input/`, Console-Projektwurzel) nebeneinander.

**Nur die zwei Hauptprojekte**, nicht `TestGuardian.Core.Tests`/`TestGuardian.Console.Tests` — wie in deiner Anfrage ("innerhalb der 2 Komponenten").

## Neue Struktur

```
TestGuardian.Core/
  Trx/
    Models/
      AggregatedWarning.cs
      AssemblySummary.cs
      GuardianVerdict.cs      <- NEU, extrahiert aus TestRunVerdict.cs (siehe unten)
      ReadProgress.cs
      RunWarning.cs
      TestCaseResult.cs
      TestOutcome.cs
      TestRunOverview.cs
      TestRunResult.cs
      TrxReadFailure.cs
      TrxReadResult.cs
    TestRunAggregator.cs      (Logik, bleibt/verschiebt sich nicht)
    TestRunVerdict.cs         (nur noch die statische Klasse, Modelle raus)
    TrxBatchReader.cs
    TrxDocumentParser.cs
    TrxFileReader.cs
  Input/
    Models/
      InputResolutionResult.cs
      UnresolvedInput.cs
    TrxInputResolver.cs
  SynchronousProgress.cs      (bleibt an der Wurzel — kein "Objekt" im Sinne der Aufgabe,
                                sondern eine kleine Utility-Klasse mit Verhalten; auf Root-Ebene
                                liegt ohnehin kein Modell daneben, das getrennt werden müsste)

TestGuardian.Console/
  Models/
    CliOptions.cs             <- NEU, extrahiert aus CliArgumentParser.cs (siehe unten)
  CliArgumentParser.cs        (nur noch die statische Klasse)
  ConsoleReportPrinter.cs
  Program.cs
```

## Nötige Datei-Aufteilungen (nicht nur Verschieben)

Zwei Dateien mischen aktuell Modell und Logik in derselben Datei — reines Verschieben würde das nicht auflösen:

1. **`TestRunVerdict.cs`** enthält `VerdictReasonKind` (enum), `VerdictReason` (record), `VerdictSeverity` (enum) und `GuardianVerdict` (record) *zusammen* mit der `TestRunVerdict`-Klasse. Die vier Modelltypen wandern in eine neue `Trx/Models/GuardianVerdict.cs`, die Klasse selbst bleibt (gekürzt) in `Trx/TestRunVerdict.cs`.
2. **`CliArgumentParser.cs`** enthält den `CliOptions`-Record zusammen mit der Parser-Klasse. `CliOptions` wandert in eine neue `Console/Models/CliOptions.cs`.

Keine Verhaltensänderung, keine Logik wird angefasst — reines Verschieben/Aufteilen von Dateien.

## Offene Frage: Namensräume mitziehen oder nicht?

Aktuell entspricht der Ordner exakt dem Namensraum (`Trx/` → `TestGuardian.Core.Trx`, `Input/` → `TestGuardian.Core.Input`). Mit einem `Models`-Unterordner gibt es zwei Möglichkeiten:

- **A) Namensraum bleibt unverändert** (`TestGuardian.Core.Trx` auch für Dateien in `Trx/Models/`). Ordner ist dann rein organisatorisch, Namensraum bleibt die fachliche Gruppierung. Kein bestehender `using`, kein Testcode, keine Konsumenten-Datei muss angefasst werden — nur die betroffenen Dateien selbst wandern. Geringstes Risiko, am wenigsten Diff.
- **B) Namensraum wird mitgezogen** (`TestGuardian.Core.Trx.Models` etc.), damit Ordner und Namensraum 1:1 übereinstimmen (gängige .NET-Konvention). Bedeutet: jede Datei, die einen der neun verschobenen Typen verwendet, bräuchte zusätzlich `using TestGuardian.Core.Trx.Models;` (bzw. `.Input.Models`, `.Console.Models`) — betrifft praktisch jede Datei im Core-Projekt und mehrere Test-Dateien. Sauberer im klassischen Sinne, aber deutlich größerer, rein mechanischer Diff für eine reine Umsortierung ohne fachlichen Mehrwert.

Ich tendiere zu **A** — bei diesem Projektumfang bringt die 1:1-Namensraum-Regel keinen praktischen Nutzen, aber einen großen Diff quer durchs ganze Projekt, kurz bevor die Kür-Aufgaben starten. Deine Entscheidung.

## Nicht Teil dieses Refactorings

- Keine Änderung an `TestGuardian.Core.Tests`/`TestGuardian.Console.Tests`.
- Keine Verhaltens-/Logikänderung irgendeiner Art.
- `SynchronousProgress<T>` bleibt an der Core-Wurzel liegen (siehe oben).
