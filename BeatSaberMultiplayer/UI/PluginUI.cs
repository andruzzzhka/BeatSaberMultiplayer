using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using BeatSaberMultiplayer.UI.ViewControllers;
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
        public DownloadFlowCoordinator downloadFlowCoordinator;

        private TextMeshProUGUI _newVersionText;
        private Button _multiplayerButton;

        public static void OnLoad()
        {
            if (instance != null)
            {
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
                SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
                SongLoader.SongsLoadedEvent += SongsLoaded;
                CreateUI();
            }
        }

        private void SceneManager_activeSceneChanged(Scene prev, Scene next)
        {
            if(next.name == "Menu")
            {
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
                    serverHubFlowCoordinator.mainMenuViewController = _mainMenuViewController;
                }
                if (roomCreationFlowCoordinator == null)
                {
                    roomCreationFlowCoordinator = new GameObject("RoomCreationFlow").AddComponent<RoomCreationFlowCoordinator>();
                }
                if (roomFlowCoordinator == null)
                {
                    roomFlowCoordinator = new GameObject("RoomFlow").AddComponent<RoomFlowCoordinator>();
                }
                if (downloadFlowCoordinator == null)
                {
                    downloadFlowCoordinator = new GameObject("DownloadFlow").AddComponent<DownloadFlowCoordinator>();
                }

                CreateOnlineButton();
                _multiplayerButton.interactable = SongLoader.AreSongsLoaded;

                StartCoroutine(CheckVersion());

                CreateMenu();
            }
            catch (Exception e)
            {
                Log.Exception($"EXCEPTION ON AWAKE(TRY CREATE BUTTON): {e}");
            }
        }

        private void CreateOnlineButton()
        {
            _newVersionText = BeatSaberUI.CreateText(_mainMenuRectTransform, "A new version of the mod\nis available!", new Vector2(38.5f, -7.5f));
            _newVersionText.fontSize = 6f;
            _newVersionText.alignment = TextAlignmentOptions.Center;
            _newVersionText.gameObject.SetActive(false);

            Console.WriteLine("Text name = "+ _newVersionText.name);

            _multiplayerButton = BeatSaberUI.CreateUIButton(_mainMenuRectTransform, "PartyButton");
            _multiplayerButton.transform.SetParent(Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "SoloButton").transform.parent);

            BeatSaberUI.SetButtonText(_multiplayerButton, "Online");
            BeatSaberUI.SetButtonIcon(_multiplayerButton, Base64Sprites.onlineIcon);

            _multiplayerButton.onClick.AddListener(delegate ()
            {
                try
                {
                    serverHubFlowCoordinator.OnlineButtonPressed();
                }
                catch (Exception e)
                {
                    Log.Exception($"EXCETPION IN ONLINE BUTTON: {e}");
                }
            });
        }

        private void CreateMenu()
        {
            var onlineSubMenu = SettingsUI.CreateSubMenu("Multiplayer");

            var avatarsInGame = onlineSubMenu.AddBool("Show Avatars In Game");
            avatarsInGame.GetValue += delegate { return Config.Instance.ShowAvatarsInGame; };
            avatarsInGame.SetValue += delegate (bool value) { Config.Instance.ShowAvatarsInGame = value; };

            var avatarsInRoom = onlineSubMenu.AddBool("Show Avatars In Room");
            avatarsInRoom.GetValue += delegate { return Config.Instance.ShowAvatarsInRoom; };
            avatarsInRoom.SetValue += delegate (bool value) { Config.Instance.ShowAvatarsInRoom = value; };

            var spectatorMode = onlineSubMenu.AddBool("Spectator Mode (Beta)");
            spectatorMode.GetValue += delegate { return Config.Instance.SpectatorMode; };
            spectatorMode.SetValue += delegate (bool value) { Config.Instance.SpectatorMode = value; };
        }

        IEnumerator CheckVersion()
        {

            Log.Info("Checking for updates...");

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
                    Log.Info($"A new version of the mod is available!\nNew version: {(string)latestRelease["tag_name"]}\nCurrent version: {Plugin.instance.Version}");
                    _newVersionText.gameObject.SetActive(true);
                }
            }
        }
    }
}
