using System;
using System.Text.RegularExpressions;

namespace KXIParse
{
    class LexicalScanner : ILexicalScanner
    {
        private Token currentToken;
        private readonly System.IO.StreamReader file;
        private string currentLine;
        private int lineNumber;

        public LexicalScanner(string fileName)
        {
            file = new System.IO.StreamReader(fileName);
            currentLine = file.ReadLine();
            currentToken = null;
            lineNumber = 1;
        }

        public Token GetToken()
        {
            return currentToken;
        }

        public void NextToken()
        {
            LoadNextLine();

            //get rid of whitespace and next line symbols, continue
            while (true)
            {
                var whitespace = Regex.Match(currentLine, "^(\\s)*(#N#)?").Value;
                if (whitespace.Length > 0)
                {
                    if (whitespace.Contains("#N#"))
                        lineNumber++;
                    currentLine = currentLine.Remove(0, whitespace.Length);
                }
                else
                    break;
                LoadNextLine();
            }

            //we are now at the first character of a real symbol number

            var DONE = false;
            foreach (var tokenData in TokenDictionary.Get())
            {
                if (tokenData.Key == TokenType.Unknown) continue;

                var value = Regex.Match(currentLine, tokenData.Value).Value;
                if (!string.IsNullOrEmpty(value))
                {
                    currentLine = currentLine.Remove(0, value.Length);
                    if(value.Contains("#N#"))
                        value = value.Replace("#N#", "");
                    currentToken = new Token(tokenData.Key, value,lineNumber);
                    DONE=true;
                    break;
                }
            }
            if(DONE)return;
            
            //If you have not found an acceptable character log it as Unknown
            var firstChar = ""+currentLine[0];
            currentLine = currentLine.Remove(0,1);
            if(currentToken!=null && currentToken.Type==TokenType.Unknown)
                currentToken.Value+=firstChar;
            else
                currentToken = new Token(TokenType.Unknown,firstChar,lineNumber);
        }

        private void LoadNextLine()
        {
            while (currentLine.Length < 80)
            {
                var nextLine = file.ReadLine();
                if (nextLine == null)
                {
                    currentLine += "#E#";
                    break;
                }
                currentLine += "#N#" + nextLine;
            }
        }
    }
}
