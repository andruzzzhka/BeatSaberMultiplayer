using System.Collections;
using BeatSaberMultiplayerServer;

// ReSharper disable once CheckNamespace
namespace System.Linq {
    public static class Extensions {
        public static ConsoleColor GetColour(this Logger.LogMessage message) {
            switch (message.Level) {
                case Logger.LoggerLevel.Info:
                    return ConsoleColor.Blue;
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
    }
}