namespace TestGuardian.Core
{

    public interface ITestGuardianCore
    {
        void PrintTest(string message);
    }

    public class TestGuardianCore : ITestGuardianCore
    {

        public void PrintTest(string message) { 
            Console.WriteLine(message);
        }
    }
}
