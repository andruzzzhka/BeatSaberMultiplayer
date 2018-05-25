using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace ServerHub.Misc {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {

        [JsonObject(MemberSerialization.OptIn)]
        public class IPSettings {
            [JsonProperty]
            private int _port;

            private Action MarkDirty { get; }
        
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            public int Port {
                get => _port;
                set {
                    _port = value;
                    MarkDirty();
                }
            }

            public IPSettings(Action markDirty) {
                MarkDirty = markDirty;
                _port = 3700;
            }
        }
        [JsonObject(MemberSerialization.OptIn)]
        public class LoggerSettings {
            [JsonProperty]
            private string _logsDir;

            private Action MarkDirty { get; }
        
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
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

        public IPSettings SettingsIP => _ip;
        public LoggerSettings SettingsLogger => _logger;

        private static Settings _instance;
        [JsonProperty]
        private readonly IPSettings _ip;
        [JsonProperty]
        private readonly LoggerSettings _logger;

        private static FileInfo FileLocation { get; } = new FileInfo("./Settings.json");

        public static Settings Instance {
            get {
                if (_instance != null) return _instance;
                try {
                    FileLocation?.Directory?.Create();
                    _instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FileLocation.FullName));
                    _instance.MarkClean();
                }
                catch (Exception ex) {
                    _instance = new Settings();
                    _instance.Save();
                }

                return _instance;
            }
        }

        private bool IsDirty { get; set; }

        Settings() {
            _ip = new IPSettings(MarkDirty);
            _logger = new LoggerSettings(MarkDirty);
            MarkDirty();
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    Logger.Instance.Log(json);
                    f.Write(json);
                }

                MarkClean();
                return true;
            }
            catch (IOException ex) {
                return false;
            }
        }

        void MarkDirty() {
            IsDirty = true;
        }

        void MarkClean() {
            IsDirty = false;
        }
    }
}