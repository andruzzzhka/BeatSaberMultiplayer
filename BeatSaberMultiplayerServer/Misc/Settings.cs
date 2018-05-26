using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace BeatSaberMultiplayerServer.Misc {
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
        public class AvailableSongsSettings {
            private int[] _songs;

            private Action MarkDirty { get; }
        
            [JsonProperty]
            public int[] Songs {
                get => _songs;
                set
                {
                    _songs = value;
                    MarkDirty();
                }
            }

            public AvailableSongsSettings(Action markDirty) {
                MarkDirty = markDirty;
                _songs = new int[] { 65};
            }
        }

        [JsonProperty]
        public IPSettings IP { get; }
        [JsonProperty]
        public LoggerSettings Logger { get; }
        [JsonProperty]
        public AvailableSongsSettings AvailableSongs { get; }

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
            AvailableSongs = new AvailableSongsSettings(MarkDirty);
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