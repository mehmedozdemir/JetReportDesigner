using System.Globalization;
using System.Text.RegularExpressions;

namespace JetReportDesigner.Core.Binding;

/// <summary>Thrown when an expression cannot be parsed or evaluated.</summary>
public sealed class ExpressionException(string message) : Exception(message);

/// <summary>
/// A small expression language for element values. An expression is either prefixed
/// with <c>=</c> or is a bare call to a known function (e.g. <c>now()</c>,
/// <c>sum(orders.total)</c>). Supports numbers, single-quoted strings,
/// <c>true/false/null</c>, field references (<c>source.field</c> or bare
/// <c>field</c>), <c>param.name</c>, arithmetic, comparison, <c>and/or/not</c>,
/// parentheses, scalar functions and scope-aware aggregates.
/// </summary>
public static partial class ExpressionEvaluator
{
    /// <summary>Scalar functions (arguments are evaluated before the call).</summary>
    private static readonly HashSet<string> ScalarFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "iif", "coalesce",
        "format", "upper", "lower", "len", "trim", "left", "right", "substring", "replace", "contains",
        "abs", "round", "floor", "ceiling", "ceil", "sqrt", "pow", "sign", "trunc", "mod",
        "now", "today", "year", "month", "day", "adddays",
        "pagenumber", "totalpages", "rownumber", "totalrows",
    };

    /// <summary>Aggregates that iterate the current band's scope rows (argument is lazy).</summary>
    private static readonly HashSet<string> AggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sum", "avg", "average", "count", "min", "max", "first", "last",
    };

    [GeneratedRegex(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*\(")]
    private static partial Regex BareCallRegex();

    public static bool IsExpression(string? value)
    {
        if (value is null || value.Length < 2)
        {
            return false;
        }

        if (value[0] == '=')
        {
            return true;
        }

        var m = BareCallRegex().Match(value);
        return m.Success
            && (ScalarFunctions.Contains(m.Groups[1].Value) || AggregateFunctions.Contains(m.Groups[1].Value));
    }

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
                        return AggregateFunctions.Contains(token.Text)
                            ? CallAggregate(token.Text)
                            : CallFunction(token.Text, ParseArguments());
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

        /// <summary>
        /// An aggregate: re-evaluates its single argument for every row in
        /// <see cref="BindingContext.AggregateRows"/>. <c>min</c>/<c>max</c> with two or
        /// more arguments fall back to a scalar minimum/maximum.
        /// </summary>
        private object? CallAggregate(string name)
        {
            var lname = name.ToLowerInvariant();
            Expect(TokenType.LParen);

            if (Current.Type == TokenType.RParen)
            {
                _index++;
                return lname == "count" ? (double)context.AggregateRows.Count : 0d;
            }

            var start = _index;
            var currentRowValue = ParseExpression();

            if (Current.Type == TokenType.Comma)
            {
                var values = new List<object?> { currentRowValue };
                while (Current.Type == TokenType.Comma)
                {
                    _index++;
                    values.Add(ParseExpression());
                }

                Expect(TokenType.RParen);
                return lname switch
                {
                    "min" => values.Select(ToNumber).Min(),
                    "max" => values.Select(ToNumber).Max(),
                    _ => throw new ExpressionException($"'{lname}' takes a single argument."),
                };
            }

            var argTokens = tokens.GetRange(start, _index - start);
            argTokens.Add(new Token(TokenType.End, string.Empty));
            Expect(TokenType.RParen);

            var rows = context.AggregateRows;
            var evaluated = new List<object?>(rows.Count);
            foreach (var r in rows)
            {
                evaluated.Add(new Parser(argTokens, context.WithRow(r)).ParseExpression());
            }

            return lname switch
            {
                "sum" => evaluated.Sum(ToNumber),
                "avg" or "average" => evaluated.Count == 0 ? 0d : evaluated.Average(ToNumber),
                "count" => (double)evaluated.Count(v => v is not null && v is not string { Length: 0 }),
                "min" => evaluated.Where(v => v is not null).Select(ToNumber).DefaultIfEmpty().Min(),
                "max" => evaluated.Where(v => v is not null).Select(ToNumber).DefaultIfEmpty().Max(),
                "first" => evaluated.Count > 0 ? evaluated[0] : null,
                "last" => evaluated.Count > 0 ? evaluated[^1] : null,
                _ => throw new ExpressionException($"Unknown aggregate '{name}'."),
            };
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

        private object? CallFunction(string name, List<object?> args)
        {
            string Str(int i) => Convert.ToString(args.ElementAtOrDefault(i), context.Culture) ?? string.Empty;
            double Nm(int i) => ToNumber(args.ElementAtOrDefault(i));
            DateTime Dt(int i) => args.ElementAtOrDefault(i) switch
            {
                DateTime d => d,
                DateTimeOffset o => o.DateTime,
                string s when DateTime.TryParse(s, context.Culture, DateTimeStyles.None, out var d) => d,
                _ => context.Now,
            };

            return name.ToLowerInvariant() switch
            {
                "if" or "iif" => ToBool(args[0]) ? args[1] : args[2],
                "coalesce" => args.FirstOrDefault(a => a is not null),
                "format" => BindingResolver.FormatValue(args[0], Convert.ToString(args.ElementAtOrDefault(1), CultureInfo.InvariantCulture), context.Culture),

                "upper" => Str(0).ToUpper(context.Culture),
                "lower" => Str(0).ToLower(context.Culture),
                "trim" => Str(0).Trim(),
                "len" => (double)Str(0).Length,
                "left" => Slice(Str(0), 0, (int)Nm(1)),
                "right" => Slice(Str(0), Math.Max(0, Str(0).Length - (int)Nm(1)), (int)Nm(1)),
                "substring" => args.Count >= 3
                    ? Slice(Str(0), (int)Nm(1), (int)Nm(2))
                    : Slice(Str(0), (int)Nm(1), Str(0).Length),
                "replace" => Str(0).Replace(Str(1), Str(2), StringComparison.Ordinal),
                "contains" => Str(0).Contains(Str(1), StringComparison.OrdinalIgnoreCase),

                "abs" => Math.Abs(Nm(0)),
                "round" => Math.Round(Nm(0), args.Count >= 2 ? (int)Nm(1) : 0, MidpointRounding.AwayFromZero),
                "floor" => Math.Floor(Nm(0)),
                "ceiling" or "ceil" => Math.Ceiling(Nm(0)),
                "sqrt" => Math.Sqrt(Nm(0)),
                "pow" => Math.Pow(Nm(0), Nm(1)),
                "sign" => (double)Math.Sign(Nm(0)),
                "trunc" => Math.Truncate(Nm(0)),
                "mod" => Nm(1) == 0 ? 0d : Nm(0) % Nm(1),

                "now" => context.Now,
                "today" => context.Now.Date,
                "year" => (double)Dt(0).Year,
                "month" => (double)Dt(0).Month,
                "day" => (double)Dt(0).Day,
                "adddays" => Dt(0).AddDays(Nm(1)),

                "pagenumber" => (double)context.PageNumber,
                "totalpages" => (double)context.TotalPages,
                "rownumber" => (double)context.RowNumber,
                "totalrows" => (double)context.TotalRows,

                _ => throw new ExpressionException($"Unknown function '{name}'."),
            };
        }

        private static string Slice(string s, int start, int length)
        {
            if (start < 0)
            {
                start = 0;
            }

            if (start >= s.Length || length <= 0)
            {
                return string.Empty;
            }

            return s.Substring(start, Math.Min(length, s.Length - start));
        }

        private bool IsKeyword(string keyword) =>
            Current.Type == TokenType.Identifier && string.Equals(Current.Text, keyword, StringComparison.OrdinalIgnoreCase);

        // ---- value helpers ----

        private static double ToNumber(object? value) => value switch
        {
            null => 0,
            double d => d,
            bool b => b ? 1 : 0,
            DateTime dt => dt.ToOADate(),
            IConvertible c => SafeToDouble(c),
            _ => double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0,
        };

        private static double SafeToDouble(IConvertible c)
        {
            try
            {
                return c.ToDouble(CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return 0;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
        }

        private static bool ToBool(object? value) => value switch
        {
            null => false,
            bool b => b,
            double d => d != 0,
            string s => !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };

        private object Add(object? left, object? right) =>
            left is string || right is string
                ? Convert.ToString(left, context.Culture) + Convert.ToString(right, context.Culture)
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
