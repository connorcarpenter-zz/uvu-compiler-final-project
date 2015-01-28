using System;
using System.Collections.Generic;

namespace KXIParse
{
    class Program
    {
        static void Main()
        {
            try
            {
                var lexer = new Lexer("../../program.kxi");
                var tokens = lexer.GenerateTokens();
                //PrintTokenList(tokens);
                var syntaxer = new Syntaxer(tokens);
                syntaxer.ParseTokens();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            Console.ReadLine();
        }

        static void PrintTokenList(IEnumerable<Token> tokenList)
        {
            foreach (var t in tokenList)
                Console.WriteLine("" + t.LineNumber + ": " + t.Type + ": " + t.Value);
        }
    }
}
