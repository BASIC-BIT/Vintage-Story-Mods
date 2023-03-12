using System;
using System.Linq;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat
{
    public class TheStringSlingingScrambler
    {
        private Random _random;

        public TheStringSlingingScrambler()
        {
            _random = new Random();
        }

        public string ScrambleMessage(string message, Language language)
        {
            var random = new Random(GetWordHash(message));
            return string.Join(" ", message
                .Split(' ')
                .Select(word => word.Trim())
                .Select(GetSyllableCount)
                .Select(syllables =>
                    string.Join("",
                        syllables.DoTimes(
                            _ => language.Syllables.GetRandomElement(random)))));
        }

        private int GetSyllableCount(string word)
        {
            return (int)((word.Length / 2.0) +
                         (_random.Next(
                             (int)Math.Round(word.Length / 2.0))
                                          - 
                                          Math.Round(word.Length / 4.0)));
        }

        private int GetWordHash(string word)
        {
            return word.Select(character => (int)character).Aggregate((acc, cur) => acc + cur);
        }

        private char RandomChar(char original)
        {
            if (ChatHelper.IsPunctuation(original))
            {
                return original;
            }

            // ascii ranges of usable characters is 33 to 126, so random mod 94 plus 33
            var asciiValue = _random.Next(94) + 33;
            char value = (char)asciiValue;
            return value;
        }

        //Unimplemented
        private string AddRandomCharactersToWords(string message)
        {
            return message;
        }
    }
}