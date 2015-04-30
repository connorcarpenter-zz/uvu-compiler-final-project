using System;
using System.Collections.Generic;
using System.IO;

namespace KXIParse
{
    class Program
    {
        private const bool DEBUG = false;
        private static string lastModified = "";
        static void Main(string[] args)
        {
            var fileName = "";
            if (DEBUG)
            {
                fileName = "../../program.kxi";
            }
            else
            {
                fileName = args[0];
            }

            
            var done = false;
            while (!done)
            {
                if (File.Exists(fileName))
                {
                    var dt = File.GetLastWriteTime(fileName);
                    if (dt.ToString().Equals(lastModified))
                    {
                        System.Threading.Thread.Sleep(1000);
                        continue;
                    }
                    lastModified = dt.ToString();
                }

                var file = new System.IO.StreamReader(fileName);
                try
                {
                    var lexer = new Lexer(file);

                    var syntaxer = new Syntaxer(lexer);
                    var symbolTable = syntaxer.SyntaxPass();
                    if (false)
                    {
                        Console.WriteLine("Syntax pass is done");
                        Console.ReadLine();

                        PrintSymbolTable(symbolTable);
                        Console.WriteLine("Symbol table is done");
                        Console.ReadLine();
                    }

                    var icodeList = syntaxer.SemanticPass(symbolTable);
                    var symbolTable2 = Syntaxer._syntaxSymbolTable;
                    //Console.WriteLine("Semantics pass is done. Press enter to print final back-patched icode.");
                    //Console.ReadLine();

                    foreach (var q in icodeList)
                    {
                        if (q.Label.Length != 0)
                            Console.WriteLine();
                        Console.WriteLine(q.ToString());
                    }
                    Console.WriteLine("Finished with ICode generation\n");

                    var tarcoder = new Tarcoder(symbolTable2, icodeList);
                    var tcodeList = tarcoder.Generate();
                    foreach (var t in tcodeList)
                    {
                        Console.WriteLine(t.ToString());
                    }
                    Console.WriteLine("Finished with TCode generation\n");
                    var tcodestring = Tarcoder.TCodeString(tcodeList);

                    VMShell.Execute(tcodestring);

                    done = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                file.Close();
            }
        }

        static void PrintTokenList(IEnumerable<Token> tokenList)
        {
            foreach (var t in tokenList)
                Console.WriteLine("" + t.LineNumber + ": " + t.Type + ": " + t.Value);
        }


        static void PrintSymbolTable(Dictionary<string,Symbol> symbolTable)
        {
            Console.WriteLine("\nSymbol Table\n---\n");
 
            foreach (var s in symbolTable)
            {
                Console.WriteLine("" + s.Key + "\n" + "---");
                Console.WriteLine("Scope:   "+s.Value.Scope);
                Console.WriteLine("Symid:   " + s.Value.SymId);
                Console.WriteLine("Value:   " + s.Value.Value);
                Console.WriteLine("Kind:   " + s.Value.Kind);
                if (s.Value.Data != null)
                {
                    if (!string.IsNullOrEmpty(s.Value.Data.Type))
                        Console.WriteLine("Type:   " + s.Value.Data.Type + (s.Value.Data.IsArray ? "[]" : ""));
                    if (s.Value.Data.Params != null)
                        foreach (var p in s.Value.Data.Params)
                            Console.Write("Param:   " + p);
                    if (!string.IsNullOrEmpty(s.Value.Data.AccessMod))
                        Console.WriteLine("AccessMod:   " + s.Value.Data.AccessMod);
                }
                Console.WriteLine();
            }
        }
    }
}
