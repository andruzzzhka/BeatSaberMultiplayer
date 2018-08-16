using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using BeatSaberMultiplayer.UI.ViewControllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
                CreateMenu();
            }
            catch (Exception e)
            {
                Log.Exception($"EXCEPTION ON AWAKE(TRY CREATE BUTTON): {e}");
            }
        }

        private void CreateOnlineButton()
        {
            Button _multiplayerButton = BeatSaberUI.CreateUIButton(_mainMenuRectTransform, "PartyButton");
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

            var webSocketServer = onlineSubMenu.AddBool("WebSocket Server");
            webSocketServer.GetValue += delegate { return Config.Instance.EnableWebSocketServer; };
            webSocketServer.SetValue += delegate (bool value) { Config.Instance.EnableWebSocketServer = value; };
        }
    }
}
