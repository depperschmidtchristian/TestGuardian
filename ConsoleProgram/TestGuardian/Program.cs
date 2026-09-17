// See https://aka.ms/new-console-template for more information
using TestGuardian.Input;

void Main()
{
    while (true)
    {
        
        string userInput = Console.ReadLine();

        if (userInput == null)
        {
            break;
        }
        ITxrParser parser = new TxrParser();
        parser.ReadTxrFile(userInput);
    }
}

Main();