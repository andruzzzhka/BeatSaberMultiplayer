using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ServerCommons.Misc {
    public class Logger {
        private static Logger _instance;
        
        public static Logger Instance {
            get {
                if (_instance != null) return _instance;
                _instance = new Logger();
                return _instance;
            }
        }

        public enum LoggerLevel {
            Info,
            Warning,
            Exception,
            Error
        }

        public struct LogMessage {
            public string Message { get; set; }
            public LoggerLevel Level { get; set; }

            public LogMessage(string msg, LoggerLevel lvl) {
                Message = msg;
                Level = lvl;
            }
        }
        
        private FileInfo LogFile { get; }

        private StreamWriter LogWriter;

        Logger() {
            LogFile = GetPath();
            LogFile.Create().Close();

            LogWriter = LogFile.AppendText();
            LogWriter.AutoFlush = true;
        }

        #region Log Functions

        public void Log(object msg) {
            PrintLogMessage(new LogMessage($"[LOG @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Info));
        }

        public void Error(object msg) {
            PrintLogMessage(new LogMessage($"[ERROR @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Error));
        }

        public void Exception(object msg) {
            PrintLogMessage(new LogMessage($"[EXCEPTION @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Exception));
        }

        public void Warning(object msg) {
            PrintLogMessage(new LogMessage($"[WARNING @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Warning));
        }

        #endregion
        
        void PrintLogMessage(LogMessage msg)
        {
            Console.ForegroundColor = msg.GetColour();
            LogWriter.WriteLine(msg.Message);
            Console.WriteLine(msg.Message);
            Console.ResetColor();
        }

        FileInfo GetPath() {
            var logsDir = new DirectoryInfo($"./{Settings.Instance.Logger.LogsDir}/{DateTime.Now:dd-MM-yy}");
            logsDir.Create();
            return new FileInfo($"{logsDir.FullName}/{logsDir.GetFiles().Length}.txt");
        }

        public void Stop() {
            _instance = null;
        }
    }
}