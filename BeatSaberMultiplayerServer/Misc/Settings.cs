using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BeatSaberMultiplayerServer.Misc {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {

        [JsonObject(MemberSerialization.OptIn)]
        public class ServerSettings {
            private string _ip;
            private int _port;

            private int _wsport;
            private bool _wsenabled;

            private string _serverName;

            private string[] _serverHubIPs;
            private int[] _serverHubPorts;


            private int _maxPlayers;
            private int _lobbyTime;

            private Difficulty _preferredDifficulty;

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
            public int WSPort
            {
                get => _wsport;
                set
                {
                    _wsport = value;
                    MarkDirty();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool WSEnabled
            {
                get => _wsenabled;
                set
                {
                    _wsenabled = value;
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
            public string[] ServerHubIPs
            {
                get => _serverHubIPs;
                set
                {
                    _serverHubIPs = value;
                    MarkDirty();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public int[] ServerHubPorts
            {
                get => _serverHubPorts;
                set
                {
                    _serverHubPorts = value;
                    MarkDirty();
                }
            }

            [JsonProperty]
            public int MaxPlayers
            {
                get => _maxPlayers;
                set
                {
                    _maxPlayers = value;
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

            [JsonProperty]
            public Difficulty PreferredDifficulty
            {
                get => _preferredDifficulty;
                set
                {
                    _preferredDifficulty = value;
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
                _wsport = 3702;
                _wsenabled = false;
                _serverName = "New Server";
                _serverHubIPs = new string[] { "beatsaber.jaddie.co.uk", "assistant.moe" };
                _serverHubPorts = new int[] { 3700, 3700 };
                _maxPlayers = 0;
                _lobbyTime = 60;
                _preferredDifficulty = Difficulty.Expert;
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

        public enum SongOrder { Voting, Shuffle, List}

        [JsonObject(MemberSerialization.OptIn)]
        public class AvailableSongsSettings {

            private SongOrder _order;
            private int[] _songs;

            private Action MarkDirty { get; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty]
            public SongOrder SongOrder
            {
                get => _order;
                set
                {
                    _order = value;
                    MarkDirty();
                }
            }

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
                _order = SongOrder.Voting;
                _songs = new int[] {65};
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class AccessSettings
        {
            private List<string> _blacklist;

            private bool _whitelistEnabled;
            
            private List<string> _whitelist;
            
            private Action MarkDirty { get; }

            [JsonProperty]
            public List<string> Blacklist
            {
                get => _blacklist;
                set
                {
                    _blacklist = value;
                    MarkDirty();
                }
            }

            [JsonProperty]
            public bool WhitelistEnabled
            {
                get => _whitelistEnabled;
                set
                {
                    _whitelistEnabled = value;
                    MarkDirty();
                }
            }

            [JsonProperty]
            public List<string> Whitelist
            {
                get => _whitelist;
                set
                {
                    _whitelist = value;
                    MarkDirty();
                }
            }

            public AccessSettings(Action markDirty)
            {
                MarkDirty = markDirty;
                _blacklist = new List<string>();
                _whitelistEnabled = false;
                _whitelist = new List<string>();
            }

        }

        [JsonProperty]
        public ServerSettings Server { get; }
        [JsonProperty]
        public LoggerSettings Logger { get; }
        [JsonProperty]
        public AvailableSongsSettings AvailableSongs { get; }
        [JsonProperty]
        public AccessSettings Access { get; }

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
            Access = new AccessSettings(MarkDirty);
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
        }

        void MarkClean() {
            IsDirty = false;
        }
    }
}