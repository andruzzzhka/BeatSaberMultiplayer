using System;
using System.IO;
using System.Reflection;
using SimpleJSON;
using UnityEngine;
using JSON = WyrmTale.JSON;

namespace BeatSaberMultiplayer {
    [Serializable]
    public class Config {
        
        [SerializeField] private string[] _serverHubIPs;
        [SerializeField] private int[] _serverHubPorts;
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
                catch(Exception) {
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
            _serverHubIPs = new string[]{"hub.assistant.moe", "soupwhale.com", "minemalox.me", "beatsaber.weebvr.com", "178.62.239.103", "beatsaber.jaddie.co.uk" };
            _serverHubPorts = new int[] { 3700, 3700, 3700, 3700, 3700, 3700 };
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