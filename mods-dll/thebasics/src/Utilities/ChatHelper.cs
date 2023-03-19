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
        {
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
        };

        public static bool IsPunctuation(char character)
        {
            return Punctuation.Any(punctuation => character == punctuation);
        }

        private static readonly char[] Delimiter = //TODO: Come up with a better name for this
        {
            ' ',
            '\t',
            '\n',
            '\r',
        };

        public static bool IsDelimiter(char character)
        {
            return Delimiter.Any(punctuation => character == punctuation);
        }

        public static bool DoesMessageNeedPunctuation(string input)
        {
            if (input.Length == 0)
            {
                return false;
            }

            var lastCharacter = input[input.Length - 1];

            return !IsPunctuation(lastCharacter);
        }

        public static string Trim(string input)
        {
            return input.Trim();
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

        public static string LangColor(string message, Language lang)
        {
            return $"<font color=\"{lang.Color}\">{message}</font>";
        }

        public delegate void OnOffChatCommandDelegate(IServerPlayer player, int groupId, bool value);
        
        public delegate void PlayerTargetChatCommandDelegate(IServerPlayer player, int groupId, IServerPlayer targetPlayer);

        public delegate void FullStringChatCommandDelegate(IServerPlayer player, int groupId, string value);

        public static ServerChatCommandDelegate GetChatCommandFromOnOff(
            string command,
            OnOffChatCommandDelegate handler)
        {
            return (player, groupId, args) =>
            {
                if (args.Length != 1)
                {
                    player.SendMessage(groupId, "Usage: /" + command + " [on|off]", EnumChatType.CommandError);
                    return;
                }

                var value = args[0].ToLower();

                if (value != "on" && value != "off")
                {
                    player.SendMessage(groupId, "Usage: /" + command + " [on|off]", EnumChatType.CommandError);
                    return;
                }

                var boolValue = value == "on";

                handler(player, groupId, boolValue);
            };
        }
        
        public delegate void SingleNumberChatCommandDelegate(IServerPlayer player, int groupId, int value);
        
        public static ServerChatCommandDelegate GetChatCommandFromSingleNumber(
            string command,
            SingleNumberChatCommandDelegate handler)
        {
            return (player, groupId, args) =>
            {
                if (args.Length != 1)
                {
                    player.SendMessage(groupId, "Usage: /" + command + " [num]", EnumChatType.CommandError);
                    return;
                }

                var value = args[0];

                int result;
                var parseSuccess = int.TryParse(value, out result);

                if (!parseSuccess)
                {
                    player.SendMessage(groupId, "Usage: /" + command + " [num]", EnumChatType.CommandError);
                    return;
                }

                handler(player, groupId, result);
            };
        }
        
        public delegate void SingleStringChatCommandDelegate(IServerPlayer player, int groupId, string value);
        
        public static ServerChatCommandDelegate GetChatCommandFromSingleString(
            string command,
            SingleStringChatCommandDelegate handler)
        {
            return (player, groupId, args) =>
            {
                if (args.Length != 1)
                {
                    player.SendMessage(groupId, "Usage: /" + command + " [value]", EnumChatType.CommandError);
                    return;
                }
                
                var value = args.PopAll().Trim();
                
                handler(player, groupId, value);
            };
        }

        public static string GetMessage(string message)
        {
            var foundText = new Regex(@".*?> (.+)$").Match(message);

            return foundText.Groups[1].Value.Trim();
        }

        public static string GetStartUsageNotationForOptional(bool optional)
        {
            return optional ? "(" : "[";
        }
        public static string GetEndUsageNotationForOptional(bool optional)
        {
            return optional ? ")" : "]";
        }
        
        public static ServerChatCommandDelegate GetChatCommandFromPlayerTarget(
            string command,
            ICoreServerAPI api,
            PlayerTargetChatCommandDelegate handler, 
            bool optional = false)
        {
            return (player, groupId, args) =>
            {
                if (args.Length > 2 || (!optional && args.Length == 0))
                {
                    var usageString = "Usage: /" + command + " " + GetStartUsageNotationForOptional(optional) + "name" +
                                      GetEndUsageNotationForOptional(optional);
                    player.SendMessage(groupId, usageString, EnumChatType.CommandError);
                    return;
                }

                if (args.Length == 0)
                {
                    handler(player, groupId, null);
                    return;
                }

                var targetPlayer = api.GetPlayerByName(args[0]);

                if (targetPlayer == null)
                {
                    player.SendMessage(groupId, "Could not find target player", EnumChatType.CommandError);
                    return;
                }

                handler(player, groupId, targetPlayer);
            };
        }
    }
}