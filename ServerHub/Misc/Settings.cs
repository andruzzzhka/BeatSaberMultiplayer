using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace ServerHub.Misc {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {

        [JsonObject(MemberSerialization.OptIn)]
        public class ServerSettings {
            private int _port;
            private int _tickrate;
            private bool _tryUPnP;

            private Action MarkDirty { get; }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public int Port
            {
                get => _port;
                set
                {
                    _port = value;
                    MarkDirty();
                }
            }
            
            /// <summary>
             /// Remember to Save after changing the value
             /// </summary>
            [JsonProperty]
            public int Tickrate
            {
                get => _tickrate;
                set
                {
                    _tickrate = value;
                    MarkDirty();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool TryUPnP
            {
                get
                {
                    return _tryUPnP;
                }
                set
                {
                    _tryUPnP = value;
                    MarkDirty();
                }
            }

            public ServerSettings(Action markDirty) {
                MarkDirty = markDirty;
                _port = 3700;
                _tickrate = 30;
                _tryUPnP = true;
            }
        }
        [JsonObject(MemberSerialization.OptIn)]
        public class LoggerSettings {
            private string _logsDir;

            private Action MarkDirty { get; }
        
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string LogsDir {
                get => _logsDir;
                set {
                    _logsDir = value;
                    MarkDirty();
                }
            }

            public LoggerSettings(Action markDirty) {
                MarkDirty = markDirty;
                _logsDir = "Logs/";
            }
        }

        [JsonProperty]
        public ServerSettings Server { get; }
        [JsonProperty]
        public LoggerSettings Logger { get; }

        private static Settings _instance;

        private static FileInfo FileLocation { get; } = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Settings.json"));

        public static Settings Instance {
            get {
                if (_instance != null) return _instance;
                try {
                    FileLocation?.Directory?.Create();
                    _instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FileLocation?.FullName));
                    _instance.MarkDirty();
                }
                catch (Exception ex) {
                    _instance = new Settings();
                    _instance.Save();
                    Misc.Logger.Instance.Exception(ex.Message);
                }

                return _instance;
            }
        }

        private bool IsDirty { get; set; }

        Settings() {
            Server = new ServerSettings(MarkDirty);
            Logger = new LoggerSettings(MarkDirty);
            MarkDirty();
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    f.Write(json);
                }

                MarkClean();
                return true;
            }
            catch (IOException) {
                return false;
            }
        }

        void MarkDirty() {
            IsDirty = true;
            Save();
        }

        void MarkClean() {
            IsDirty = false;
        }
    }
}