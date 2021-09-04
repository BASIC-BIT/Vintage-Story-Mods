using System;

namespace thebasics.Extensions
{
    public static class ArrayExtensions
    {
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

            var random = new Random();

            return items[random.Next(items.Length)];
        }
    }
}