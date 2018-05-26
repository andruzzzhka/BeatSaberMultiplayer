using System;
using System.IO;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;

namespace BeatSaberMultiplayerServer {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {

        [JsonObject(MemberSerialization.OptIn)]
        public class ServerSettings {
            private string _name;
            private string _ip;
            private int _port;
            private int[] _songs;
            [JsonProperty]
            private string SongDirectory;
            private DirectoryInfo _availableSongs;
            private DirectoryInfo _downloads;
            private DirectoryInfo _downloaded;
            
            private Action MarkDirty { get; }
        
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string Name {
                get => _name;
                set {
                    _name = value;
                    MarkDirty();
                }
            }
            
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
            
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public int[] Songs {
                get => _songs;
                set {
                    _songs = value;
                    MarkDirty();
                }
            }

            public string IP {
                get {
                    if (_ip != null) return _ip;
                    _ip = GetPublicIPv4();
                    return _ip;
                }
            }
            
            DirectoryInfo AvailableSongs {
                get {
                    if (_availableSongs != null) return _availableSongs;
                    _availableSongs = new DirectoryInfo(SongDirectory);
                    _availableSongs.Create();
                    return _availableSongs;
                }
            }
            
            public DirectoryInfo Downloads {
                get {
                    if (_downloads != null) return _downloads;
                    _downloads = new DirectoryInfo(Path.Combine(AvailableSongs.FullName, "Downloads"));
                    _downloads.Create();
                    return _downloads;
                }
            }
            
            public DirectoryInfo Downloaded {
                get {
                    if (_downloaded != null) return _downloaded;
                    _downloaded = new DirectoryInfo(Path.Combine(AvailableSongs.FullName, "Downloaded"));
                    _downloaded.Create();
                    return _downloaded;
                }
            }
            
            string GetPublicIPv4() {
                using (var client = new WebClient()) {
                    return client.DownloadString("https://api.ipify.org");
                }
            }

            public ServerSettings(Action markDirty) {
                MarkDirty = markDirty;
                _name = "DEFAULT_SERVER";
                _port = 3700;
                _songs = new[] {65, 45, 46, 31, 71, 197, 517, 584, 476};
                SongDirectory = "AvailableSongs/";
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
        public class ServerHubSettings {
            private string _ip;
            private int _port;

            private Action MarkDirty { get; }
        
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string IP {
                get => _ip;
                set {
                    _ip = value;
                    MarkDirty();
                }
            }
            
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

            public ServerHubSettings(Action markDirty) {
                MarkDirty = markDirty;
                _ip = "";
                _port = 3007;
            }
        }

        [JsonProperty]
        public ServerSettings Server { get; }
        [JsonProperty]
        public LoggerSettings Logger { get; }
        [JsonProperty]
        public ServerHubSettings Database { get; }

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
                    BeatSaberMultiplayerServer.Logger.Instance.Exception(ex.Message);
                }

                return _instance;
            }
        }

        private bool IsDirty { get; set; }

        Settings() {
            Server = new ServerSettings(MarkDirty);
            Logger = new LoggerSettings(MarkDirty);
            Database = new ServerHubSettings(MarkDirty);
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