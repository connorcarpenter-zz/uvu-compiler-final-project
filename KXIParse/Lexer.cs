using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace KXIParse
{
    internal interface ILexer
    {
        Token GetToken();
        void NextToken();
        Token PeekToken();
    }

    class Lexer : ILexer
    {
        private Token _currentToken;
        private readonly System.IO.StreamReader _file;
        private List<Token> tokenList;
        private string _currentLine;
        private int _lineNumber;
        private const int IdentifierMaxLength = 80;
        private const string Newline = "#N#";
        private const string Endline = "#E#";
        private const string WhitespaceRegex = "^(\\s)*(" + Newline + ")?";

        public Lexer(System.IO.StreamReader file)
        {
            _file = file;
            _currentLine = _file.ReadLine();
            _currentToken = null;
            _lineNumber = 1;
            tokenList = new List<Token>();
            NextToken();
        }

        public Token GetToken()
        {
            return _currentToken;
        }

        public Token PeekToken()
        {
            return tokenList[1];
        }

        public void NextToken()
        {
            if(tokenList.Count>0)
                tokenList.RemoveAt(0);
            while (tokenList.Count() < 4)
            {
                GenerateTokens();
            }

            _currentToken = tokenList[0];
        }

        private void GenerateTokens()
        {
            var lastToken = GetToken(); // :)

            var currentToken = GenerateToken();
            if (currentToken.Type == TokenType.EOT)
                tokenList.Add(null);
            if (currentToken != lastToken)
                PostProcess(currentToken); //adds to tokenlist;
        }

        public Token GenerateToken()
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
            foreach (var tokenData in TokenData.Get())
            {
                if (tokenData.Key == TokenType.Unknown) continue;

                var value = Regex.Match(_currentLine, tokenData.Value.Regex).Value;
                if (!string.IsNullOrEmpty(value))
                {
                    _currentLine = _currentLine.Remove(0, value.Length);
                    if (value.Contains(Newline))
                        value = value.Replace(Newline, "");
                    return new Token(tokenData.Key, value, _lineNumber);
                }
            }

            //If you have not found an acceptable character log it as Unknown
            var firstChar = "" + _currentLine[0];
            _currentLine = _currentLine.Remove(0, 1);
            return new Token(TokenType.Unknown, firstChar, _lineNumber);
        }

        private void PostProcess(Token token)
        {
            //remove all comments
            if (token.Type == TokenType.Comment)
                return;

            //throw exception if there's any unknowns
            if(token.Type == TokenType.Unknown)
                throw new Exception(string.Format("Lexer Error: Unknown symbol on line {0}: {1}",
                                    token.LineNumber,
                                    token.Value));

            //turn back-to-back numbers into addition/subtraction statements
            if (tokenList.Count() >= 2)
            {
                Token l = null;
                for (var index = 0; index < tokenList.Count; index++)
                {
                    var t = tokenList[index];
                    if (l == null)
                    {
                        l = t;
                        continue;
                    }

                    if ((l.Type == TokenType.Number || l.Type == TokenType.Identifier) && t.Type == TokenType.Number &&
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
            }

            tokenList.Add(token);
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
