using System.Globalization;

namespace JetReportDesigner.Core.Binding;

/// <summary>Thrown when an expression cannot be parsed or evaluated.</summary>
public sealed class ExpressionException(string message) : Exception(message);

/// <summary>
/// A small expression language for element values prefixed with <c>=</c>. Supports
/// numbers, single-quoted strings, <c>true/false/null</c>, field references
/// (<c>source.field</c> or bare <c>field</c>), <c>param.name</c>, arithmetic
/// (<c>+ - * / %</c>), comparison, <c>and/or/not</c>, parentheses, and the
/// functions <c>if</c>, <c>coalesce</c>, <c>format</c>, <c>upper</c>, <c>lower</c>,
/// <c>len</c>, <c>pageNumber</c>, <c>totalPages</c>, <c>now</c>. Aggregates stay on
/// the element (<c>aggregate</c>/<c>aggregateScope</c>), not here.
/// </summary>
public static class ExpressionEvaluator
{
    public static bool IsExpression(string? value) =>
        value is not null && value.Length > 1 && value[0] == '=';

    public static object? Evaluate(string expression, BindingContext context)
    {
        var body = expression.StartsWith('=') ? expression[1..] : expression;
        var parser = new Parser(new Lexer(body).Tokenize(), context);
        var result = parser.ParseExpression();
        parser.Expect(TokenType.End);
        return result;
    }

    // ---------------- lexer ----------------

    private enum TokenType
    {
        Number, String, Identifier, Operator, LParen, RParen, Comma, End,
    }

    private readonly record struct Token(TokenType Type, string Text);

    private sealed class Lexer(string source)
    {
        private int _pos;

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_pos < source.Length)
            {
                var c = source[_pos];
                if (char.IsWhiteSpace(c))
                {
                    _pos++;
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && _pos + 1 < source.Length && char.IsDigit(source[_pos + 1])))
                {
                    tokens.Add(new Token(TokenType.Number, ReadWhile(ch => char.IsDigit(ch) || ch == '.')));
                }
                else if (c == '\'')
                {
                    tokens.Add(new Token(TokenType.String, ReadString()));
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    tokens.Add(new Token(TokenType.Identifier, ReadWhile(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.')));
                }
                else if (c == '(')
                {
                    _pos++;
                    tokens.Add(new Token(TokenType.LParen, "("));
                }
                else if (c == ')')
                {
                    _pos++;
                    tokens.Add(new Token(TokenType.RParen, ")"));
                }
                else if (c == ',')
                {
                    _pos++;
                    tokens.Add(new Token(TokenType.Comma, ","));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Operator, ReadOperator()));
                }
            }

