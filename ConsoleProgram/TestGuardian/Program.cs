// See https://aka.ms/new-console-template for more information

void Main()
{
    while (true)
    {
        string userInput = Console.ReadLine();
        if (userInput != null)
        {
            OutputName(userInput);
        }

    }
}

void OutputName(string input)
{
    Console.WriteLine(input);
}



Main();