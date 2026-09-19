#include "pch.h"
#include "MathModuleToTest.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace TaglaufDemoTests
{

    TEST_CLASS(NachtlaufDemoTests)
    {
    public:
        TEST_METHOD(Test_Add_without_Owner)
        {
            const int a = 10;
            const int b = 2;

            int result = MathModule::add(a, b);

            Assert::IsTrue(result == 12);

        }

        BEGIN_TEST_METHOD_ATTRIBUTE(Test_Subtract_with_Owner)
            TEST_METHOD_ATTRIBUTE(L"Owner", L"Taglauf")
            END_TEST_METHOD_ATTRIBUTE()

        TEST_METHOD(Test_Subtract_with_Owner)
        {
            const int a = 8;
            const int b = 7;

            int result = MathModule::subtract(a, b);

            Assert::IsTrue(result == 1);

        }

        BEGIN_TEST_METHOD_ATTRIBUTE(Test_Multiply_with_Owner)
            TEST_METHOD_ATTRIBUTE(L"Owner", L"Taglauf")
            END_TEST_METHOD_ATTRIBUTE()

            TEST_METHOD(Test_Multiply_with_Owner)
        {
            const int a = 3;
            const int b = 2;

            int result = MathModule::multiply(a, b);

            Assert::IsTrue(result == 6);

        }

    };

};