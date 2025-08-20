using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Utilities
{
    public static class ChatHelper
    {
        private static readonly char[] Punctuation =
        [
            '.',
            '!',
            '?',
            '~',
            '-',
            ';',
            ':',
            '/',
            ',',
            '"',
            '\'',
        ];

        public static bool IsPunctuation(char character)
        {
            return Punctuation.Any(punctuation => character == punctuation);
        }

        private static readonly char[] Whitespace =
        [
            ' ',
            '\t',
            '\n',
            '\r',
        ];

        public static bool IsWhitespace(char character)
        {
            return Whitespace.Any(punctuation => character == punctuation);
        }

        public static bool DoesMessageNeedPunctuation(string input)
        {
            if (input.Length == 0)
            {
                return false;
            }

            var lastCharacter = input[^1];

            return !IsPunctuation(lastCharacter);
        }

        public static string Strong(string input)
        {
            return WrapWithTag(input, "strong");
        }

        public static string Quote(string input)
        {
            var builder = new StringBuilder();

            builder.Append("\"");
            builder.Append(input);
            builder.Append("\"");

            return builder.ToString();
        }

        public static string Wrap(string input, string wrap)
        {
            var builder = new StringBuilder();

            builder.Append(wrap);
            builder.Append(input);
            builder.Append(wrap);

            return builder.ToString();
        }

        public static string WrapWithTag(string input, string tag)
        {
            var builder = new StringBuilder();

            builder.Append(GetTag(tag, TagPosition.Start));
            builder.Append(input);
            builder.Append(GetTag(tag, TagPosition.End));

            return builder.ToString();
        }

        public static string GetTag(string tag, TagPosition position)
        {
            var builder = new StringBuilder();
            builder.Append("<");
            if (position == TagPosition.End)
            {
                builder.Append("/");
            }

            builder.Append(tag);
            builder.Append(">");

            return builder.ToString();
        }

        public enum TagPosition
        {
            Start,
            End,
        }
        
        public static string OnOff(bool value)
        {
            return value ? "on" : "off";
        }

        public static string Build(params string[] values)
        {
            var builder = new StringBuilder();
            foreach (var value in values)
            {
                builder.Append(value);
            }

            return builder.ToString();
        }

        public static string Color(string message, string color)
        {
            if (string.IsNullOrEmpty(color))
                return message;
                
            return $"<font color=\"{color}\">{message}</font>";
        }
        
        public static string LangColor(string message, Language lang)
        {
            return Color(message, lang.Color);
        }

        // Escape user-provided nicknames to prevent VTML injection
        // Uses HTML entities so that players can still use < > & in their nicknames
        // These will be properly displayed in chat but won't break VTML parsing
        public static string EscapeMarkup(string input)
        {
            return VtmlUtils.EscapeVtml(input);
        }

        public static string LangIdentifier(Language lang)
        {
            return LangColor($"{lang.Name} (:{lang.Prefix})", lang);
        }
        
        public static string GetMessage(string message)
        {
            var foundText = new Regex(@".*?> (.+)$").Match(message);

            return foundText.Groups[1].Value.Trim();
        }

        public static string Italic(string input)
        {
            return WrapWithTag(input, "i");
        }
    }
}