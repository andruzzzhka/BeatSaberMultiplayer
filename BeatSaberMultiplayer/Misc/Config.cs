using System;
using System.IO;
using System.Reflection;
using BeatSaberMultiplayer.Misc;
using IllusionPlugin;
using SimpleJSON;
using UnityEngine;

namespace BeatSaberMultiplayer {
    [Serializable]
    public class Config {

        [SerializeField] private string[] _serverHubIPs;
        [SerializeField] private int[] _serverHubPorts;
        [SerializeField] private bool _showAvatarsInGame;
        [SerializeField] private bool _showOtherPlayersBlocks;
        [SerializeField] private bool _showAvatarsInRoom;
        [SerializeField] private bool _downloadAvatars;
        [SerializeField] private bool _separateAvatarForMultiplayer;
        [SerializeField] private string _publicAvatarHash;
        [SerializeField] private bool _spectatorMode;
        [SerializeField] private int _maxSimultaneousDownloads;
        [SerializeField] private string _beatSaverURL;

        [SerializeField] private bool _enableVoiceChat;
        [SerializeField] private float _voiceChatVolume;
        [SerializeField] private string _inputDevice;
        [SerializeField] private bool _pushToTalk;
        [SerializeField] private int _pushToTalkButton;


        private static Config _instance;

        private static FileInfo FileLocation { get; } = new FileInfo($"./UserData/{Assembly.GetExecutingAssembly().GetName().Name}.json");

        public static bool Load()
        {
            if (_instance != null) return false;
            try
            {
                FileLocation?.Directory?.Create();
                Misc.Logger.Info($"Attempting to load JSON @ {FileLocation.FullName}");
                _instance = JsonUtility.FromJson<Config>(File.ReadAllText(FileLocation.FullName));
                _instance.MarkDirty();
                _instance.Save();
            }
            catch (Exception)
            {
                Misc.Logger.Error($"Unable to load config @ {FileLocation.FullName}");
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
                Misc.Logger.Info($"Creating new config @ {FileLocation.FullName}");
                Instance.Save();
            }
            catch (Exception)
            {
                Misc.Logger.Error($"Unable to create new config @ {FileLocation.FullName}");
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
                    _publicAvatarHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
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

        public int MaxSimultaneousDownloads
        {
            get { return _maxSimultaneousDownloads; }
            set
            {
                _maxSimultaneousDownloads = value;
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

        public string InputDevice
        {
            get { return _inputDevice; }
            set
            {
                _inputDevice = value;
                MarkDirty();
            }
        }

        Config()
        {
            _serverHubIPs = new string[] { "127.0.0.1", "soupwhale.com", "hub.assistant.moe", "hub.n3s.co", "hub.auros.red", "beige.space", "treasurehunters.nz", "beatsaber.networkauditor.org", "hub.ligma.site", "hub.jogi-server.de", "beatsaberhub.freddi.xyz" };
            _serverHubPorts = new int[] { 3700, 3700, 3700, 3700, 3700, 3700, 3700, 3700, 3700, 3700, 3700 };
            _showAvatarsInGame = false;
            _showOtherPlayersBlocks = false;
            _showAvatarsInRoom = true;
            _downloadAvatars = true;
            _spectatorMode = false;
            _separateAvatarForMultiplayer = false;
            _publicAvatarHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            _maxSimultaneousDownloads = ModPrefs.GetInt("BeatSaverDownloader", "maxSimultaneousDownloads", 3);
            _beatSaverURL = ModPrefs.GetString("BeatSaverDownloader", "beatsaverURL", "https://beatsaver.com");

            _enableVoiceChat = true;
            _voiceChatVolume = 0.8f;
            _pushToTalk = true;
            _pushToTalkButton = 0;
            _inputDevice = "";

            IsDirty = true;
        }

        public bool Save() {
            if (!IsDirty) return false;
            try {
                using (var f = new StreamWriter(FileLocation.FullName)) {
                    Misc.Logger.Info($"Writing to File @ {FileLocation.FullName}");
                    var json = JsonUtility.ToJson(this, true);
                    f.Write(json);
                }
                MarkClean();
                return true;
            }
            catch (Exception ex) {
                Misc.Logger.Exception($"Unable to write the config file! Exception: {ex}");
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
