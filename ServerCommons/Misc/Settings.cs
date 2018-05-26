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
            public string LogsDir;
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
                    ServerCommons.Misc.Logger.Instance.Exception(ex.Message);
                }

                return _instance;
            }
        }

        Settings() {
            Logger = new LoggerSettings();
        }
    }
}
