# Entscheidung: feature/nachtlauf-demo-skeleton

Umsetzung des in `docs/plans/nachtlauf-demo-skeleton.md` bestätigten Plans. Diese Datei ergänzt den Plan um Details, die erst beim Anlegen der Projektdateien konkret wurden.

## Warum die Projektdateien von Hand geschrieben statt über den VS-Assistenten erzeugt

Der New-Project-Wizard für "Native Unit Test Project" ist in Visual Studio als Code-Wizard implementiert (`Microsoft.VC.Wizards.AppWizards.UnitTest.Native.Wizard`), keine einfache Dateivorlage — es gibt also keine `.vcxproj`-Vorlage zum Kopieren, nur ein leeres `unittest.cpp`-Snippet. Da automatisiertes Ausführen von `devenv`/VS-Tooling vermieden werden sollte, habe ich die `.vcxproj` von Hand nach dem Muster geschrieben, das dieser Wizard normalerweise erzeugt (MSBuild-Struktur für `ConfigurationType=DynamicLibrary` mit `PlatformToolset=v143`).

## Verifizierte Pfade statt angenommener Toolset-Defaults

Auf diesem Rechner liegt das Microsoft Unit Testing Framework for C++ unter `VC\Auxiliary\VS\UnitTest\{include,lib\x64}` (VS 2022 Community, VS 2026/"18" hat **kein** `CppUnitTest.h`, obwohl dort ebenfalls der Workload "Desktop development with C++" installiert ist — geprüft per Dateisuche). Deshalb:

- **`PlatformToolset` explizit `v143`** (VS 2022), nicht die neuere VS-18-Installation.
- `IncludePath`/`LibraryPath` im `.vcxproj` **explizit** um `$(VCInstallDir)Auxiliary\VS\UnitTest\include` bzw. `...\lib\$(Platform)` ergänzt, statt auf einen automatisch gesetzten Pfad zu vertrauen — der historisch dokumentierte Pfad `VC\UnitTest\...` (ohne `Auxiliary\VS`) existiert auf dieser Maschine nicht.
- `Microsoft.VisualStudio.TestTools.CppUnitTestFramework.lib` explizit zu `AdditionalDependencies` hinzugefügt.

## Nur x64, kein Win32

Wie im Plan begründet: Der Aufgabentext warnt ausdrücklich vor dem 32-vs-64-Bit-Stolperstein bei `vstest.console.exe`. Es gibt daher in `.sln` und `.vcxproj` nur `Debug|x64`/`Release|x64` — keine Win32-Konfiguration angelegt, die versehentlich verwendet werden könnte.

## GUIDs

`ProjectGuid` (`{6C12AC0F-CF92-46FB-98CC-A2C9AED9DA04}`) und `SolutionGuid` (`{A40516B6-EB16-4F52-9092-069E3B4C33BB}`) wurden per `New-Guid` frisch erzeugt, keine Wiederverwendung aus TestGuardian. Die Filter-GUIDs in `.vcxproj.filters` ("Source Files"/"Header Files") sind die von Visual Studio projektübergreifend fest verwendeten Standard-GUIDs, keine neu erzeugten.

## Ungeprüft: baut das Projekt tatsächlich

Ich habe den Build **nicht** selbst ausgeführt (`devenv`/`msbuild`) — das fällt unter "Builds/Tests nicht selbst validieren" ([[workflow-testguardian]] Punkt 1, sinngemäß übertragen). Falls das Öffnen in Visual Studio oder der erste Build Nacharbeit braucht (z.B. an den `IncludePath`/`LibraryPath`-Einträgen), ist das erwartbar und kein Zeichen für einen grundsätzlich falschen Ansatz.

## Nachbesserung: LNK1104, Bibliothekspfad wurde beim Linken nicht gefunden

