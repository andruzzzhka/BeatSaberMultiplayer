using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace ServerHub.Misc {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {

        [JsonObject(MemberSerialization.OptIn)]
        public class IPSettings {
            private int _port;

            private Action MarkDirty { get; }
        
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
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
        
        [JsonObject(MemberSerialization.OptIn)]
        public class DatabaseSettings {
            private string _username;
            private string _password;
            private string _databaseName;

            private Action MarkDirty { get; }
        
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string Username {
                get => _username;
                set {
                    _username = value;
                    MarkDirty();
                }
            }
            
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string Password {
                get => _password;
                set {
                    _password = value;
                    MarkDirty();
                }
            }
            
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string DatabaseName {
                get => _databaseName;
                set {
                    _databaseName = value;
                    MarkDirty();
                }
            }

            public DatabaseSettings(Action markDirty) {
                MarkDirty = markDirty;
                _username = "username";
                _password = "password";
                _databaseName = "database";
            }
        }

        [JsonProperty]
        public IPSettings IP { get; }
        [JsonProperty]
        public LoggerSettings Logger { get; }
        [JsonProperty]
        public DatabaseSettings Database { get; }

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
            IP = new IPSettings(MarkDirty);
            Logger = new LoggerSettings(MarkDirty);
            Database = new DatabaseSettings(MarkDirty);
            MarkDirty();
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    //Misc.Logger.Instance.Log(json);
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