using System;
using System.IO;
using System.Reflection;
using SimpleJSON;
using UnityEngine;
using JSON = WyrmTale.JSON;

namespace BeatSaberMultiplayer {
    [Serializable]
    public class Config {
        
        [SerializeField] private string _serverHubIP;
        [SerializeField] private int _serverHubPort;
        [SerializeField] private bool _showAvatarsInGame;
        [SerializeField] private bool _showAvatarsInLobby;

        private static Config _instance;
        
        private static FileInfo FileLocation { get; } = new FileInfo($"./Config/{Assembly.GetExecutingAssembly().GetName().Name}.json");

        public static Config Instance {
            get {
                if (_instance != null) return _instance;
                try {
                    FileLocation?.Directory?.Create();
                    Console.WriteLine($"Attempting to load JSON @ {FileLocation.FullName}");
                    _instance = JsonUtility.FromJson<Config>(File.ReadAllText(FileLocation.FullName));
                    _instance.MarkClean();
                }
                catch(Exception e) {
                    Console.WriteLine($"Can't load config @ {FileLocation.FullName}");
                    _instance = new Config();
                }
                return _instance;
            }
        }
        
        private bool IsDirty { get; set; }

        /// <summary>
        /// Remember to Save after changing the value
        /// </summary>
        public string ServerHubIP {
            get { return _serverHubIP; }
            set {
                _serverHubIP = value;
                MarkDirty();
            }
        }

        public int ServerHubPort
        {
            get { return _serverHubPort; }
            set
            {
                _serverHubPort = value;
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

        public bool ShowAvatarsInLobby
        {
            get { return _showAvatarsInLobby; }
            set
            {
                _showAvatarsInLobby = value;
                MarkDirty();
            }
        }

        Config() {
            _serverHubIP = "beatsaber.jaddie.co.uk";
            _serverHubPort = 3700;
            _showAvatarsInGame = false;
            _showAvatarsInLobby = true;
            MarkDirty();
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    Console.WriteLine($"Writing to File @ {FileLocation.FullName}");
                    var json = JsonUtility.ToJson(this, true);
                    f.Write(json);
                }
                MarkClean();
                return true;
            }
            catch (IOException ex) {
                Console.WriteLine($"ERROR WRITING TO CONFIG [{ex.Message}]");
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