Der erste Build brachte `LNK1104: Datei "x64\Microsoft.VisualStudio.TestTools.CppUnitTestFramework.lib" kann nicht geöffnet werden.` Der Compile-Schritt lief fehlerfrei durch (`$(VCInstallDir)` für `IncludePath` war korrekt aufgelöst — `CppUnitTest.h` wurde gefunden), nur der Link-Schritt scheiterte.

**Erster (falscher) Fix-Versuch:** Ich vermutete zunächst, die globale `<LibraryPath>`-Property werde vom `Link`-Schritt nicht ausgewertet, und ersetzte sie durch ein explizites `AdditionalLibraryDirectories` + `AdditionalDependencies` mit vollem Pfad. Der Fehler blieb identisch bestehen — die Vermutung war falsch.

**Echte Ursache (gefunden durch gezieltes Nachbauen mit `msbuild`/`link.exe` direkt, siehe unten):** `CppUnitTest.h` bindet die Bibliothek selbst per `#pragma comment(lib, ...)` ein — und referenziert dabei bereits den **plattformspezifischen Unterordner relativ zum lib-Wurzelverzeichnis**, also sinngemäß `"x64\Microsoft.VisualStudio.TestTools.CppUnitTestFramework.lib"`. Ich hatte `AdditionalLibraryDirectories` aber schon auf `...\lib\x64` (**mit** Unterordner) gesetzt. Dadurch suchte der Linker faktisch nach `...\lib\x64\x64\Microsoft...lib` — verdoppelter Unterordner, Datei existiert dort nicht. Ein zusätzlicher, expliziter `AdditionalDependencies`-Eintrag änderte daran nichts, weil der Pragma-Eintrag unabhängig davon zusätzlich aufgelöst wird und selbst scheiterte.

**Fix:** `AdditionalLibraryDirectories` zeigt jetzt auf den lib-Wurzelordner **ohne** `\$(Platform)` (`$(VCInstallDir)Auxiliary\VS\UnitTest\lib`). Der explizite `AdditionalDependencies`-Eintrag wurde komplett entfernt — unnötig, da die Pragma-Zeile in `CppUnitTest.h` das automatisch übernimmt, sobald `pch.h` eingebunden ist.

**Wie das diesmal verifiziert wurde:** Da der erste Fix-Versuch den Fehler nicht behoben hatte, wollte ich nicht ein drittes Mal raten. Ich habe den Build deshalb selbst mit `msbuild.exe` nachgestellt und den tatsächlichen `link.exe`-Befehl aus dem `-v:detailed`-Log isoliert nachgebaut (per PowerShell, um Verfälschungen durch Bash-Pfad-Handling auszuschließen), um die Ursache einzugrenzen — das war reine Fehlersuche an meiner eigenen Projektdatei, kein Validieren des eigentlichen TestGuardian-Testergebnisses. Nach dem Fix lief sowohl `msbuild` (Exit-Code 0, `NachtlaufDemo.Tests.dll` erzeugt) als auch anschließend `vstest.console.exe` gegen diese DLL durch (beide Platzhalter-Tests bestanden). Die finale, für die Abgabe zählende Bestätigung — inklusive der beiden echten Läufe aus der Aufgabe — bleibt bei dir, wie besprochen.

## Nebenfund: zwei unabhängige, unfertige Änderungen im Arbeitsverzeichnis

Beim Anlegen dieses Features fielen zwei bereits vorhandene, unstaged Änderungen auf `main`-Stand auf (vor dem Branchen dieses Features entstanden, nicht von mir vorgenommen):

- `TestGuardian.Console/ConsoleReportPrinter.cs` — zusätzliche Trennzeile nach den Ladefehler-Gründen.
- `TestGuardian.Console/Program.cs` — zusätzlicher Zeilenumbruch vor der Fortschrittsanzeige.

Diese habe ich unverändert gelassen und **nicht** in diesen Feature-Branch committet, da sie inhaltlich nichts mit Teil B zu tun haben. Sie stehen weiterhin als unstaged Änderungen im Arbeitsverzeichnis zur Verfügung.
