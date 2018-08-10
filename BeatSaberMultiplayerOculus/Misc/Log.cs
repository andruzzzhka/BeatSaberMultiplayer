using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeatSaberMultiplayer.Misc
{
    static class Log
    {
        private static string loggerName = "BSMultiplayer";

        public static void Info(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("["+loggerName+" - Info] "+message);
        }

        public static void Warning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("[" + loggerName + " - Warning] " + message);
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[" + loggerName + " - Error] " + message);
        }

        public static void Exception(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + loggerName + " - Exception] " + message);
        }

    }
}
