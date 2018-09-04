using System;
using System.IO;
using System.Reflection;
using BeatSaberMultiplayer.Misc;
using SimpleJSON;
using UnityEngine;

namespace BeatSaberMultiplayer {
    [Serializable]
    public class Config {
        
        [SerializeField] private string[] _serverHubIPs;
        [SerializeField] private int[] _serverHubPorts;
        [SerializeField] private bool _showAvatarsInGame;
        [SerializeField] private bool _showAvatarsInRoom;
        [SerializeField] private bool _spectatorMode;
        [SerializeField] private bool _enableWebSocketServer;
        [SerializeField] private int _webSocketPort;

        private static Config _instance;
        
        private static FileInfo FileLocation { get; } = new FileInfo($"./UserData/{Assembly.GetExecutingAssembly().GetName().Name}.json");

        public static bool Load()
        {
            if (_instance != null) return false;
            try
            {
                FileLocation?.Directory?.Create();
                Log.Info($"Attempting to load JSON @ {FileLocation.FullName}");
                _instance = JsonUtility.FromJson<Config>(File.ReadAllText(FileLocation.FullName));
                _instance.MarkClean();
            }
            catch (Exception)
            {
                Log.Error($"Unable to load config @ {FileLocation.FullName}");
                return false;
            }
            return true;
        }

        public static bool Create()
        {
            if (_instance != null) return false;
            try
            {
                FileLocation?.Directory?.Create();
                Log.Info($"Creating new config @ {FileLocation.FullName}");
                Instance.Save();
            }
            catch (Exception)
            {
                Log.Error($"Unable to create new config @ {FileLocation.FullName}");
                return false;
            }
            return true;
        }

        public static Config Instance {
            get {
                if (_instance == null)
                    _instance = new Config();
                return _instance;
            }
        }
        
        private bool IsDirty { get; set; }

        /// <summary>
        /// Remember to Save after changing the value
        /// </summary>
        public string[] ServerHubIPs {
            get { return _serverHubIPs; }
            set {
                _serverHubIPs = value;
                MarkDirty();
            }
        }

        public int[] ServerHubPorts
        {
            get { return _serverHubPorts; }
            set
            {
                _serverHubPorts = value;
                MarkDirty();
            }
        }

        public bool ShowAvatarsInGame
        {
            get { return _showAvatarsInGame; }
            set
            {
                _showAvatarsInGame = value;
                MarkDirty();
            }
        }

        public bool ShowAvatarsInRoom
        {
            get { return _showAvatarsInRoom; }
            set
            {
                _showAvatarsInRoom = value;
                MarkDirty();
            }
        }

        public bool SpectatorMode
        {
            get { return _spectatorMode; }
            set
            {
                _spectatorMode = value;
                MarkDirty();
            }
        }

        public bool EnableWebSocketServer
        {
            get { return _enableWebSocketServer; }
            set
            {
                _enableWebSocketServer = value;
                MarkDirty();
            }
        }

        public int WebSocketPort
        {
            get { return _webSocketPort; }
            set
            {
                _webSocketPort = value;
                MarkDirty();
            }
        }

        Config()
        {
            _serverHubIPs = new string[] { "127.0.0.1", "87.103.199.211", "soupwhale.com", "hub.assistant.moe" };
            _serverHubPorts = new int[] { 3700, 3700, 3700, 3700 };
            _showAvatarsInGame = false;
            _showAvatarsInRoom = true;
            _spectatorMode = false;
            _enableWebSocketServer = false;
            _webSocketPort = 3701;
            IsDirty = true;
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    Log.Info($"Writing to File @ {FileLocation.FullName}");
                    var json = JsonUtility.ToJson(this, true);
                    f.Write(json);
                }
                MarkClean();
                return true;
            }
            catch (IOException ex) {
                Log.Exception($"ERROR WRITING TO CONFIG [{ex.Message}]");
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