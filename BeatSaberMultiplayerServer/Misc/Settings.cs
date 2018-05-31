using System;
using System.IO;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;

namespace BeatSaberMultiplayerServer.Misc {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {

        [JsonObject(MemberSerialization.OptIn)]
        public class ServerSettings {
            private string _ip;
            private int _port;
            private string _serverName;
            private string _serverHubIP;
            private int _serverHubPort;

            private int _lobbyTime;

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
            public string IP
            {
                get {
                    if (_ip != null) return _ip;
                    _ip = GetPublicIPv4();
                    return _ip;
                }
            }
            
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
            public string ServerName
            {
                get => _serverName;
                set
                {
                    _serverName = value;
                    MarkDirty();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string ServerHubIP
            {
                get => _serverHubIP;
                set
                {
                    _serverHubIP = value;
                    MarkDirty();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public int ServerHubPort
            {
                get => _serverHubPort;
                set
                {
                    _serverHubPort = value;
                    MarkDirty();
                }
            }

            [JsonProperty]
            public int LobbyTime
            {
                get => _lobbyTime;
                set
                {
                    _lobbyTime = value;
                    MarkDirty();
                }
            }

            string GetPublicIPv4()
            {
                using (var client = new WebClient())
                {
                    return client.DownloadString("https://api.ipify.org");
                }
            }

            private DirectoryInfo AvailableSongs {
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

            public ServerSettings(Action markDirty) {
                MarkDirty = markDirty;
                _port = 3701;
                _serverName = "New Server";
                _serverHubIP = "beatsaber.jaddie.co.uk";
                _serverHubPort = 3700;
                _lobbyTime = 60;
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
        public ServerSettings Server { get; }
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
                    ServerCommons.Misc.Logger.Instance.Exception(ex.Message);
                }

                return _instance;
            }
        }

        private bool IsDirty { get; set; }

        Settings() {
            Server = new ServerSettings(MarkDirty);
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