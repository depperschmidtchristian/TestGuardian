#include "pch.h"
#include "MathModuleToTest.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NachtlaufDemoTests
{

    TEST_CLASS(NachtlaufDemoTests)
    {
    public:
        TEST_METHOD(Test_Add_without_Owner)
        {
            const int a = 1;
            const int b = 2;

            int result = MathModule::add(a, b);

            Assert::IsTrue(result == 3);

        }

        TEST_METHOD(Test_Subtract_without_Owner)
        {
            const int a = 5;
            const int b = 7;

            int result = MathModule::subtract(a, b);

            Assert::IsTrue(result == -2);

        }

        TEST_METHOD(Test_Multiply_without_Owner)
        {
            const int a = 10;
            const int b = 5;

            int result = MathModule::multiply(a, b);

            Assert::IsTrue(result == 50);

        }

        BEGIN_TEST_METHOD_ATTRIBUTE(Test_Multiply_with_Owner)
            TEST_METHOD_ATTRIBUTE(L"Owner", L"Nachtlauf")
            END_TEST_METHOD_ATTRIBUTE()

        TEST_METHOD(Test_Multiply_with_Owner)
        {
            const int a = 3;
            const int b = 10;

            int result = MathModule::multiply(a, b);

            Assert::IsTrue(result == 30);

        }

        BEGIN_TEST_METHOD_ATTRIBUTE(Test_Divide_with_Owner)
            TEST_METHOD_ATTRIBUTE(L"Owner", L"Nachtlauf")
            END_TEST_METHOD_ATTRIBUTE()

        TEST_METHOD(Test_Divide_with_Owner)
        {
            const int a = 10;
            const int b = 2;

            int result = MathModule::divide(a, b);

            Assert::IsTrue(result == 5);

        }

        //BEGIN_TEST_METHOD_ATTRIBUTE(Test_DivideModulo_with_Owner)
        //    TEST_METHOD_ATTRIBUTE(L"Owner", L"Nachtlauf")
        //    END_TEST_METHOD_ATTRIBUTE()

        //TEST_METHOD(Test_DivideModulo_with_Owner)
        //{
        //    const int a = 20;
        //    const int b = 2;

        //    int result = MathModule::divideModulo(a, b);

        //    Assert::IsTrue(result == 0);

        //}
    };
}
