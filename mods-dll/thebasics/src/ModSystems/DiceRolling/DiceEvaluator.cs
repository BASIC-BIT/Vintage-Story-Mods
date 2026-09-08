using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace thebasics.ModSystems.DiceRolling;

/// <summary>A bounded expression parser. The complete request is parsed before any dice are drawn.</summary>
public static class DiceEvaluator
{
    public const int MaxInputLength = 512;
    public const int MaxTokens = 256;
    public const int MaxDepth = 32;
    public const int MaxDice = 100;
    public const int MaxSides = 1000000;
    public const int MaxWork = 2048;
    public const int MaxBreakdownLength = 4096;

    public static DiceRollResult EvaluateInput(string input, Func<int, int> rollDie = null)
    {
        if (string.IsNullOrWhiteSpace(input)) throw Error("syntax", "Enter a dice expression, such as 2d6+3.");
        if (input.Length > MaxInputLength) throw Limit();
        try
        {
            var parser = new Parser(input);
            var node = parser.Parse();
            var context = new Evaluation(rollDie ?? (sides => Random.Shared.Next(1, sides + 1)));
            var value = node(context);
            if (value.Text.Length > MaxBreakdownLength) throw Limit();
            int? simpleSides = Regex.IsMatch(parser.Expression, @"^d[0-9]+$", RegexOptions.IgnoreCase)
                ? int.Parse(parser.Expression.Substring(1), CultureInfo.InvariantCulture) : null;
            return new(parser.Expression, parser.Reason, value.Number, value.Success && !value.Ordinary,
                value.Text, simpleSides, parser.Mechanics.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }
        catch (OverflowException) { throw Error("overflow", "The expression exceeds the supported numeric range."); }
        catch (DivideByZeroException) { throw Error("division_by_zero", "A dice expression cannot divide by zero."); }
    }

    private static DiceRollException Error(string code, string message) => new(code, message);
    private static DiceRollException Syntax() => Error("syntax", "Invalid dice expression. Use # or // before a reason if its meaning is ambiguous.");
    private static DiceRollException Limit() => Error("limit", "Roll limit exceeded. Use a shorter expression or fewer dice.");
    private static string FormatNumber(decimal number) => number.ToString(CultureInfo.InvariantCulture);
    private sealed record Value(decimal Number, string Text, bool Success = false, bool Ordinary = false);
    private delegate Value Node(Evaluation context);

    private sealed class Evaluation(Func<int, int> rollDie)
    {
        private int draws;
        private int work;
        public void Step() { if (++work > MaxWork) throw Limit(); }
        public int Draw(int sides)
        {
            Step();
            if (++draws > MaxDice) throw Limit();
            var face = rollDie(sides);
            if (face < 1 || face > sides) throw Error("invalid_face", "The dice source returned an invalid face.");
            return face;
        }
    }

    private sealed class Parser(string input)
    {
        private int position;
        private int tokens;
        private int depth;
        private int declaredDice;
        public string Expression { get; private set; } = "";
        public string Reason { get; private set; } = "";
        public HashSet<string> Mechanics { get; } = new(StringComparer.Ordinal);
        private char Current => position < input.Length ? char.ToLowerInvariant(input[position]) : '\0';
        private void Spaces() { while (position < input.Length && char.IsWhiteSpace(input[position])) position++; }
        private bool Take(string text)
        {
            Spaces();
            if (!input.AsSpan(position).StartsWith(text.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
            position += text.Length;
            if (++tokens > MaxTokens) throw Limit();
            return true;
        }
        public Node Parse()
        {
            var node = Sum();
            int end = position;
            while (end > 0 && char.IsWhiteSpace(input[end - 1])) end--;
            Spaces();
            if (position < input.Length)
            {
                if (Take("#") || Take("//")) Reason = input.Substring(position).Trim();
                else if (position > end && char.IsLetter(Current) && !LooksLikeExpressionTail()) Reason = input.Substring(position).Trim();
                else throw Syntax();
            }
            Expression = input.Substring(0, end).Trim();
            return node;
        }
        private bool LooksLikeExpressionTail()
        {
            var tail = input.Substring(position);
            return Regex.IsMatch(tail, @"^(?:d(?:[\d.]|\s|$)|(?:kh|kl|dh|dl|ro|rr|r)(?:\d|[<>=])|(?:floor|ceil|round)\s*\(|[A-Za-z]+\s*\()", RegexOptions.IgnoreCase);
        }
        private Node Sum()
        {
            var left = Product();
            while (true)
            {
                if (Take("+")) left = Binary(left, Product(), '+');
                else if (Take("-")) left = Binary(left, Product(), '-');
                else return left;
            }
        }
        private Node Product()
        {
            var left = Unary();
            while (true)
            {
                Spaces();
                if (input.AsSpan(position).StartsWith("//".AsSpan(), StringComparison.Ordinal)) return left;
                if (Take("*")) left = Binary(left, Unary(), '*');
                else if (Take("/")) left = Binary(left, Unary(), '/');
                else return left;
            }
        }
        private Node Binary(Node left, Node right, char op)
        {
            Mechanics.Add("arithmetic");
            return context =>
            {
                context.Step();
                var a = left(context); var b = right(context);
                decimal value = op switch { '+' => a.Number + b.Number, '-' => a.Number - b.Number, '*' => a.Number * b.Number, '/' => a.Number / b.Number, _ => throw Syntax() };
                return new(value, "(" + a.Text + " " + op + " " + b.Text + ")", a.Success || b.Success, a.Ordinary || b.Ordinary);
            };
        }
        private Node Unary()
        {
            if (++depth > MaxDepth) throw Limit();
            try
            {
                if (Take("+")) return Unary();
                if (Take("-"))
                {
                    Mechanics.Add("arithmetic");
                    var child = Unary();
                    return context => { context.Step(); var value = child(context); return value with { Number = -value.Number, Text = "-(" + value.Text + ")" }; };
                }
                return Primary();
            }
            finally { depth--; }
        }
        private Node Primary()
        {
            if (Take("(")) { var child = Sum(); if (!Take(")")) throw Syntax(); return child; }
            foreach (var helper in new[] { "floor", "ceil", "round" })
            {
                if (!Take(helper)) continue;
                if (!Take("(")) throw Syntax();
                var child = Sum();
                if (!Take(")")) throw Syntax();
                Mechanics.Add(helper);
                return context =>
                {
                    context.Step(); var value = child(context);
                    return value with { Number = helper switch { "floor" => decimal.Floor(value.Number), "ceil" => decimal.Ceiling(value.Number), _ => decimal.Round(value.Number, 0, MidpointRounding.AwayFromZero) }, Text = helper + "(" + value.Text + ")" };
                };
            }
            if (Take("d")) return Dice(1, ReadNumber());
            var number = ReadNumber();
            if (ModifierAhead("d") && Take("d")) return Dice(number, ReadNumber());
            return context => { context.Step(); return new(number, FormatNumber(number)); };
        }
        private decimal ReadNumber()
        {
            Spaces(); int start = position;
            while (char.IsAsciiDigit(Current)) position++;
            if (Current == '.') { position++; while (char.IsAsciiDigit(Current)) position++; }
            if (start == position || !decimal.TryParse(input.AsSpan(start, position - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) throw Syntax();
            if (++tokens > MaxTokens) throw Limit();
            if (CanonicalNumber(input.Substring(start, position - start)) != CanonicalNumber(FormatNumber(number)))
                throw Error("precision", "A number has more precision than the dice evaluator supports.");
            return number;
        }
        private static string CanonicalNumber(string literal)
        {
            var normalized = literal.TrimStart('0');
            return normalized.Contains('.') ? normalized.TrimEnd('0').TrimEnd('.') : normalized;
        }
        private Node Dice(decimal countValue, decimal sidesValue)
        {
            var (count, sides) = ValidateDiceDimensions(countValue, sidesValue);
            Mechanics.Add("dice");
            bool explode = false, recursive = false;
            Func<int, bool> reroll = null, success = null;
            string selection = null;
            int selectionCount = 0;
            while (true)
            {
                Spaces();
                if (Take("!"))
                {
                    if (explode) throw Syntax();
                    explode = true; Mechanics.Add("explode");
                }
                else if (ModifierAhead("rr") || ModifierAhead("ro") || ModifierAhead("r"))
                {
                    if (reroll != null) throw Syntax();
                    recursive = Take("rr");
                    if (!recursive && !Take("ro")) Take("r");
                    reroll = Comparison(true); Mechanics.Add("reroll");
                }
                else if (ModifierAhead("kh") || ModifierAhead("kl") || ModifierAhead("dh") || ModifierAhead("dl"))
                {
                    if (selection != null) throw Syntax();
                    selection = input.Substring(position, 2).ToLowerInvariant();
                    Take(selection);
                    selectionCount = ReadSelectionCount(); Mechanics.Add("keep_drop");
                }
                else if (Current is '<' or '>' or '=')
                {
                    if (success != null) throw Syntax();
                    success = Comparison(false); Mechanics.Add("success_pool");
                }
                else break;
            }
            // Conditions matching every possible face cannot terminate. Reject before drawing.
            if (!explode && selection != null && selectionCount > count) throw Error("dimensions", "Keep/drop count exceeds the available dice.");
            if ((explode && sides == 1) || (recursive && MatchesAll(reroll, sides))) throw Limit();
            return context => RollPool(context, count, sides, explode, reroll, recursive, selection, selectionCount, success);
        }
        private (int Count, int Sides) ValidateDiceDimensions(decimal countValue, decimal sidesValue)
        {
            if (countValue <= 0 || sidesValue <= 0 || countValue != decimal.Truncate(countValue) || sidesValue != decimal.Truncate(sidesValue))
                throw Error("dimensions", "Dice counts and sides must be positive whole numbers.");
            if (countValue > MaxDice || sidesValue > MaxSides) throw Limit();
            int count = (int)countValue, sides = (int)sidesValue;
            if ((declaredDice += count) > MaxDice) throw Limit();
            return (count, sides);
        }
        private int ReadSelectionCount()
        {
            var amount = ReadNumber();
            if (amount < 0 || amount != decimal.Truncate(amount) || amount > MaxDice)
                throw Error("dimensions", "Keep/drop counts must be whole numbers from 0 to 100.");
            return (int)amount;
        }
        private bool ModifierAhead(string modifier)
        {
            Spaces();
            if (!input.AsSpan(position).StartsWith(modifier.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
            int next = position + modifier.Length;
            return next == input.Length || !char.IsLetter(input[next]);
        }
        private Func<int, bool> Comparison(bool allowImplicitEquality)
        {
            string op;
            if (Take(">=")) op = ">=";
            else if (Take("<=")) op = "<=";
            else if (Take(">")) op = ">";
            else if (Take("<")) op = "<";
            else if (Take("=")) op = "=";
            else if (allowImplicitEquality) op = "=";
            else throw Syntax();
            var threshold = ReadNumber();
            return face => op switch { ">=" => face >= threshold, "<=" => face <= threshold, ">" => face > threshold, "<" => face < threshold, _ => face == threshold };
        }
        private static bool MatchesAll(Func<int, bool> condition, int sides) => condition(1) && condition(sides);

        private sealed class Face(int number, bool generated = false)
        {
            public int Number { get; } = number;
            public bool Generated { get; } = generated;
            public bool Replaced { get; set; }
            public bool Dropped { get; set; }
        }
        private static Value RollPool(Evaluation context, int count, int sides, bool explode,
            Func<int, bool> reroll, bool recursive, string selection, int selectionCount, Func<int, bool> success)
        {
            var faces = Enumerable.Range(0, count).Select(_ => new Face(context.Draw(sides))).ToList();
            if (explode)
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    context.Step();
                    if (faces[i].Number == sides) faces.Add(new Face(context.Draw(sides), true));
                }
            }
            if (reroll != null)
            {
                // Snapshot the eligible pool: generated replacement faces are revisited only for rr.
                foreach (var original in faces.ToArray())
                {
                    var face = original;
                    while (reroll(face.Number))
                    {
                        context.Step(); face.Replaced = true;
                        face = new Face(context.Draw(sides), true);
                        faces.Add(face);
                        if (!recursive) break;
                    }
                }
            }
            var retained = faces.Where(face => !face.Replaced).ToList();
            if (selection != null)
            {
                if (selectionCount > retained.Count) throw Error("dimensions", "Keep/drop count exceeds the available dice.");
                var sorted = selection.EndsWith("h", StringComparison.Ordinal)
                    ? retained.OrderByDescending(face => face.Number) : retained.OrderBy(face => face.Number);
                var selected = sorted.Take(selectionCount).ToHashSet();
                foreach (var face in retained)
                {
                    context.Step();
                    face.Dropped = selection.StartsWith("k", StringComparison.Ordinal) ? !selected.Contains(face) : selected.Contains(face);
                }
            }
            decimal total = 0;
            var descriptions = new List<string>();
            foreach (var face in faces)
            {
                context.Step();
                string flags = face.Generated ? " generated" : "";
                if (face.Replaced) flags += " replaced";
                else if (face.Dropped) flags += " dropped";
                else
                {
                    total += success == null ? face.Number : success(face.Number) ? 1 : 0;
                    flags += success == null ? " kept" : success(face.Number) ? " success" : " miss";
                }
                descriptions.Add(face.Number.ToString(CultureInfo.InvariantCulture) + flags);
            }
            return new(total, "[" + string.Join(", ", descriptions) + "]", success != null, success == null);
        }
    }
}
