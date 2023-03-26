using System;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat
{
    public static class LanguageScrambler
    {
        public static string ScrambleMessage(string message, Language language)
        {
            var wordRegex = new Regex(@"\w+");
            return wordRegex.Replace(message, match =>
            {
                var word = match.Groups[0].Value;
                var random = new Random(GetWordHash(word));

                var syllableCount = GetSyllableCount(word, random);

                var garbledText = string.Join("",
                    syllableCount.DoTimes(_ => language.Syllables.GetRandomElement(random)));

                return garbledText;
            });
        }

        private static int GetSyllableCount(string word, Random random)
        {
            return (int)Math.Max((word.Length / 2.0) +
                                 (random.Next(
                                      (int)Math.Round(word.Length / 2.0))
                                  -
                                  Math.Round(word.Length / 4.0)), 1);
        }

        private static int GetWordHash(string word)
        {
            return word.Select(character => (int)character)
                .Aggregate((acc, cur) =>
                    acc + cur);
        }

        //Unused
        private static char RandomChar(char original, Random random)
        {
            if (ChatHelper.IsPunctuation(original))
            {
                return original;
            }

            // ascii ranges of usable characters is 33 to 126, so random mod 94 plus 33
            var asciiValue = random.Next(94) + 33;
            char value = (char)asciiValue;
            return value;
        }

        //Unimplemented
        private static string AddRandomCharactersToWords(string message)
        {
            return message;
        }
    }
}