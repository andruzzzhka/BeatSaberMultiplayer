using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ServerHub.Misc {
    public class Logger : IDisposable {
        private static Logger _instance;
        
        public static Logger Instance {
            get {
                if (_instance != null) return _instance;
                _instance = new Logger();
                return _instance;
            }
        }

        public enum LoggerLevel {
            Log,
            Warning,
            Exception,
            Error
        }

        public struct LogMessage {
            public string Message { get; set; }
            public string Type { get; set; }
            public long Time { get; set; }

            public LogMessage(string msg, LoggerLevel lvl) {
                Message = msg;
                Type = lvl.ToString();
                Time = DateTime.Now.Ticks/TimeSpan.TicksPerSecond;
            }
        }

        public List<LogMessage> logHistory;
        public int logHistorySize;

        private FileInfo LogFile { get; }

        private StreamWriter LogWriter;

        Logger() {
            logHistorySize = 128;
            logHistory = new List<LogMessage>();

            LogFile = GetPath();
            LogFile.Create().Close();

            LogWriter = LogFile.AppendText();
            LogWriter.AutoFlush = true;
        }

        #region Log Functions

        public void Debug(object msg, bool hideHeader = false)
        {
#if DEBUG
            PrintLogMessage(new LogMessage($"[DEBUG     -- {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Log));
#endif
        }

        public void Log(object msg, bool hideHeader = false) {
            PrintLogMessage(new LogMessage($"[LOG       -- {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Log));
        }

        public void Error(object msg, bool hideHeader = false) {
             PrintLogMessage(new LogMessage($"[ERROR     -- {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Error));
        }

        public void Exception(object msg, bool hideHeader = false) {
            PrintLogMessage(new LogMessage($"[EXCEPTION -- {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Exception));
        }

        public void Warning(object msg, bool hideHeader = false) {
            PrintLogMessage(new LogMessage($"[WARNING   -- {DateTime.Now:HH:mm:ss}] {msg}", LoggerLevel.Warning));
        }

        #endregion
        
        void PrintLogMessage(LogMessage msg)
        {
            logHistory.Add(msg);
            if(logHistory.Count > logHistorySize)
            {
                logHistory.RemoveAt(0);
            }

            Console.ForegroundColor = msg.GetColour();
            Console.WriteLine(msg.Message);
            LogWriter.WriteLine(msg.Message);
            Console.ResetColor();
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
            Dispose();
        }

        public void Dispose()
        {
            LogWriter.Flush();
            LogWriter.Close();
        }
    }
}