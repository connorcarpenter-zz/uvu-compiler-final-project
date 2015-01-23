using System;

namespace KXIParse
{
    class Program
    {
        static void Main()
        {
            var lexer = new Lexer("../../program.kxi");
            var tokenList = lexer.GenerateTokenList();
           
            foreach(var t in tokenList)
                Console.WriteLine("" + t.LineNumber + ": " + t.Type + ": " + t.Value);

            Console.ReadLine();
        }
    }
}
