// ReSharper disable once CheckNamespace

using ServerHub.Misc;
using System.Collections.Generic;

namespace System.Linq
{
    public static class Extensions {
        public static ConsoleColor GetColour(this Logger.LogMessage message) {
            switch (Enum.Parse(typeof(Logger.LoggerLevel), message.Type)) {
                case Logger.LoggerLevel.Log:
                    return ConsoleColor.Green;
                case Logger.LoggerLevel.Warning:
                    return ConsoleColor.Magenta;
                case Logger.LoggerLevel.Exception:
                    return ConsoleColor.Yellow;
                case Logger.LoggerLevel.Error:
                    return ConsoleColor.Red;
                default:
                    return ConsoleColor.White;
            }
        }

        public static uint ConcatUInts(this uint a, uint b)
        {
            const uint c0 = 10, c1 = 100, c2 = 1000, c3 = 10000, c4 = 100000,
                c5 = 1000000, c6 = 10000000, c7 = 100000000, c8 = 1000000000;
            a *= b < c0 ? c0 : b < c1 ? c1 : b < c2 ? c2 : b < c3 ? c3 :
                 b < c4 ? c4 : b < c5 ? c5 : b < c6 ? c6 : b < c7 ? c7 : c8;
            return a + b;
        }

        static Random rnd = new Random();

        public static T Random<T>(this List<T> list)
        {
            return list[rnd.Next(list.Count)];
        }

        public static T Random<T>(this List<T> list, T except)
        {
            T item = list[rnd.Next(list.Count)];
            while (item.Equals(except))
            {
                item = list[rnd.Next(list.Count)];
            }
            return item;
        }
    }

}