# Plan: feature/json-output

Ziel: Kür-Feature "Maschinenlesbare Ausgabe (JSON)" aus der Aufgabenstellung. Neuer Schalter `--to-json <Pfad>`: schreibt am Ende des Laufs den gesamten Report als JSON an den angegebenen Pfad — dieselben drei Dinge, die `ConsoleReportPrinter.Print` heute schon bekommt (`overview`, `inputResolution`, `verdict`), nicht nur die reine `TestRunOverview`.

## Aus der Rücksprache bestätigt

1. **"Overview" meinte den gesamten Output**, nicht nur `TestRunOverview` — JSON enthält `overview`, `verdict` und `unresolvedInputs`.
2. Enums als Strings (`JsonStringEnumConverter`), nicht als Zahlen.
3. `System.Text.Json` (BCL), keine neue Abhängigkeit.
4. Domain-Records direkt serialisiert, kein paralleles DTO-Modell — es wird nur geschrieben, nie wieder eingelesen, also ist das für Records bei Deserialisierung nötige Konstruktor-Matching hier irrelevant.
5. Modul lebt in `TestGuardian.Core`, nicht `TestGuardian.Console` (analog zu `TrxFileReader`, das auch schon File-I/O in Core macht).
6. **Schreibbarkeits-Check für den `--to-json`-Pfad passiert so früh wie möglich** (vor Eingabe-Auflösung/Testlauf) und wird wie ein fehlerhafter Schalteraufruf behandelt: `ArgumentException` → derselbe `PrintUsageError`/WARNUNG-Pfad → exit 1, ohne dass überhaupt eine `.trx`-Datei gelesen wird.
7. **Kritische Nachbesserung (2026-09-20): die Datei am Zielpfad wird nie geöffnet, wenn dort bereits eine Datei liegt — existiert dort schon eine Datei, wird sofort abgebrochen, ohne sie anzurühren.** Um Datenverlust unter allen Umständen auszuschließen: kein "Datei probeweise öffnen und wieder schließen"-Check mehr (das wäre bereits ein Öffnen der Zieldatei); der frühe Check prüft nur per `File.Exists`/`Directory.Exists`, ohne die Zieldatei je zu öffnen. Das eigentliche, einmalige Schreiben am Ende passiert ausschließlich mit `FileMode.CreateNew` — das schlägt atomar fehl, falls die Datei doch existiert (z.B. TOCTOU zwischen frühem Check und Programmende), statt sie zu überschreiben.

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

/// <summary>
/// Writes exactly once and never overwrites: opens the target with <see cref="FileMode.CreateNew"/>,
/// which itself throws if the file already exists — the authoritative guard against data loss,
/// independent of (and stricter than) whatever the earlier CLI-level existence check already caught.
/// </summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Write(string path, JsonReport report)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, report, Options);
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

## Früher Check (vor dem Testlauf) — rührt die Zieldatei nie an

Neue Methode `JsonOutputPathValidator.EnsureCanCreate(string path)` in `TestGuardian.Console` (CLI-spezifische Vorab-Validierung — reine Existenz-/Pfadprüfung, öffnet die Zieldatei zu keinem Zeitpunkt):

- `File.Exists(path)` → existiert die Datei bereits, sofort `ArgumentException` ("Datei existiert bereits — wird nicht überschrieben, um Datenverlust zu vermeiden."). Die Datei wird dabei nicht geöffnet, nur ihre Existenz geprüft.
- Zielverzeichnis (`Path.GetDirectoryName(path)`) existiert nicht → `ArgumentException` mit entsprechender Meldung.
- Kein Probe-Öffnen/-Erstellen der Zieldatei selbst — das ist ausschließlich Aufgabe des tatsächlichen Schreibvorgangs am Ende (siehe unten), und zwar nur genau einmal.

In `Program.cs`, direkt nach erfolgreichem `CliArgumentParser.Parse` und **vor** `TrxInputResolver.Resolve`, im selben `try`/`catch (ArgumentException)`-Block:

```csharp
if (options.JsonOutputPath is { } jsonPath)
{
    JsonOutputPathValidator.EnsureCanCreate(jsonPath);
}
```

## Tatsächliches Schreiben (nach der Auswertung) — einziger Zugriff auf die Zieldatei, ausschließlich `FileMode.CreateNew`

Am Ende von `Program.cs`, nach `ConsoleReportPrinter.Print(...)`. `JsonReportWriter.Write` öffnet ausschließlich mit `FileMode.CreateNew` (siehe oben) — das ist der einzige Zeitpunkt im gesamten Programm, an dem die Zieldatei überhaupt angefasst wird, und es schlägt garantiert fehl statt zu überschreiben, falls dort inzwischen doch eine Datei liegt (z.B. TOCTOU zwischen frühem Check und Programmende). Damit dieser seltene Fall nicht als roher, unkommentierter Stacktrace endet (inkonsistent mit jeder anderen I/O-Fehlerstelle im Projekt), fängt `Program.cs` an dieser Stelle gezielt `IOException` ab, gibt eine klare Meldung auf `Console.Error` aus und beendet mit `return 1` — unabhängig vom sonstigen Testverdikt, weil das ausdrücklich angeforderte Ergebnis (die JSON-Datei) dann nicht existiert:

```csharp
if (options.JsonOutputPath is { } jsonPath)
{
    try
    {
        JsonReportWriter.Write(jsonPath, new JsonReport(overview, inputResolution, verdict));
        Console.WriteLine($"JSON-Bericht geschrieben nach: {jsonPath}");
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"JSON-Bericht konnte nicht geschrieben werden: {ex.Message}");
        return 1;
    }
}

return verdict.IsSuccessful ? 0 : 1;
```

## Tests

- `CliArgumentParserTests`: `--to-json <Pfad>` setzt `JsonOutputPath` korrekt; `--to-json` ohne Wert wirft `ArgumentException`.
- `JsonOutputPathValidatorTests` (Console.Tests, Temp-Verzeichnisse wie in `TrxInputResolverTests`): Pfad in existierendem Verzeichnis, Datei existiert noch nicht → wirft nicht; **Datei existiert bereits → wirft `ArgumentException`, ohne die Datei anzurühren (Inhalt bleibt exakt erhalten — das ist der eigentliche Kernfall dieses Features)**; nicht existierendes Zielverzeichnis → wirft.
- `JsonReportWriterTests` (Core.Tests): geschriebene Datei lässt sich mit `JsonDocument.Parse` zurücklesen und enthält die erwarteten Felder (Assemblies, `severity` als String statt Zahl, `reasons`, `unresolvedInputs`) — kein exakter String-Vergleich (zu brüchig gegenüber Formatierungsänderungen), gleiches Prinzip wie in `ConsoleReportPrinterTests`; zusätzlich ein Test, der `Write` gegen einen bereits existierenden Pfad aufruft und prüft, dass eine `IOException` fliegt **und** der vorhandene Dateiinhalt unverändert bleibt.

## Nicht Teil dieses Features

- Keine Schema-Version, kein Zeitstempel im JSON — nicht angefragt, würde ohne belegten Bedarf Komplexität hinzufügen.
- Kein Einlesen/Deserialisieren von JSON — reine Ausgabe für externe Weiterverarbeitung.
- Keine Änderung an der bestehenden Exit-Code-/Verdikt-Logik selbst (Rot/Gelb/Grün) — `--to-json` ist ein zusätzlicher Seitenausgang, kein neuer Klassifizierungsfall.
