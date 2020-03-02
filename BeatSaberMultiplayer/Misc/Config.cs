using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    [Serializable]
    public class Config {

        [SerializeField] private string _modVersion;
        [SerializeField] private string[] _serverRepositories;
        [SerializeField] private string[] _serverHubIPs;
        [SerializeField] private int[] _serverHubPorts;
        [SerializeField] private bool _showAvatarsInGame;
        [SerializeField] private bool _showOtherPlayersBlocks;
        [SerializeField] private bool _showAvatarsInRoom;
        [SerializeField] private bool _downloadAvatars;
        [SerializeField] private bool _separateAvatarForMultiplayer;
        [SerializeField] private string _publicAvatarHash;
        [SerializeField] private bool _spectatorMode;
        [SerializeField] private int _submitScores;
        [SerializeField] private string _beatSaverURL;

        [SerializeField] private bool _enableVoiceChat;
        [SerializeField] private float _voiceChatVolume;
        [SerializeField] private bool _micEnabled;
        [SerializeField] private bool _spatialAudio;
        [SerializeField] private bool _pushToTalk;
        [SerializeField] private int _pushToTalkButton;
        [SerializeField] private string _voiceChatMicrophone;

        [SerializeField] private Vector3 _scoreScreenPosOffset;
        [SerializeField] private Vector3 _scoreScreenRotOffset;
        [SerializeField] private Vector3 _scoreScreenScale;


        private static Config _instance;

        private static FileInfo FileLocation { get; } = new FileInfo($"./UserData/{Assembly.GetExecutingAssembly().GetName().Name}.json");

        private static readonly List<string> newServerRepositories = new List<string>()
        {
            "https://raw.githubusercontent.com/Zingabopp/BeatSaberMultiplayerServerRepo/master/CompatibleServers.json"
        };

        public static bool Load()
        {
            if (_instance != null) return false;
            try
            {
                FileLocation.Directory.Create();
                Plugin.log.Debug($"Attempting to load JSON @ {FileLocation.FullName}");
                _instance = JsonUtility.FromJson<Config>(File.ReadAllText(FileLocation.FullName));

                UpdateModVersion(_instance);

                _instance.MarkDirty();
                _instance.Save();
            }
            catch (Exception)
            {
                Plugin.log.Error($"Unable to load config @ {FileLocation.FullName}");
                return false;
            }
            return true;
        }

        public static bool Create()
        {
            if (_instance != null) return false;
            try
            {
                FileLocation.Directory.Create();
                Plugin.log.Info($"Creating new config @ {FileLocation.FullName}");
                Instance.Save();
            }
            catch (Exception)
            {
                Plugin.log.Error($"Unable to create new config @ {FileLocation.FullName}");
                return false;
            }
            return true;
        }

        public static void UpdateModVersion(Config _instance)
        {
            
            _instance.ModVersion = IPA.Loader.PluginManager.GetPluginFromId("BeatSaberMultiplayer").Metadata.Version.ToString();
        }

        public static Config Instance {
            get {
                if (_instance == null)
                {
                    _instance = new Config();
                    UpdateModVersion(_instance);
                }
                return _instance;
            }
        }

        private bool IsDirty { get; set; }

        /// <summary>
        /// Remember to Save after changing the value
        /// </summary>
        public string ModVersion
        {
            get { return _modVersion; }
            set
            {
                _modVersion = value;
                MarkDirty();
            }
        }

        public string[] ServerRepositories
        {
            get { return _serverRepositories; }
            set
            {
                _serverRepositories = value;
                MarkDirty();
            }
        }

        /// <summary>
        /// Remember to Save after changing the value
        /// </summary>
        public string[] ServerHubIPs
        {
            get { return _serverHubIPs; }
            set
            {
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

        public bool ShowOtherPlayersBlocks
        {
            get { return _showOtherPlayersBlocks; }
            set
            {
                _showOtherPlayersBlocks = value;
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

        public bool DownloadAvatars
        {
            get { return _downloadAvatars; }
            set
            {
                _downloadAvatars = value;
                MarkDirty();
            }
        }

        public bool SeparateAvatarForMultiplayer
        {
            get { return _separateAvatarForMultiplayer; }
            set
            {
                _separateAvatarForMultiplayer = value;
                MarkDirty();
            }
        }

        public string PublicAvatarHash
        {
            get { return _publicAvatarHash; }
            set
            {
                if (value == null)
                {
                    _publicAvatarHash = Data.PlayerInfo.avatarHashPlaceholder;
                }
                else
                {
                    _publicAvatarHash = value;
                }
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

        public int SubmitScores
        {
            get { return _submitScores; }
            set
            {
                _submitScores = value;
                MarkDirty();
            }
        }

        public string BeatSaverURL
        {
            get { return _beatSaverURL; }
            set
            {
                _beatSaverURL = value;
                MarkDirty();
            }
        }

        public bool EnableVoiceChat
        {
            get { return _enableVoiceChat; }
            set
            {
                _enableVoiceChat = value;
                MarkDirty();
            }
        }

        public float VoiceChatVolume
        {
            get { return _voiceChatVolume; }
            set
            {
                _voiceChatVolume = value;
                MarkDirty();
            }
        }

        public bool MicEnabled
        {
            get { return _micEnabled; }
            set
            {
                _micEnabled = value;
                MarkDirty();
            }
        }

        public bool SpatialAudio
        {
            get { return _spatialAudio; }
            set
            {
                _spatialAudio = value;
                MarkDirty();
            }
        }

        public bool PushToTalk
        {
            get { return _pushToTalk; }
            set
            {
                _pushToTalk = value;
                MarkDirty();
            }
        }

        public int PushToTalkButton
        {
            get { return _pushToTalkButton; }
            set
            {
                _pushToTalkButton = value;
                MarkDirty();
            }
        }

        public string VoiceChatMicrophone
        {
            get { return _voiceChatMicrophone; }
            set
            {
                _voiceChatMicrophone = value;
                MarkDirty();
            }
        }

        public Vector3 ScoreScreenPosOffset
        {
            get { return _scoreScreenPosOffset; }
            set
            {
                _scoreScreenPosOffset = value;
                MarkDirty();
            }
        }

        public Vector3 ScoreScreenRotOffset
        {
            get { return _scoreScreenRotOffset; }
            set
            {
                _scoreScreenRotOffset = value;
                MarkDirty();
            }
        }

        public Vector3 ScoreScreenScale
        {
            get { return _scoreScreenScale; }
            set
            {
                _scoreScreenScale = value;
                MarkDirty();
            }
        }

        Config()
        {
            _modVersion = string.Empty;
            _serverRepositories = new string[0];
            _serverHubIPs = new string[0];
            _serverHubPorts = new int[0];
            _showAvatarsInGame = false;
            _showOtherPlayersBlocks = false;
            _showAvatarsInRoom = true;
            _downloadAvatars = true;
            _spectatorMode = false;
            _separateAvatarForMultiplayer = false;
            _publicAvatarHash = Data.PlayerInfo.avatarHashPlaceholder;
            _submitScores = 2;
            _beatSaverURL = "https://beatsaver.com";

            _enableVoiceChat = true;
            _voiceChatVolume = 0.8f;
            _micEnabled = true;
            _spatialAudio = false;
            _pushToTalk = true;
            _pushToTalkButton = 6;
            _voiceChatMicrophone = null;

            _scoreScreenPosOffset = Vector3.zero;
            _scoreScreenRotOffset = Vector3.zero;
            _scoreScreenScale = Vector3.one;

            IsDirty = true;
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    Plugin.log.Debug($"Writing to File @ {FileLocation.FullName}");
                    var json = JsonUtility.ToJson(this, true);
                    f.Write(json);
                }
                MarkClean();
                return true;
            }
            catch (Exception ex) {
                Plugin.log.Critical(ex);
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
