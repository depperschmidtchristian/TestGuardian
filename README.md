# TestGuardian

Ein Werkzeug, das hinter jedem Testlauf sitzt und entscheidet: darf dieses Ergebnis wirklich als erfolgreich durchgehen? Hintergrund: ein Lauf, der 13 von 13 Tests als erfolgreich meldete, obwohl tatsächlich null Tests liefen (Testfilter traf nichts, das Ergebnis wurde trotzdem als Erfolg gemeldet). TestGuardian prüft genau das nach — anhand der `.trx`-Ergebnisdateien von Visual Studio / `vstest.console.exe`.

## Was es tut

- Liest eine oder mehrere `.trx`-Dateien (Einzeldatei, Ordner, Suchmuster) und gibt eine Übersicht je Bibliothek und gesamt aus (ausgeführt · bestanden · fehlgeschlagen · übersprungen).
- Scheitert (Exit-Code ≠ 0), wenn mindestens ein Test fehlgeschlagen ist, null Tests ausgeführt wurden, oder weniger Tests liefen als erwartet (`--min-tests`).
- Unterscheidet echte Fehlschläge von **Ladefehlern** (Testbibliothek konnte nicht geladen werden) — beides in einen Topf zu werfen verfälscht das Bild.
- Meldet kaputte Eingaben (fehlende Datei, leere/kaputte XML, falscher Pfad) niemals stillschweigend als "alles gut", sondern als sichtbare Warnung oder fehlerhafte Meldung.
- **Zusätzlich (Kür):** maschinenlesbare JSON-Ausgabe, Vergleich gegen einen gespeicherten Bericht ("war dieser Test gestern schon fehlgeschlagen?"), und eine Liste bekannter, geduldeter Fehlschläge (z. B. Tests, die eine hier fehlende Installation brauchen).

Alle Design-Entscheidungen samt Begründung stehen einzeln unter [`docs/decisions/`](docs/decisions), die zugehörigen Pläne unter [`docs/plans/`](docs/plans).

## Bauen & Installieren

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download).

Als globales Tool paketieren (Release, nicht Debug) und installieren:

```powershell
dotnet pack TestGuardian\src\TestGuardian.Console\TestGuardian.Console.csproj -c Release -o nupkg
dotnet tool install --global --add-source nupkg TestGuardian
```

War TestGuardian schon einmal installiert (z. B. nach Codeänderungen neu installieren), zuerst `dotnet tool uninstall --global TestGuardian` ausführen, dann `dotnet tool install` wie oben erneut.

Danach steht `TestGuardian` als Befehl in jedem Terminal zur Verfügung:

```powershell
TestGuardian --help
```

Nur bauen, ohne zu installieren (z. B. zum Entwickeln): `dotnet build TestGuardian\src\src.sln`.

## Verwendung

```
TestGuardian <Datei|Ordner|Suchmuster>... [Optionen]
```

| Option | Bedeutung |
|---|---|
| `--max-depth [<n>]` | Unterordner mit durchsuchen (ohne Zahl: bis zu 10 Ebenen). Nur für Ordner-Eingaben, nicht für Suchmuster. |
| `--min-tests <n>` | Lauf gilt als fehlerhaft, wenn weniger als `<n>` Tests ein eindeutiges Ergebnis lieferten. |
| `--to-json [<Pfad>]` | Schreibt den vollständigen Bericht als JSON. Ohne Pfad: `testguardian_report_<n>.json` im aktuellen Verzeichnis. Überschreibt nie eine bestehende Datei. |
| `--baseline <Pfad>` | Vergleicht die aktuell fehlgeschlagenen Tests gegen einen zuvor mit `--to-json` erzeugten Bericht (neu fehlgeschlagen / bereits vorher bekannt / seither behoben). |
| `--known-failures <Pfad>` | Textdatei mit bekannten, geduldeten Fehlschlägen (ein Testname pro Zeile, `#`-Kommentare erlaubt) — zählen nicht als Grund für ein fehlerhaftes Urteil, bleiben aber sichtbar. |
| `--help`, `-h` | Diese Hilfe anzeigen und beenden. |

**Exit-Code:** `0` bei Urteil ERFOLGREICH, `1` bei WARNUNG oder FEHLERHAFT, oder bei einem fehlerhaften Aufruf.

### Beispiele

Mit den mitgelieferten Beispieldaten (`Fixtures/Sample Data/`) ausprobieren:

```powershell
TestGuardian "Fixtures\Sample Data\lauf-a.trx"                       # echte Fehlschläge -> FEHLERHAFT
TestGuardian "Fixtures\Sample Data\lauf-b.trx"                       # Filter trifft nichts, vstest meldet trotzdem Erfolg -> FEHLERHAFT
TestGuardian "Fixtures\Sample Data" --to-json bericht-heute.json     # JSON-Bericht schreiben
TestGuardian "Fixtures\Sample Data" --baseline bericht-gestern.json  # gegen einen alten Bericht vergleichen
```

## Teil B: der native Beweis (`GuardianProof`)

`GuardianProof/` ist ein natives C++-Testprojekt (Microsoft Unit Testing Framework for C++). Zwei Demoläufe zeigen den eigentlichen Zweck von TestGuardian:

1. Ein Filter, der trifft (`Owner=Nachtlauf`) → Tests laufen, TestGuardian meldet ERFOLGREICH.
2. Ein Filter, der nichts trifft → `vstest.console.exe` selbst meldet Erfolg, TestGuardian schlägt Alarm.

Fertige `.trx`-Ergebnisse beider Läufe (und einiger weiterer Varianten) liegen bereits unter `GuardianProof/TestResults/`. Eigene Läufe: Lösung in Visual Studio öffnen (`GuardianProof/GuardianProof.sln`), Tests bauen, dann z. B.

```powershell
vstest.console.exe GuardianProof\x64\Debug\GuardianProof.Tests.dll /TestCaseFilter:"Owner=Nachtlauf" /Logger:trx
TestGuardian TestResults\*.trx
```

## Tests ausführen

```powershell
dotnet test TestGuardian\src\src.sln
```
