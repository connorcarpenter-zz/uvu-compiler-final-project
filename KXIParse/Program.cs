using System;
using System.Collections.Generic;

namespace KXIParse
{
    class Program
    {
        static void Main()
        {
            const string fileName = "../../program.kxi";

            var tokenList = new List<Token>();
            var lexicalScanner = new LexicalScanner(fileName);
            var lastToken = lexicalScanner.GetToken();

            while (true)
            {
                lexicalScanner.NextToken();
                var currentToken = lexicalScanner.GetToken();
                if (currentToken.Type == TokenType.EOT)
                    break;
                if (currentToken != lastToken)
                {
                    tokenList.Add(currentToken);
                    lastToken = currentToken;
                }
            }

            for (var i = 0; i < tokenList.Count; i++)
                Console.WriteLine("" + i + ": " + tokenList[i].Type + ": " + tokenList[i].Value);

            Console.ReadLine();
        }
    }
}
