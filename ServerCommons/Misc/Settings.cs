using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace ServerCommons.Misc {
    
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings {
        
        [JsonObject(MemberSerialization.OptIn)]
        public class LoggerSettings {
            [JsonProperty]
            public string LogsDir = "./Logs";
        }

        [JsonProperty]
        public LoggerSettings Logger { get; }

        private static Settings _instance;

        private static FileInfo FileLocation { get; } = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Settings.json"));

        public static Settings Instance {
            get {
                if (_instance != null) return _instance;
                try {
                    _instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FileLocation?.FullName));
                }
                catch (Exception ex) {
                    _instance = new Settings();
                    var dir = new DirectoryInfo(_instance.Logger.LogsDir);
                    if (!dir.Exists) dir.Create();
                    Misc.Logger.Instance.Exception(ex.Message);
                }

                return _instance;
            }
        }

        Settings() {
            Logger = new LoggerSettings();
        }
    }
}
