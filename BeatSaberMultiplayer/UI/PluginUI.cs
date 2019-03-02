using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using BeatSaberMultiplayer.UI.UIElements;
using BeatSaberMultiplayer.UI.ViewControllers;
using BS_Utils.Gameplay;
using CustomUI.BeatSaber;
using CustomUI.Settings;
using CustomUI.Utilities;
using SimpleJSON;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI
{
    class PluginUI : MonoBehaviour
    {
        public static PluginUI instance;
        
        private MainMenuViewController _mainMenuViewController;
        private RectTransform _mainMenuRectTransform;

        public ServerHubFlowCoordinator serverHubFlowCoordinator;
        public RoomCreationFlowCoordinator roomCreationFlowCoordinator;
        public RoomFlowCoordinator roomFlowCoordinator;
        public ModeSelectionFlowCoordinator modeSelectionFlowCoordinator;
        public ChannelSelectionFlowCoordinator channelSelectionFlowCoordinator;
        public RadioFlowCoordinator radioFlowCoordinator;

        private TextMeshProUGUI _newVersionText;
        private Button _multiplayerButton;
        private MultiplayerListViewController _publicAvatarOption;

        public static void OnLoad()
        {
            if (instance != null)
            {
                instance.CreateUI();
                return;
            }
            new GameObject("Multiplayer Plugin").AddComponent<PluginUI>();
        }

        public void Awake()
        {
            if (instance != this)
            {
                DontDestroyOnLoad(this);
                instance = this;
                GetUserInfo.UpdateUserInfo();
                SongLoader.SongsLoadedEvent += SongsLoaded;
                CreateUI();
            }
        }

        public void SongsLoaded(SongLoader sender, List<CustomLevel> levels)
        {
            if (_multiplayerButton != null)
            {
                _multiplayerButton.interactable = true;
            }
            else
            {
                CreateUI();
            }

            SongInfo.GetOriginalLevelHashes();
        }

        public void CreateUI()
        {
            try
            {
                _mainMenuViewController = Resources.FindObjectsOfTypeAll<MainMenuViewController>().First();
                _mainMenuRectTransform = _mainMenuViewController.transform as RectTransform;

                if (serverHubFlowCoordinator == null)
                {
                    serverHubFlowCoordinator = new GameObject("ServerHubFlow").AddComponent<ServerHubFlowCoordinator>();
                }
                if (roomCreationFlowCoordinator == null)
                {
                    roomCreationFlowCoordinator = new GameObject("RoomCreationFlow").AddComponent<RoomCreationFlowCoordinator>();
                }
                if (roomFlowCoordinator == null)
                {
                    roomFlowCoordinator = new GameObject("RoomFlow").AddComponent<RoomFlowCoordinator>();
                }
                if (modeSelectionFlowCoordinator == null)
                {
                    modeSelectionFlowCoordinator = new GameObject("ModeSelectFlow").AddComponent<ModeSelectionFlowCoordinator>();
                }
                if (channelSelectionFlowCoordinator == null)
                {
                    channelSelectionFlowCoordinator = new GameObject("ChannelSelectFlow").AddComponent<ChannelSelectionFlowCoordinator>();
                }
                if (radioFlowCoordinator == null)
                {
                    radioFlowCoordinator = new GameObject("RadioFlow").AddComponent<RadioFlowCoordinator>();
                }

                CreateOnlineButton();
                _multiplayerButton.interactable = SongLoader.AreSongsLoaded;

                StartCoroutine(CheckVersion());

                CreateMenu();
            }
            catch (Exception e)
            {
                Misc.Logger.Exception($"Unable to create UI! Exception: {e}");
            }
        }

        private void CreateOnlineButton()
        {
            _newVersionText = BeatSaberUI.CreateText(_mainMenuRectTransform, "A new version of the mod\nis available!", new Vector2(18.25f, 30f));
            _newVersionText.fontSize = 5f;
            _newVersionText.lineSpacing = -30;
            _newVersionText.alignment = TextAlignmentOptions.Center;
            _newVersionText.gameObject.SetActive(false);

            _multiplayerButton = BeatSaberUI.CreateUIButton(_mainMenuRectTransform, "SoloFreePlayButton");
            _multiplayerButton.transform.SetParent(Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "SoloFreePlayButton").transform.parent);
            _multiplayerButton.transform.SetSiblingIndex(2);

            _multiplayerButton.SetButtonText("Online");
            _multiplayerButton.SetButtonIcon(Sprites.onlineIcon);

            _multiplayerButton.onClick.AddListener(delegate ()
            {
                try
                {
                    MainFlowCoordinator mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();

                    mainFlow.InvokeMethod("PresentFlowCoordinator", modeSelectionFlowCoordinator, null, false, false);
                }
                catch (Exception e)
                {
                    Misc.Logger.Exception($"Unable to present flow coordinator! Exception: {e}");
                }
            });
        }

        private void CreateMenu()
        {
            var onlineSubMenu = SettingsUI.CreateSubMenu("Multiplayer | General");

            var avatarsInGame = onlineSubMenu.AddBool("Show Avatars In Game", "Show avatars of other players while playing a song");
            avatarsInGame.GetValue += delegate { return Config.Instance.ShowAvatarsInGame; };
            avatarsInGame.SetValue += delegate (bool value) { Config.Instance.ShowAvatarsInGame = value; };

            var blocksInGame = onlineSubMenu.AddBool("Show Other Players Blocks", "<color=red>BETA</color>\nShow other players blocks while playing a song\n<color=red>Requires \"Show Avatars In Game\"</color>");
            blocksInGame.GetValue += delegate { return Config.Instance.ShowOtherPlayersBlocks; };
            blocksInGame.SetValue += delegate (bool value) { Config.Instance.ShowOtherPlayersBlocks = value; };

            var avatarsInRoom = onlineSubMenu.AddBool("Show Avatars In Room", "Show avatars of other players while in room");
            avatarsInRoom.GetValue += delegate { return Config.Instance.ShowAvatarsInRoom; };
            avatarsInRoom.SetValue += delegate (bool value) { Config.Instance.ShowAvatarsInRoom = value; };

            var downloadAvatars = onlineSubMenu.AddBool("Download Other Players Avatars", "Download other players avatars from ModelSaber");
            downloadAvatars.GetValue += delegate { return Config.Instance.DownloadAvatars; };
            downloadAvatars.SetValue += delegate (bool value) { Config.Instance.DownloadAvatars = value; };

            var separateAvatar = onlineSubMenu.AddBool("Separate Avatar For Multiplayer", "Use avatar specified in \"Public Avatar\" instead of your current avatar");
            separateAvatar.GetValue += delegate { return Config.Instance.SeparateAvatarForMultiplayer; };
            separateAvatar.SetValue += delegate (bool value) { InGameOnlineController.Instance.SetSeparatePublicAvatarState(value); };

            _publicAvatarOption = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>((RectTransform)onlineSubMenu.transform, "Public Avatar");
            _publicAvatarOption.OnEnable();
            _publicAvatarOption.ValueChanged += (e) => { InGameOnlineController.Instance.SetSeparatePublicAvatarHash(ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == CustomAvatar.Plugin.Instance.AvatarLoader.Avatars[e]).Key); };
            _publicAvatarOption.maxValue = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.Count - 1;
            _publicAvatarOption.textForValues = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.Select(x => (string.IsNullOrEmpty(x.Name) ? "" : x.Name)).ToArray();

            if (ModelSaberAPI.cachedAvatars.TryGetValue(Config.Instance.PublicAvatarHash, out CustomAvatar.CustomAvatar avatar))
            { 
                _publicAvatarOption.Value = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.ToList().IndexOf(avatar);
            }
            else
            {
                if (ModelSaberAPI.isCalculatingHashes)
                {
                    ModelSaberAPI.hashesCalculated -= UpdateSelectedAvatar;
                    ModelSaberAPI.hashesCalculated += UpdateSelectedAvatar;
                }
                _publicAvatarOption.Value = 0;
            }

            _publicAvatarOption.UpdateText();
            LoadAllAvatars();
            
            var spectatorMode = onlineSubMenu.AddBool("Spectator Mode", "<color=red>BETA</color>\nWatch other players playing a song (e.g. tournaments)\n<color=red>You can't play songs while \"Spectator Mode\" is on!</color>");
            spectatorMode.GetValue += delegate { return Config.Instance.SpectatorMode; };
            spectatorMode.SetValue += delegate (bool value) { Config.Instance.SpectatorMode = value; };



            var voiceSubMenu = SettingsUI.CreateSubMenu("Multiplayer | Voice");

            var voiceEnabled = voiceSubMenu.AddBool("Enable Voice Chat");
            voiceEnabled.GetValue += delegate { return Config.Instance.EnableVoiceChat; };
            voiceEnabled.SetValue += delegate (bool value) { InGameOnlineController.Instance.ToggleVoiceChat(value); };

            var voiceVolume = voiceSubMenu.AddInt("Voice Chat Volume", 1, 20, 1);
            voiceVolume.GetValue += delegate { return (int)(Config.Instance.VoiceChatVolume * 20f); };
            voiceVolume.SetValue += delegate (int value) { Config.Instance.VoiceChatVolume = value / 20f; InGameOnlineController.Instance.VoiceChatVolumeChanged(value / 20f); };

            var micEnabled = voiceSubMenu.AddBool("Enable Microphone");
            micEnabled.GetValue += delegate { return Config.Instance.MicEnabled; };
            micEnabled.SetValue += delegate (bool value) { Config.Instance.MicEnabled = value; };

            var spatialAudio = voiceSubMenu.AddBool("Spatial Audio");
            spatialAudio.GetValue += delegate { return Config.Instance.SpatialAudio; };
            spatialAudio.SetValue += delegate (bool value) { Config.Instance.SpatialAudio = value; InGameOnlineController.Instance.VoiceChatSpatialAudioChanged(value); };

            var pushToTalk = voiceSubMenu.AddBool("Push to Talk");
            pushToTalk.GetValue += delegate { return Config.Instance.PushToTalk; };
            pushToTalk.SetValue += delegate (bool value) { Config.Instance.PushToTalk = value; };
            
            var pushToTalkButton = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>((RectTransform)voiceSubMenu.transform, "Push to Talk Button");
            pushToTalkButton.OnEnable();
            pushToTalkButton.ValueChanged += (e) => { Config.Instance.PushToTalkButton = e; };
            pushToTalkButton.maxValue = 7;
            pushToTalkButton.textForValues = new string[] { "L Grip", "R Grip", "L Trigger", "R Trigger", "L+R Grip", "L+R Trigger", "Any Grip", "Any Trigger" };
            pushToTalkButton.Value = Config.Instance.PushToTalkButton;
            pushToTalkButton.UpdateText();
        }

        void UpdateSelectedAvatar()
        {
            if (ModelSaberAPI.cachedAvatars.TryGetValue(Config.Instance.PublicAvatarHash, out CustomAvatar.CustomAvatar avatar))
            {
                _publicAvatarOption.Value = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.ToList().IndexOf(avatar);
            }
        }

        void LoadAllAvatars(Action callback = null)
        {
            Misc.Logger.Info($"Loading all avatars...");
            foreach (var avatar in CustomAvatar.Plugin.Instance.AvatarLoader.Avatars)
            {
                if (!avatar.IsLoaded)
                {
                    avatar.Load((loadedAvatar, result) => {
                        if (result == CustomAvatar.AvatarLoadResult.Completed)
                        {
                            UpdateAvatarsList();
                            Misc.Logger.Info($"Loading avatar \"{loadedAvatar.Name}\"!");
                        }
                    });
                }
            }
        }

        void UpdateAvatarsList()
        {
            _publicAvatarOption.textForValues = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.Select(x => (string.IsNullOrEmpty(x.Name) ? "" : x.Name)).ToArray();
            _publicAvatarOption.UpdateText();
        }

        IEnumerator CheckVersion()
        {

            Misc.Logger.Info("Checking for updates...");

            UnityWebRequest www = UnityWebRequest.Get($"https://api.github.com/repos/andruzzzhka/BeatSaberMultiplayer/releases");
            www.timeout = 10;

            yield return www.SendWebRequest();
            
            if(!www.isNetworkError && !www.isHttpError)
            {
                JSONNode releases = JSON.Parse(www.downloadHandler.text);

                JSONNode latestRelease = releases[0];                

                bool newTag = (!((string)latestRelease["tag_name"]).StartsWith(Plugin.instance.Version));

                if (newTag)
                {
                    Misc.Logger.Info($"A new version of the mod is available!\nNew version: {(string)latestRelease["tag_name"]}\nCurrent version: {Plugin.instance.Version}");
                    _newVersionText.gameObject.SetActive(true);
                    _newVersionText.text = $"Version {(string)latestRelease["tag_name"]}\nis available!\nCurrent version: {Plugin.instance.Version}";
                }
            }
        }
    }
}
