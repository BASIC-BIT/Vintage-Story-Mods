using System;
using System.Collections.Generic;
using System.Linq;

namespace thebasics.Extensions
{
    public static class IntExtensions
    {
        public static IEnumerable<T> DoTimes<T>(this int value, Func<int, T> method)
        {
            return Enumerable.Range(0, value).Select(method);
        }
        
        public static IEnumerable<T> DoTimes<T>(this int value, Func<T> method)
        {
            return Enumerable.Range(0, value).Select(_ => method());
        }
    }
}