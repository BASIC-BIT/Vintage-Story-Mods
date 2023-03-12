#nullable enable
using System;

namespace thebasics.Extensions
{
    public static class ArrayExtensions
    {
        public static T GetRandomElement<T>(this T[] items, Random random)
        {
            if (items.Length == 0)
            {
                throw new ArgumentException();
            }

            if (items.Length == 1)
            {
                return items[0];
            }

            return items[random.Next(items.Length)];
        }
        public static T GetRandomElement<T>(this T[] items)
        {
            if (items.Length == 0)
            {
                throw new ArgumentException();
            }

            if (items.Length == 1)
            {
                return items[0];
            }

            var useRandom = new Random();

            return items[useRandom.Next(items.Length)];
        }
    }
}