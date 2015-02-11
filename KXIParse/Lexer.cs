using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KXIParse
{
    class Lexer
    {
        private Token _currentToken;
        private readonly System.IO.StreamReader _file;
        private string _currentLine;
        private int _lineNumber;
        private const int IdentifierMaxLength = 80;
        private const string Newline = "#N#";
        private const string Endline = "#E#";
        private const string WhitespaceRegex = "^(\\s)*(" + Newline + ")?";

        public Lexer(string fileName)
        {
            _file = new System.IO.StreamReader(fileName);
            _currentLine = _file.ReadLine();
            _currentToken = null;
            _lineNumber = 1;
        }
        public List<Token> GenerateTokens()
        {
            var tokenList = new List<Token>();
            var lastToken = GetToken();

            while (true)
            {
                NextToken();
                var currentToken = GetToken();
                if (currentToken.Type == TokenType.EOT)
                    break;
                if (currentToken == lastToken) continue;
                tokenList.Add(currentToken);
                lastToken = currentToken;
            }



            return PostProcess(tokenList);
        }

        public List<Token> PostProcess(List<Token> tokenList)
        {
            //remove all comments
            tokenList.RemoveAll(s => s.Type == TokenType.Comment);

            //throw exception if there's any unknowns
            foreach (var t in tokenList.Where(t => t.Type == TokenType.Unknown))
                throw new Exception(string.Format("Unknown symbol on line {0}: {1}",
                                    t.LineNumber,
                                    t.Value));

            //turn back-to-back numbers into addition/subtraction statements
            Token l = null;
            for (var index = 0; index < tokenList.Count; index++)
            {
                var t = tokenList[index];
                if (l == null)
                {
                    l = t;
                    continue;
                }

                if (l.Type == TokenType.Number && t.Type == TokenType.Number &&
                    (t.Value[0].Equals('+') || t.Value[0].Equals('-'))) 
                {
                    if (t.Value[0].Equals('+'))
                        tokenList.Insert(index, new Token(TokenType.Add, "+", t.LineNumber));
                    if (t.Value[0].Equals('-'))
                        tokenList.Insert(index, new Token(TokenType.Subtract, "-", t.LineNumber));
                    t.Value = t.Value.Remove(0, 1);
                }

                l = tokenList[index];
            }

            return tokenList;
        }

        private Token GetToken()
        {
            return _currentToken;
        }
        private void NextToken()
        {
            LoadNextLine();

            //get rid of whitespace and next line symbols, continue
            while (true)
            {
                var whitespace = Regex.Match(_currentLine, WhitespaceRegex).Value;
                if (whitespace.Length > 0)
                {
                    if (whitespace.Contains(Newline))
                        _lineNumber++;
                    _currentLine = _currentLine.Remove(0, whitespace.Length);
                }
                else
                    break;
                LoadNextLine();
            }

            //we are now at the first character of a real symbol number

            var done = false;
            foreach (var tokenData in TokenData.Get())
            {
                if (tokenData.Key == TokenType.Unknown) continue;

                var value = Regex.Match(_currentLine, tokenData.Value.Regex).Value;
                if (!string.IsNullOrEmpty(value))
                {
                    _currentLine = _currentLine.Remove(0, value.Length);
                    if (value.Contains(Newline))
                        value = value.Replace(Newline, "");
                    _currentToken = new Token(tokenData.Key, value, _lineNumber);
                    done = true;
                    break;
                }
            }
            if(done)return;
            
            //If you have not found an acceptable character log it as Unknown
            var firstChar = ""+_currentLine[0];
            _currentLine = _currentLine.Remove(0,1);
            if(_currentToken!=null && _currentToken.Type==TokenType.Unknown)
                _currentToken.Value+=firstChar;
            else
                _currentToken = new Token(TokenType.Unknown,firstChar,_lineNumber);
        }
        private void LoadNextLine()
        {
            while (_currentLine.Length < IdentifierMaxLength)
            {
                var nextLine = _file.ReadLine();
                if (nextLine == null)
                {
                    _currentLine += Endline;
                    break;
                }
                _currentLine += Newline + nextLine;
            }
        }
    }

    
}
