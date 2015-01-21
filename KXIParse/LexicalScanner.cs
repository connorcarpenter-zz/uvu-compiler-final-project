using System;
using System.Text.RegularExpressions;

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

        public void NextToken()
        {
            LoadNextLine();

            //get rid of whitespace and next line symbols, continue
            currentLine = Regex.Replace(currentLine, "^[\\s#N#]+", "");

            LoadNextLine();

            var DONE = false;
            foreach (var tokenData in TokenDictionary.Get())
            {
                if (tokenData.Key == TokenType.Unknown) continue;
                //if (tokenData.Key == TokenType.EOT) continue;

                var value = Regex.Match(currentLine, tokenData.Value).Value;
                if (!string.IsNullOrEmpty(value))
                {
                    currentLine = currentLine.Remove(0, value.Length);
                    value = value.Replace("#N#", "");
                    currentToken = new Token(tokenData.Key, value);
                    DONE=true;
                    break;
                }
            }
            if(DONE)return;
            
            var firstChar = ""+currentLine[0];
            currentLine = currentLine.Remove(0,1);
            if(currentToken.Type==TokenType.Unknown)
                currentToken.Value+=firstChar;
            else
                currentToken = new Token(TokenType.Unknown,firstChar);

            //get rid of comments, if so NEXT

            //get identifier, if so NEXT

            //get number, if so NEXT

            //get end of line symbol (as EOT), if so NEXT

            //get any of the literal token types, if so NEXT

            //continue to go forward until reaching whitespace, label all that as an unknown
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
