#include "pch.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NachtlaufDemoTests
{
    // Platzhalter-Tests: belegen nur, dass Projektvorlage, Framework-Referenz und das
    // TEST_METHOD_ATTRIBUTE-Muster aus der Aufgabe funktionieren. Kein fachlicher Inhalt.
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
}
