# Plan: feature/input-warning-level

Ziel: Fehlerhafte **Eingabe** in TestGuardian (falscher Pfad, Suchmuster trifft nichts, zu viele/verirrte Argumente) soll nicht mehr als "URTEIL: ROT" erscheinen — das suggeriert einen echten Testfehlschlag. Stattdessen: eine gelbe **Warnung**, die klar sagt "hier stimmt etwas an deinem Aufruf nicht", getrennt von echten roten Testergebnissen.

**Wichtig, unverändert:** Der Exit-Code bleibt in jedem dieser Fälle `≠ 0`. Die Pflichtanforderung ("Ein Werkzeug, das bei kaputter Eingabe stillschweigend 'alles gut' meldet, hat denselben Fehler wie das Problem, das es lösen soll") verlangt nicht, dass es *rot* aussieht — nur, dass es nicht *grün* ist. Gelb erfüllt das, ist aber ehrlicher darüber, wo das Problem liegt.

## Betroffene Fälle (Ist-Zustand heute)

| Fall | Beispiel | Wo im Code | Heute |
|---|---|---|---|
| Unbekannte/kaputte CLI-Option | `--minTests` (Tippfehler) | `CliArgumentParser.Parse` wirft `ArgumentException`, in `Program.cs` abgefangen | Eigener Pfad: `Console.Error.WriteLine("Fehlerhafter Aufruf: ...")`, kein Verdict, `return 1`. **Läuft schon nicht durchs ROT-Banner** — siehe Commit `adb56a3`. |
| Zu viele/verirrte Argumente (kein `--`-Präfix, z.B. `echo` aus einem verketteten PowerShell-Befehl ohne `;`) | `TestGuardian ... --min-tests 3 echo $LASTEXITCODE` | Landet in `CliOptions.Inputs`, dann `TrxInputResolver` → `UnresolvedInput` | Fließt in `GuardianVerdict` über `VerdictReasonKind.UnresolvedInputs` → **heute ROT** |
| Falscher Pfad / Suchmuster trifft nichts | `TestGuardian C:\Tippfehler\` | `TrxInputResolver` → `UnresolvedInput` | **heute ROT** |
| Datei existiert, aber Inhalt kaputt (fehlt, leer, unparsbares XML) | `lauf-e.trx`-Fall | `TrxFileReader`/`TrxDocumentParser` → `TrxReadFailure` → `overview.FileFailures` | **heute ROT** — siehe "Offene Entscheidung" unten |

Die erste Zeile ist also schon gelöst (kein ROT-Banner, aber auch keine einheitliche Optik). Die drei anderen sollen laut deiner Anfrage von ROT auf GELB wechseln.

## Domänenmodell-Änderung (`TestGuardian.Core/Trx/TestRunVerdict.cs`)

`GuardianVerdict` bekommt eine dritte Stufe statt nur grün/rot:

```csharp
public enum VerdictSeverity
{
    Green,
    Yellow,
    Red
}

public sealed record GuardianVerdict(VerdictSeverity Severity, IReadOnlyList<VerdictReason> Reasons)
{
    // Exit-Code-Entscheidung bleibt binär: alles außer Green scheitert.
    public bool IsSuccessful => Severity == VerdictSeverity.Green;
}
```

`TestRunVerdict.Evaluate` sammelt Gründe wie bisher, aber in zwei Eimern statt einem:

- **Rot-Gründe** (echtes Testergebnis): `RealTestFailures`, `LoadErrors`, `ZeroTestsExecuted`, `BelowMinimumTestCount`.
- **Gelb-Gründe** (Eingabe-/Bedienproblem): `UnresolvedInputs`, und — **vorbehaltlich deiner Entscheidung unten** — `UnreadableFiles`.

```csharp
var severity =
    redReasons.Count > 0 ? VerdictSeverity.Red :
    yellowReasons.Count > 0 ? VerdictSeverity.Yellow :
    VerdictSeverity.Green;
