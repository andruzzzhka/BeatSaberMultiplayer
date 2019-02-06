using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ServerHub.Data;

namespace ServerHub.Misc {
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {

        [JsonObject(MemberSerialization.OptIn)]
        public class ServerSettings {
            private int _port;
            private int _tickrate;
            private bool _tryUPnP;
            private bool _enableWebSocketServer;
            private bool _enableWebSocketRoomInfo;
            private bool _enableWebSocketRCON;
            private string _rconPassword;
            private int _webSocketPort;
            private bool _showTickrateInTitle;
            private bool _allowEventMessages;
            private bool _allowVoiceChat;
            private bool _showTickEventExceptions;
            private bool _sendCrashReports;

            internal Action MarkDirty { get; set; }

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
                    MarkDirty?.Invoke();
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
                    MarkDirty?.Invoke();
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
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool EnableWebSocketServer
            {
                get => _enableWebSocketServer;
                set
                {
                    _enableWebSocketServer = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool EnableWebSocketRoomInfo
            {
                get => _enableWebSocketRoomInfo;
                set
                {
                    _enableWebSocketRoomInfo = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool EnableWebSocketRCON
            {
                get => _enableWebSocketRCON;
                set
                {
                    _enableWebSocketRCON = value;
                    MarkDirty?.Invoke();
                }
            }
            
            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string RCONPassword
            {
                get => _rconPassword;
                set
                {
                    _rconPassword = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public int WebSocketPort
            {
                get => _webSocketPort;
                set
                {
                    _webSocketPort = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool ShowTickrateInTitle
            {
                get => _showTickrateInTitle;
                set
                {
                    _showTickrateInTitle = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool AllowEventMessages
            {
                get => _allowEventMessages;
                set
                {
                    _allowEventMessages = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool AllowVoiceChat
            {
                get => _allowVoiceChat;
                set
                {
                    _allowVoiceChat = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool ShowTickEventExceptions
            {
                get => _showTickEventExceptions;
                set
                {
                    _showTickEventExceptions = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool SendCrashReports
            {
                get => _sendCrashReports;
                set
                {
                    _sendCrashReports = value;
                    MarkDirty?.Invoke();
                }
            }

            public ServerSettings(Action markDirty) {
                MarkDirty = markDirty;
                _port = 3700;
                _tickrate = 30;
                _tryUPnP = true;
                _enableWebSocketServer = false;
                _enableWebSocketRoomInfo = false;
                _enableWebSocketRCON = false;
                _rconPassword = "changeme";
                _webSocketPort = 3701;
                _showTickrateInTitle = true;
                _allowEventMessages = true;
                _allowVoiceChat = true;
                _showTickEventExceptions = false;
                _sendCrashReports = true;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class RadioSettings
        {
            private bool _enableRadio;
            private float _nextSongPrepareTime;
            private float _resultsShowTime;
            private ObservableCollection<ChannelSettings> _radioChannels;

            internal Action MarkDirty { get; set; }

            [JsonProperty]
            public bool EnableRadio
            {
                get => _enableRadio;
                set
                {
                    _enableRadio = value;
                    MarkDirty?.Invoke();
                }
            }

            [JsonProperty]
            public float NextSongPrepareTime
            {
                get => _nextSongPrepareTime;
                set
                {
                    _nextSongPrepareTime = value;
                    MarkDirty?.Invoke();
                }
            }

            [JsonProperty]
            public float ResultsShowTime
            {
                get => _resultsShowTime;
                set
                {
                    _resultsShowTime = value;
                    MarkDirty?.Invoke();
                }
            }

            [JsonProperty]
            public ObservableCollection<ChannelSettings> RadioChannels
            {
                get
                {
                    return _radioChannels;
                }
                set
                {
                    _radioChannels = value;
                    MarkDirty?.Invoke();
                }
            }

            public RadioSettings(Action markDirty)
            {
                MarkDirty = markDirty;
                _enableRadio = false;
                _nextSongPrepareTime = 90f;
                _resultsShowTime = 15f;
                _radioChannels = new ObservableCollection<ChannelSettings>();
                _radioChannels.CollectionChanged += (sender, e) => { MarkDirty?.Invoke(); };
            }
        }
        
        public class ChannelSettings
        {
            public string ChannelName;
            public string ChannelIconUrl;
            [JsonConverter(typeof(StringEnumConverter))]
            public BeatmapDifficulty PreferredDifficulty;
            public List<string> JoinMessages;
            public List<string> DefaultSongIDs;
            
            public ChannelSettings()
            {
                ChannelName = "Radio Channel";
                ChannelIconUrl = "https://cdn.akaku.org/akaku-radio-icon.png";
                PreferredDifficulty = BeatmapDifficulty.Expert;
                JoinMessages = new List<string>();
                DefaultSongIDs = new List<string>();
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class LoggerSettings {
            private string _logsDir;

            internal Action MarkDirty { get; set; }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string LogsDir {
                get => _logsDir;
                set {
                    _logsDir = value;
                    MarkDirty?.Invoke();
                }
            }

            public LoggerSettings(Action markDirty) {
                MarkDirty = markDirty;
                _logsDir = "Logs/";
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class AccessSettings
        {
            private List<string> _blacklist;
            private bool _whitelistEnabled;
            private List<string> _whitelist;

            internal Action MarkDirty { get; set; }

            [JsonProperty]
            public List<string> Blacklist
            {
                get => _blacklist;
                set
                {
                    _blacklist = value;
                    MarkDirty?.Invoke();
                }
            }

            [JsonProperty]
            public bool WhitelistEnabled
            {
                get => _whitelistEnabled;
                set
                {
                    _whitelistEnabled = value;
                    MarkDirty?.Invoke();
                }
            }

            [JsonProperty]
            public List<string> Whitelist
            {
                get => _whitelist;
                set
                {
                    _whitelist = value;
                    MarkDirty?.Invoke();
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

        [JsonObject(MemberSerialization.OptIn)]
        public class TournamentModeSettings
        {
            private bool _enabled;
            private string _roomNameTemplate;
            private int _rooms;
            private string _password;

            internal Action MarkDirty { get; set; }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public bool Enabled
            {
                get => _enabled;
                set
                {
                    _enabled = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string RoomNameTemplate
            {
                get => _roomNameTemplate;
                set
                {
                    _roomNameTemplate = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public int Rooms
            {
                get => _rooms;
                set
                {
                    _rooms = value;
                    MarkDirty?.Invoke();
                }
            }

            /// <summary>
            /// Remember to Save after changing the value
            /// </summary>
            [JsonProperty]
            public string Password
            {
                get => _password;
                set
                {
                    _password = value;
                    MarkDirty?.Invoke();
                }
            }

            public TournamentModeSettings(Action markDirty)
            {
                MarkDirty = markDirty;
                _enabled = false;
                _roomNameTemplate = "Tournament Room {0}";
                _rooms = 4;
                _password = "";
            }
        }

        [JsonProperty]
        public ServerSettings Server { get; internal set; }
        [JsonProperty]
        public RadioSettings Radio { get; internal set; }
        [JsonProperty]
        public LoggerSettings Logger { get; internal set; }
        [JsonProperty]
        public AccessSettings Access { get; internal set; }
        [JsonProperty]
        public TournamentModeSettings TournamentMode { get; internal set; }

        private static Settings _instance;

        private static FileInfo FileLocation { get; } = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Settings.json"));

        public static Settings Instance {
            get {
                if (_instance != null) return _instance;
                try {
                    FileLocation?.Directory?.Create();
                    _instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FileLocation?.FullName));
                    _instance.SetMakeDirtyAction();
                    _instance.MarkDirty();
                }
                catch (Exception ex) {
                    _instance = CreateNewInstance();
                    _instance.Save();
                    Misc.Logger.Instance.Exception(ex);
                }

                return _instance;
            }
        }

        private bool IsDirty { get; set; }

        Settings()
        {

        }

        private void SetMakeDirtyAction()
        {
            Server.MarkDirty = MarkDirty;
            Radio.MarkDirty = MarkDirty;
            Logger.MarkDirty = MarkDirty;
            Access.MarkDirty = MarkDirty;
            TournamentMode.MarkDirty = MarkDirty;
        }

        private static Settings CreateNewInstance()
        {
            Settings instance = new Settings();

            instance.Server = new ServerSettings(instance.MarkDirty);
            instance.Radio = new RadioSettings(instance.MarkDirty);
            instance.Logger = new LoggerSettings(instance.MarkDirty);
            instance.Access = new AccessSettings(instance.MarkDirty);
            instance.TournamentMode = new TournamentModeSettings(instance.MarkDirty);

            instance.MarkDirty();

            return instance;
        }

        public bool Save() {
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    f.Write(json);
                }

                MarkClean();
                return true;
            }
            catch {
                return false;
            }
        }

        public bool Load(string json)
        {
            try
            {
                JsonConvert.PopulateObject(json, this);
                MarkDirty();
                return true;
            }
            catch (Exception e)
            {
                Misc.Logger.Instance.Exception("Unable to load settings! Exception: "+e);
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