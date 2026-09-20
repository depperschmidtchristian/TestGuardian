# Entscheidung: feature/help-and-install-script

Kleiner, zeitkritischer Umbau kurz vor Abgabe — bewusst ohne separaten Plan-Vorab-Durchlauf umgesetzt (Umfang klar, zwei unabhängige, risikoarme Anpassungen). Begründung hier statt in einem eigenen Plan-Dokument.

## `--help` läuft nie durch `CliArgumentParser`

Geprüft in `Program.cs` als allererstes, noch vor `CliArgumentParser.Parse`: `args.Any(a => a is "--help" or "-h")`. Wäre `--help` stattdessen ein Fall *innerhalb* von `CliArgumentParser.Parse`, müsste es sich gegen die "keine Eingabe angegeben"-Prüfung durchsetzen (die dort ganz am Ende sitzt) — `TestGuardian --help` ganz ohne Datei-Eingabe würde sonst fälschlich als Bedienfehler abgewiesen, bevor die Hilfe überhaupt gedruckt wird. Ein Scan von `args` vorab umgeht dieses Henne-Ei-Problem sauber und ist außerdem, wie bei den meisten CLI-Tools üblich, unabhängig davon gültig, was sonst noch (valide oder nicht) auf der Kommandozeile steht.

Exit-Code `0`: Hilfe anzuzeigen ist eine erfolgreich abgeschlossene Aktion, kein Fehler — konsistent mit `--help` bei praktisch jedem anderen CLI-Tool.

## Installations-Skript statt zwei Handbefehlen

`dotnet tool install` kann nicht direkt aus dem Quellcode installieren, sondern nur aus einem bereits gepackten `.nupkg` — ein einzelner nativer `dotnet`-Befehl, der Bauen *und* Installieren in einem Schritt erledigt, existiert nicht. `install.ps1` (Repo-Wurzel) macht daraus für den Nutzer trotzdem einen einzigen Befehl:

1. `dotnet pack ... -c Release -o nupkg` — **explizit Release**, nicht der SDK-Default (Debug).
2. `dotnet tool uninstall --global TestGuardian` (Fehler bewusst ignoriert, falls noch nicht installiert) — macht das Skript beliebig oft wiederholbar, ohne dass ein späterer Lauf mit "already installed" abbricht.
3. `dotnet tool install --global --add-source nupkg TestGuardian`.

Jeder der beiden `dotnet`-Aufrufe, die tatsächlich fehlschlagen können (`pack`, `install`), wird über `$LASTEXITCODE` geprüft und bricht mit klarer Fehlermeldung ab, statt stillschweigend weiterzulaufen — passend zur Grundphilosophie des ganzen Projekts, auch hier im Installationsschritt selbst.

`nupkg/` (Ausgabeordner des Skripts) landet nicht im Repo — `*.nupkg` steht schon in `.gitignore`.

## Tests

- `ConsoleReportPrinterTests.PrintHelp_OutputListsEverySwitch`: prüft, dass alle aktuellen Schalter (`--max-depth`, `--min-tests`, `--to-json`, `--baseline`, `--known-failures`, `--help`) in der Hilfe-Ausgabe auftauchen — würde brechen, falls ein Schalter mal ergänzt und die Hilfe vergessen wird.
- `install.ps1` selbst habe ich nicht ausgeführt (Installationsskripte fallen unter dieselbe Regel wie Tests — das übernimmst du).
- Wie immer: `dotnet build` (Clean-Rebuild aller vier Projekte) lief bei mir sauber durch, 0 Fehler/Warnungen.
