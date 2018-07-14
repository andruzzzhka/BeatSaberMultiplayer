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
            public bool PrintToLog { get; set; }

            public LogMessage(string msg, LoggerLevel lvl, bool log = true) {
                Message = msg;
                Level = lvl;
                PrintToLog = log;
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

        public void Log(object msg, bool hideHeader = false) {
            if(!hideHeader)
                PrintLogMessage(new LogMessage($"[LOG @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Info));
            else
                PrintLogMessage(new LogMessage($"{msg}", LoggerLevel.Info, false));
        }

        public void Error(object msg, bool hideHeader = false) {
            if (!hideHeader)
                PrintLogMessage(new LogMessage($"[ERROR @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Error));
            else
                PrintLogMessage(new LogMessage($"{msg}", LoggerLevel.Error, false));
        }

        public void Exception(object msg, bool hideHeader = false) {
            if (!hideHeader)
                PrintLogMessage(new LogMessage($"[EXCEPTION @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Exception));
            else
                PrintLogMessage(new LogMessage($"{msg}", LoggerLevel.Exception, false));
        }

        public void Warning(object msg, bool hideHeader = false) {
            if (!hideHeader)
                PrintLogMessage(new LogMessage($"[Warning @ {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Warning));
            else
                PrintLogMessage(new LogMessage($"{msg}", LoggerLevel.Warning, false));
        }

        #endregion
        
        void PrintLogMessage(LogMessage msg)
        {
            Console.ForegroundColor = msg.GetColour();
            Console.WriteLine(msg.Message);
            if (msg.PrintToLog)
                LogWriter.WriteLine(msg.Message);
        }

        FileInfo GetPath() {
            var logsDir = new DirectoryInfo($"./{Settings.Instance.Logger.LogsDir}/{DateTime.Now:dd-MM-yy}");
            logsDir.Create();
            
            if (logsDir.GetFiles().Length > 0)
            {
                if (string.IsNullOrEmpty(File.ReadAllText($"{logsDir.FullName}/{logsDir.GetFiles().Length - 1}.txt")))
                {
                    return new FileInfo($"{logsDir.FullName}/{logsDir.GetFiles().Length - 1}.txt");
                }
            }
            return new FileInfo($"{logsDir.FullName}/{logsDir.GetFiles().Length}.txt");
        }

        public void Stop() {
            _instance = null;
        }
    }
}