# Plan: feature/nachtlauf-demo-skeleton

Grundgerüst für Teil B der Aufgabe ("Der Beweis (C++)", `Fixtures/docs/Bewerberaufgabe-Testwaechter.pdf`, Seite 2f.). Dieser Plan deckt **nur das Gerüst** ab — kein vollständiger Beweis, keine zwei vorgeführten Läufe. Das ist bewusst so mit dir abgestimmt: du willst darauf selbst aufbauen.

## Warum ein neuer, getrennter Ordner statt Erweiterung von `TestGuardian/`

Teil B ist ein eigenständiges natives C++-Programm (Testprojekt + das, was es testet), kein Bestandteil von TestGuardian selbst — TestGuardian (C#) wertet später nur die von diesem Projekt erzeugte `.trx`-Datei aus. Eine Vermischung im selben Ordner/derselben Solution würde beide Programme technisch und gedanklich koppeln, obwohl sie nichts miteinander zu tun haben außer über das TRX-Dateiformat. Deshalb: neuer Ordner **`NachtlaufDemo/`**, direkt neben `TestGuardian/` auf Repo-Root-Ebene, mit eigener `.sln`.

**Namenswahl `NachtlaufDemo`:** angelehnt an das in der Aufgabe explizit vorgegebene Attribut-Beispiel `TEST_METHOD_ATTRIBUTE(L"Owner", L"Nachtlauf")`. Der Name macht sofort klar, wofür der Ordner da ist (die Nachtlauf-Filter-Demo für Teil B), ohne technische Interna wie "Cpp" oder "Native" vorwegzunehmen, die sich noch ändern könnten.

## Struktur

```
NachtlaufDemo/
  NachtlaufDemo.sln
  NachtlaufDemo.Tests/
    NachtlaufDemo.Tests.vcxproj
    NachtlaufDemo.Tests.vcxproj.filters
    pch.h
    pch.cpp
    NachtlaufTests.cpp
```

- **Ein einziges Projekt** (`NachtlaufDemo.Tests`), kein separates Bibliotheksprojekt. Begründung: Teil B ist laut Aufgabe "klein, aber unverzichtbar" — es geht um den *Beweis*, dass der Wächter einen leeren Filtertreffer erkennt, nicht um eine ausgebaute Produktionsbibliothek. Ein zusätzliches Bibliotheksprojekt wäre an dieser Stelle Architektur ohne Bedarf. Falls du beim Aufbauen merkst, dass du doch eine getrennte Bibliothek willst (z.B. um echten Produktionscode zu testen), trennen wir das dann.
- **Projekttyp:** "Native Unit Test Project" (Visual-Studio-Standardvorlage) mit dem Microsoft Unit Testing Framework for C++ (`CppUnitTest.h`, vorhanden unter deiner VS-2022-Installation). `pch.h`/`pch.cpp` gehören zur Standardvorlage dieses Projekttyps dazu — das hält das Projekt kompatibel mit "in Visual Studio öffnen und direkt weiterarbeiten".
- **Plattform:** nur **x64** (Debug|x64, Release|x64), Win32 wird bewusst nicht angelegt. Begründung: Der Aufgabentext warnt explizit vor dem 32-vs-64-Bit-Stolperstein, weil `vstest.console.exe` selbst als x64 läuft. Für das Grundgerüst vermeiden wir dieses Risiko, statt es unbeabsichtigt zu reproduzieren. Das *bewusste* Nachstellen dieses Stolpersteins (z.B. um zu prüfen, wie TestGuardian eine dadurch verursachte Fehlermeldung klassifiziert) wäre ein eigener, späterer Schritt — siehe "Nicht Teil dieses Features".

## Platzhalter-Testinhalt

Zwei Tests, rein um zu belegen, dass Projektvorlage, Framework-Referenz und das Attribut-Muster aus der Aufgabe funktionieren — **kein** fachlicher Inhalt, eindeutig als Platzhalter benannt, zum Ersetzen gedacht:

```cpp
TEST_CLASS(PlatzhalterTests)
{
public:
    TEST_METHOD(Platzhalter_OhneOwner_Erfolgreich)
    {
        Assert::IsTrue(true);
    }

    BEGIN_TEST_METHOD_ATTRIBUTE(Platzhalter_MitOwnerNachtlauf_Erfolgreich)
        TEST_METHOD_ATTRIBUTE(L"Owner", L"Nachtlauf")
    END_TEST_METHOD_ATTRIBUTE()

    TEST_METHOD(Platzhalter_MitOwnerNachtlauf_Erfolgreich)
    {
        Assert::IsTrue(true);
    }
};
```

Der zweite Test trägt schon jetzt `Owner=Nachtlauf`, damit der Filtermechanismus (`/TestCaseFilter:"Owner=Nachtlauf"`) sofort ausprobierbar ist, sobald das Projekt gebaut ist.

## Abgrenzung zu `TestGuardian/`

- Keine Projekt- oder Solution-Referenz zwischen `NachtlaufDemo/` und `TestGuardian/src/src.sln` — bewusst getrennt, wie besprochen.
- `.gitignore` im Repo-Root deckt die neuen Build-Artefakte bereits ab (`[Dd]ebug/`, `[Rr]elease/`, `x64/`, `.vs/`, `*.user` inkl. `*.vcxproj.user`) — keine Änderung nötig.

## Nicht Teil dieses Features (bewusst ausgeklammert)

- Die beiden vorzuführenden Läufe (Filter trifft / Filter trifft nicht) inkl. der dabei erzeugten `.trx`-Dateien.
- Echter Produktionscode, der fachlich sinnvoll ist (die Platzhalter-Tests testen nichts Reales).
- Verifikation, ob eine reale, von `vstest.console.exe` erzeugte `.trx`-Datei vom bestehenden C#-Parser (Teil A) korrekt verarbeitet wird — das wird erst mit den echten Läufen relevant und kann Nacharbeit am Parser nötig machen.
- Bewusstes Nachstellen des 32-vs-64-Bit-Stolpersteins.
- Ein `README.md` für `NachtlaufDemo/`.

## Baut das Projekt sofort? (Vorbehalt)

Ich lege die Projektdateien von Hand an (kein `devenv`/CLI-Scaffolding, um nichts automatisiert auszuführen). Ob es in Visual Studio ohne Nacharbeit lädt und baut, kannst nur du bestätigen — ich validiere das nicht selbst, das fällt unter "Tests/Builds nicht selbst verifizieren" ([[workflow-testguardian]] Punkt 1, sinngemäß auch auf den Build-Erfolg angewendet, da ein grüner Build hier dieselbe Rolle spielt wie ein grüner Testlauf).
