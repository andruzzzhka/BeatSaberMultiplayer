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
        
        private static Config _instance;
        
        private static FileInfo FileLocation { get; } = new FileInfo($"./Config/{Assembly.GetExecutingAssembly().GetName().Name}.json");

        public static Config Instance {
            get {
                Console.WriteLine("Retrieving Config Instance");
                if (_instance != null) return _instance;
                try {
                    FileLocation?.Directory?.Create();
                    Console.WriteLine($"attempting to load JSON @ {FileLocation.FullName}");
                    _instance = JsonUtility.FromJson<Config>(File.ReadAllText(FileLocation.FullName));
                    _instance.MarkClean();
                }
                catch(Exception ex) {
                    Console.WriteLine($"Config doesn't exist @ {FileLocation.FullName}");
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
        
        /// <summary>
        /// Remember to Save after changing the value
        /// </summary>
        public int ServerHubPort {
            get { return _serverHubPort; }
            set {
                _serverHubPort = value;
                MarkDirty();
            }
        }
        
        Config() {
            _serverHubIP = "127.0.0.1";
            _serverHubPort = 3700;
            MarkDirty();
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    Console.WriteLine($"Writing to File @ {FileLocation.FullName}");
                    var json = JsonUtility.ToJson(this, true);
                    Console.WriteLine($"JSON: {Environment.NewLine}{json}");
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