using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BeatSaberMultiplayer.Misc
{
    static class Log
    {
        private static StreamWriter logWriter = new StreamWriter("MPLog.txt") { AutoFlush = true};
        private static string loggerName = "BSMultiplayer";

        public static void Info(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("["+loggerName+" - Info] "+message);
            logWriter.WriteLine("[" + loggerName + " - Info] " + message);
        }

        public static void Warning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("[" + loggerName + " - Warning] " + message);
            logWriter.WriteLine("[" + loggerName + " - Warning] " + message);
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[" + loggerName + " - Error] " + message);
            logWriter.WriteLine("[" + loggerName + " - Error] " + message);
        }

        public static void Exception(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[" + loggerName + " - Exception] " + message);
            logWriter.WriteLine("[" + loggerName + " - Exception] " + message);
        }

    }
}