```

**Vorrangregel:** Sobald mindestens ein Rot-Grund vorliegt, ist das Gesamturteil ROT — auch wenn zusätzlich ein Gelb-Grund vorliegt (z.B. eine Datei nicht lesbar, aber eine andere zeigt einen echten Fehlschlag). Gelb-Gründe werden in dem Fall trotzdem mit ausgegeben, bestimmen aber nicht die Bannerfarbe. Ein echter Testfehlschlag darf nie durch ein Eingabeproblem "verdeckt" werden.

`VerdictReasonKind` selbst bleibt unverändert (keine Kategorien-Info im Enum nötig — die Zuordnung zu Rot/Gelb passiert einmalig, zentral in `Evaluate`).

## Konsolenausgabe (`TestGuardian.Console/ConsoleReportPrinter.cs`)

`PrintVerdict` unterscheidet aktuell nur Grün/Rot:

```csharp
var (color, label) = verdict.IsSuccessful
    ? (ConsoleColor.Green, "GRUEN")
    : (ConsoleColor.Red, "ROT");
```

wird zu:

```csharp
var (color, label) = verdict.Severity switch
{
    VerdictSeverity.Green => (ConsoleColor.Green, "GRUEN"),
    VerdictSeverity.Yellow => (ConsoleColor.Yellow, "WARNUNG"),
    VerdictSeverity.Red => (ConsoleColor.Red, "ROT"),
};
```

("WARNUNG" statt z.B. "GELB", weil es beschreibt *was* es ist, nicht nur die Farbe — konsistent mit "GRUEN"/"ROT", die auch das Urteil selbst benennen, nicht nur die Farbe.)

## Entscheidungen (bestätigt am 2026-09-19)

1. **`UnreadableFiles` bleibt Rot.** Eine gefundene, aber inhaltlich kaputte Datei ist zu mehrdeutig, um sie automatisch als bloßen Bedienfehler einzustufen — bleibt beim strengeren Rot.
2. **Ja** — der CLI-Parse-Fehler-Pfad wird optisch an den gelben Balken angeglichen (`ConsoleReportPrinter.PrintUsageError(string message)`, gleicher Balken-Stil wie `PrintVerdict`, ersetzt die bisherige `Console.Error.WriteLine`-Zeile in `Program.cs`).
3. **Exit-Code bleibt `1`** für Gelb (wie für Rot) — kein eigener Code, da die Pflichtanforderung nur "≠ 0" verlangt.

Damit zählt als **Gelb** ausschließlich `VerdictReasonKind.UnresolvedInputs` sowie der CLI-Parse-Fehler-Pfad (der gar nicht erst bis zu `GuardianVerdict` kommt). Alle anderen Rot-Gründe (`RealTestFailures`, `LoadErrors`, `ZeroTestsExecuted`, `BelowMinimumTestCount`, `UnreadableFiles`) bleiben Rot.

## Tests

- `TestRunVerdictTests` (neu, falls noch nicht vorhanden, sonst erweitert): pro Reason-Kind ein Fall, der prüft, welche `VerdictSeverity` herauskommt — insbesondere: nur `UnresolvedInputs` → Yellow; nur `RealTestFailures` → Red; beides gleichzeitig → Red (Vorrangregel); keine Gründe → Green.
- `ConsoleReportPrinterTests`: neuer Fall für `VerdictSeverity.Yellow` → Ausgabe enthält "WARNUNG", nicht "ROT"/"GRUEN".
- Falls Entscheidung 2 (ja): neuer Test für `ConsoleReportPrinter.PrintUsageError`.

## Nicht Teil dieses Features

- Keine Änderung an der CLI-Parsing-Logik selbst (`CliArgumentParser`) — nur daran, wie ihr Fehlerfall *dargestellt* wird (falls Entscheidung 2 = ja).
- Keine Änderung an `--min-tests`/Zählweise/LoadError-Klassifizierung — reine Präsentations-/Verdict-Kategorisierungs-Änderung.
