# Plan: feature/json-output

Ziel: Kür-Feature "Maschinenlesbare Ausgabe (JSON)" aus der Aufgabenstellung. Neuer Schalter `--to-json <Pfad>`: schreibt am Ende des Laufs den gesamten Report als JSON an den angegebenen Pfad — dieselben drei Dinge, die `ConsoleReportPrinter.Print` heute schon bekommt (`overview`, `inputResolution`, `verdict`), nicht nur die reine `TestRunOverview`.

## Aus der Rücksprache bestätigt

1. **"Overview" meinte den gesamten Output**, nicht nur `TestRunOverview` — JSON enthält `overview`, `verdict` und `unresolvedInputs`.
2. Enums als Strings (`JsonStringEnumConverter`), nicht als Zahlen.
3. `System.Text.Json` (BCL), keine neue Abhängigkeit.
4. Domain-Records direkt serialisiert, kein paralleles DTO-Modell — es wird nur geschrieben, nie wieder eingelesen, also ist das für Records bei Deserialisierung nötige Konstruktor-Matching hier irrelevant.
5. Modul lebt in `TestGuardian.Core`, nicht `TestGuardian.Console` (analog zu `TrxFileReader`, das auch schon File-I/O in Core macht).
6. **Schreibbarkeits-Check für den `--to-json`-Pfad passiert so früh wie möglich** (vor Eingabe-Auflösung/Testlauf) und wird wie ein fehlerhafter Schalteraufruf behandelt: `ArgumentException` → derselbe `PrintUsageError`/WARNUNG-Pfad → exit 1, ohne dass überhaupt eine `.trx`-Datei gelesen wird.

## Neue Dateien

`TestGuardian.Core/Json/Models/JsonReport.cs` (passend zur `Models/`-Konvention aus dem letzten Refactoring):

```csharp
using TestGuardian.Core.Input.Models;
using TestGuardian.Core.Trx.Models;

namespace TestGuardian.Core.Json.Models;

public sealed record JsonReport(
    TestRunOverview Overview,
    InputResolutionResult InputResolution,
    GuardianVerdict Verdict);
```

`TestGuardian.Core/Json/JsonReportWriter.cs`:

```csharp
namespace TestGuardian.Core.Json;

public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Write(string path, JsonReport report)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options));
    }
}
```

## CLI-Verdrahtung

`CliOptions` (`Console/Models/CliOptions.cs`) bekommt ein weiteres Feld:

```csharp
public sealed record CliOptions(IReadOnlyList<string> Inputs, int MaxDepth, int? MinTests, string? JsonOutputPath);
```

`CliArgumentParser.Parse` erkennt `--to-json <Pfad>` analog zu `--min-tests` (Wert ist Pflicht):

```csharp
case "--to-json" when i + 1 < args.Length:
    jsonOutputPath = args[i + 1];
    i++;
    break;
case "--to-json":
    throw new ArgumentException("--to-json erwartet einen Zielpfad als Wert.");
```

## Schreibbarkeits-Check (früh, vor dem Testlauf)

Neue Methode `JsonOutputPathValidator.EnsureWritable(string path)` in `TestGuardian.Console` (CLI-spezifische Vorab-Validierung — Core bleibt bis auf den eigentlichen Schreibvorgang am Ende I/O-frei für diesen Zweig):

- Versucht `File.Open(path, FileMode.Create, FileAccess.Write)` und schließt sofort wieder (reiner Zugriffstest, kein Inhalt wird geschrieben — die Datei wird am Ende ohnehin mit dem echten JSON überschrieben).
- Fängt `UnauthorizedAccessException`, `DirectoryNotFoundException`, `IOException`, `NotSupportedException`, `PathTooLongException` ab und wirft stattdessen eine `ArgumentException` mit einer verständlichen, den Pfad und die Ursache nennenden Meldung.

In `Program.cs`, direkt nach erfolgreichem `CliArgumentParser.Parse` und **vor** `TrxInputResolver.Resolve`, im selben `try`/`catch (ArgumentException)`-Block:

```csharp
if (options.JsonOutputPath is { } jsonPath)
{
    JsonOutputPathValidator.EnsureWritable(jsonPath);
}
```

## Tatsächliches Schreiben (nach der Auswertung)

Am Ende von `Program.cs`, nach `ConsoleReportPrinter.Print(...)`:

```csharp
if (options.JsonOutputPath is { } jsonPath)
{
    JsonReportWriter.Write(jsonPath, new JsonReport(overview, inputResolution, verdict));
    Console.WriteLine($"JSON-Bericht geschrieben nach: {jsonPath}");
}
```

**Offene Frage (dein Ermessen):** Der frühe Check schließt die meisten Fehler aus, aber ein seltener TOCTOU-Fall bleibt (Pfad wird zwischen Check und tatsächlichem Schreiben ungültig, z.B. Netzlaufwerk getrennt). Vorschlag: den eigentlichen Schreibvorgang ebenfalls in try/catch nehmen und bei Fehlschlag mit klarer Meldung exit 1 geben — unabhängig vom sonstigen Testverdikt, weil das ausdrücklich angeforderte Ergebnis (die JSON-Datei) sonst nicht existiert, obwohl der Aufruf das nicht sichtbar macht. Sag Bescheid, falls du es anders willst (z.B. nur Warnung auf stderr, Exit-Code bleibt am Testverdikt).

## Tests

- `CliArgumentParserTests`: `--to-json <Pfad>` setzt `JsonOutputPath` korrekt; `--to-json` ohne Wert wirft `ArgumentException`.
- `JsonOutputPathValidatorTests` (Console.Tests, Temp-Verzeichnisse wie in `TrxInputResolverTests`): schreibbarer Pfad wirft nicht; nicht existierendes Zielverzeichnis wirft; schreibgeschützte Datei wirft.
- `JsonReportWriterTests` (Core.Tests): geschriebene Datei lässt sich mit `JsonDocument.Parse` zurücklesen und enthält die erwarteten Felder (Assemblies, `severity` als String statt Zahl, `reasons`, `unresolvedInputs`) — kein exakter String-Vergleich (zu brüchig gegenüber Formatierungsänderungen), gleiches Prinzip wie in `ConsoleReportPrinterTests`.

## Nicht Teil dieses Features

- Keine Schema-Version, kein Zeitstempel im JSON — nicht angefragt, würde ohne belegten Bedarf Komplexität hinzufügen.
- Kein Einlesen/Deserialisieren von JSON — reine Ausgabe für externe Weiterverarbeitung.
- Keine Änderung an der bestehenden Exit-Code-/Verdikt-Logik selbst (Rot/Gelb/Grün) — `--to-json` ist ein zusätzlicher Seitenausgang, kein neuer Klassifizierungsfall.
