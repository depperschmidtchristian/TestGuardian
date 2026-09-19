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

## Nebenfund: zwei unabhängige, unfertige Änderungen im Arbeitsverzeichnis

Beim Anlegen dieses Features fielen zwei bereits vorhandene, unstaged Änderungen auf `main`-Stand auf (vor dem Branchen dieses Features entstanden, nicht von mir vorgenommen):

- `TestGuardian.Console/ConsoleReportPrinter.cs` — zusätzliche Trennzeile nach den Ladefehler-Gründen.
- `TestGuardian.Console/Program.cs` — zusätzlicher Zeilenumbruch vor der Fortschrittsanzeige.

Diese habe ich unverändert gelassen und **nicht** in diesen Feature-Branch committet, da sie inhaltlich nichts mit Teil B zu tun haben. Sie stehen weiterhin als unstaged Änderungen im Arbeitsverzeichnis zur Verfügung.
