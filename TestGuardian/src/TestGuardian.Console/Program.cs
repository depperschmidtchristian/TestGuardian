// See https://aka.ms/new-console-template for more information


using TestGuardian.Core;

void Main()
{
    ITestGuardianCore testGuardianCore = new TestGuardianCore();

    while (true)
    {
        testGuardianCore.PrintTest("Hello");
    }
}

Main();

