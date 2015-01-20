using System;

namespace KXIParse
{
    class LexicalScanner : ILexicalScanner
    {
        private Token currentToken;
        private readonly System.IO.StreamReader file;
        private string currentLine;

        public LexicalScanner(string fileName)
        {
            file = new System.IO.StreamReader(fileName);
            currentLine = file.ReadLine();
            NextToken();
        }

        public Token GetToken()
        {
            return currentToken;
        }

        private enum State { Begin, LookForWord}

        public void NextToken()
        {
            var value = GetNextChar();
            var state = State.Begin;

            var done = false;
            while (!done)
            {
                if(value== "")
                {
                    currentToken = new Token(TokenType.EOT, "");
                    done = true;
                    continue;
                }
                if (value == " ")
                {
                    value = GetNextChar();
                    continue;
                }
                if (Char.IsLetter(value[0]))
                {
                    switch (value)
                    {
                        case "atoi"
                    }
                }
            }
        }

        private string GetNextChar()
        {
            if(currentLine.Length==0)
                currentLine += file.ReadLine();
            if (currentLine.Length == 0) return "";
            var value = ""+currentLine[0];
            currentLine=currentLine.Remove(0, 1);
            return value;
        }
    }
}