            tokens.Add(new Token(TokenType.End, string.Empty));
            return tokens;
        }

        private string ReadWhile(Func<char, bool> predicate)
        {
            var start = _pos;
            while (_pos < source.Length && predicate(source[_pos]))
            {
                _pos++;
            }

            return source[start.._pos];
        }

        private string ReadString()
        {
            _pos++; // opening quote
            var sb = new System.Text.StringBuilder();
            while (_pos < source.Length && source[_pos] != '\'')
            {
                if (source[_pos] == '\\' && _pos + 1 < source.Length)
                {
                    _pos++;
                }

                sb.Append(source[_pos++]);
            }

            if (_pos >= source.Length)
            {
                throw new ExpressionException("Unterminated string literal.");
            }

            _pos++; // closing quote
            return sb.ToString();
        }

        private string ReadOperator()
        {
            foreach (var op in new[] { "<=", ">=", "!=", "<>", "==" })
            {
                if (source.AsSpan(_pos).StartsWith(op))
                {
                    _pos += 2;
                    return op;
                }
            }

            return source[_pos++].ToString();
        }
    }

    // ---------------- parser ----------------

    private sealed class Parser(List<Token> tokens, BindingContext context)
    {
        private int _index;

        private Token Current => tokens[_index];

        public void Expect(TokenType type)
        {
            if (Current.Type != type)
            {
                throw new ExpressionException($"Expected {type} but found '{Current.Text}'.");
            }

            _index++;
        }

        public object? ParseExpression() => ParseOr();

        private object? ParseOr()
        {
            var left = ParseAnd();
            while (IsKeyword("or"))
            {
                _index++;
                var right = ParseAnd();
                left = ToBool(left) || ToBool(right);
            }

            return left;
        }

        private object? ParseAnd()
        {
            var left = ParseComparison();
            while (IsKeyword("and"))
            {
                _index++;
                var right = ParseComparison();
                left = ToBool(left) && ToBool(right);
            }

            return left;
        }

        private object? ParseComparison()
        {
            var left = ParseAdditive();
            while (Current.Type == TokenType.Operator && Current.Text is "=" or "==" or "!=" or "<>" or "<" or "<=" or ">" or ">=")
            {
                var op = Current.Text;
                _index++;
                var right = ParseAdditive();
                left = Compare(op, left, right);
            }

            return left;
        }

        private object? ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (Current.Type == TokenType.Operator && Current.Text is "+" or "-")
            {
                var op = Current.Text;
                _index++;
                var right = ParseMultiplicative();
                left = op == "+" ? Add(left, right) : ToNumber(left) - ToNumber(right);
            }

            return left;
        }

        private object? ParseMultiplicative()
        {
            var left = ParseUnary();
            while (Current.Type == TokenType.Operator && Current.Text is "*" or "/" or "%")
            {
                var op = Current.Text;
                _index++;
                var right = ToNumber(ParseUnary());
                var l = ToNumber(left);
                left = op switch { "*" => l * right, "/" => l / right, _ => l % right };
            }

            return left;
        }

        private object? ParseUnary()
        {
            if (Current.Type == TokenType.Operator && Current.Text == "-")
            {
                _index++;
                return -ToNumber(ParseUnary());
            }

            if (IsKeyword("not"))
            {
                _index++;
                return !ToBool(ParseUnary());
            }

            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            var token = Current;
            switch (token.Type)
            {
                case TokenType.Number:
                    _index++;
                    return double.Parse(token.Text, CultureInfo.InvariantCulture);

                case TokenType.String:
                    _index++;
                    return token.Text;

                case TokenType.LParen:
                    _index++;
                    var inner = ParseExpression();
                    Expect(TokenType.RParen);
                    return inner;

                case TokenType.Identifier:
                    _index++;
                    if (Current.Type == TokenType.LParen)
                    {
                        return CallFunction(token.Text, ParseArguments());
                    }

                    return ResolveIdentifier(token.Text);

                default:
                    throw new ExpressionException($"Unexpected token '{token.Text}'.");
            }
        }

        private List<object?> ParseArguments()
        {
            Expect(TokenType.LParen);
            var args = new List<object?>();
            if (Current.Type != TokenType.RParen)
            {
                args.Add(ParseExpression());
                while (Current.Type == TokenType.Comma)
                {
                    _index++;
                    args.Add(ParseExpression());
                }
            }

            Expect(TokenType.RParen);
            return args;
        }

        private object? ResolveIdentifier(string name)
        {
            switch (name)
            {
                case "true": return true;
                case "false": return false;
                case "null": return null;
            }

            if (name.StartsWith("param.", StringComparison.Ordinal))
            {
                return context.Parameters.TryGetValue(name["param.".Length..], out var p) ? p : null;
            }

            var field = name.Contains('.') ? name[(name.IndexOf('.') + 1)..] : name;
            return context.Row.TryGetValue(field, out var value) ? value : null;
        }

        private object? CallFunction(string name, List<object?> args) => name.ToLowerInvariant() switch
        {
            "if" or "iif" => ToBool(args[0]) ? args[1] : args[2],
            "coalesce" => args.FirstOrDefault(a => a is not null),
            "format" => BindingResolver.FormatValue(args[0], Convert.ToString(args.ElementAtOrDefault(1), CultureInfo.InvariantCulture)),
            "upper" => Convert.ToString(args[0], CultureInfo.CurrentCulture)?.ToUpperInvariant(),
            "lower" => Convert.ToString(args[0], CultureInfo.CurrentCulture)?.ToLowerInvariant(),
            "len" => (double)(Convert.ToString(args[0], CultureInfo.CurrentCulture)?.Length ?? 0),
            "pagenumber" => (double)context.PageNumber,
            "totalpages" => (double)context.TotalPages,
            "now" => context.Now,
            _ => throw new ExpressionException($"Unknown function '{name}'."),
        };

        private bool IsKeyword(string keyword) =>
            Current.Type == TokenType.Identifier && string.Equals(Current.Text, keyword, StringComparison.OrdinalIgnoreCase);

        // ---- value helpers ----

        private static double ToNumber(object? value) => value switch
        {
            null => 0,
            double d => d,
            bool b => b ? 1 : 0,
            IConvertible c => c.ToDouble(CultureInfo.InvariantCulture),
            _ => double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0,
        };

        private static bool ToBool(object? value) => value switch
        {
            null => false,
            bool b => b,
            double d => d != 0,
            string s => !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };

        private static object Add(object? left, object? right) =>
            left is string || right is string
                ? Convert.ToString(left, CultureInfo.CurrentCulture) + Convert.ToString(right, CultureInfo.CurrentCulture)
                : ToNumber(left) + ToNumber(right);

        private static bool Compare(string op, object? left, object? right)
        {
            int cmp;
            if (left is string || right is string)
            {
                cmp = string.Compare(
                    Convert.ToString(left, CultureInfo.InvariantCulture),
                    Convert.ToString(right, CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
            }
            else
            {
                cmp = ToNumber(left).CompareTo(ToNumber(right));
            }

            return op switch
            {
                "=" or "==" => cmp == 0,
                "!=" or "<>" => cmp != 0,
                "<" => cmp < 0,
                "<=" => cmp <= 0,
                ">" => cmp > 0,
                ">=" => cmp >= 0,
                _ => throw new ExpressionException($"Unknown operator '{op}'."),
            };
        }
    }
}
