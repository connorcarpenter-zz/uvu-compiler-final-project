using System;

namespace KXIParse
{
    class Program
    {
        static void Main()
        {
            const string fileName = "../../program.kxi";

            var lexicalScanner = new LexicalScanner(fileName);
            while (true)
            {
                var currentToken = lexicalScanner.GetToken();
                if (currentToken.Type == TokenType.EOT)
                    break;
                Console.WriteLine(currentToken.Value+": "+currentToken.Type);
                lexicalScanner.NextToken();
            }
        }
    }
}
