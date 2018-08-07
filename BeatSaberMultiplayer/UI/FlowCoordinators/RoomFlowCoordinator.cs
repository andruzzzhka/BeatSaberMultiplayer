using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class RoomFlowCoordinator : FlowCoordinator
    {
        ServerHubNavigationController _serverHubNavigationController;

        CustomKeyboardViewController _passwordKeyboard;
        RoomNavigationController _roomNavigationController;

        HostSongSelectionViewController _songSelectionViewController;

        RoomInfo roomState;

        string ip;
        int port;
        uint roomId;
        bool usePassword;
        string password;

        bool joined = false;

        List<string> availableSongsLevelIDs = new List<string>();

        public void JoinRoom(ServerHubNavigationController serverHubNavCon, string ip, int port, uint roomId, bool usePassword, string pass = "")
        {
            _serverHubNavigationController = serverHubNavCon;
            password = pass;
            this.ip = ip;
            this.port = port;
            this.roomId = roomId;
            this.usePassword = usePassword;

            if (usePassword && string.IsNullOrEmpty(password))
            {
                if (_passwordKeyboard == null)
                {
                    _passwordKeyboard = BeatSaberUI.CreateViewController<CustomKeyboardViewController>();
                    _passwordKeyboard.enterButtonPressed += PasswordEntered;
                }

                _passwordKeyboard._inputString = "";
                _serverHubNavigationController.PresentModalViewController(_passwordKeyboard, null);
            }
            else
            {
                if(_roomNavigationController == null)
                {
                    _roomNavigationController = BeatSaberUI.CreateViewController<RoomNavigationController>();
                }

                _serverHubNavigationController.PresentModalViewController(_roomNavigationController, null);
                PluginUI.instance.downloadFlowCoordinator.Initialize(_roomNavigationController);

                if (Client.instance == null)
                {
                    Client.CreateClient();
                }
                if (!Client.instance.Connected || (Client.instance.Connected && (Client.instance.ip != ip || Client.instance.port != port)))
                {
                    Client.instance.Disconnect(true);
                    Client.instance.Connect(ip, port);
                    Client.instance.ConnectedToServerHub += ConnectedToServerHub;
                }
                else
                {
                    ConnectedToServerHub();
                }

            }
        }

        public void LeaveRoom()
        {
            if (joined)
            {
                Client.instance.LeaveRoom();
                Client.instance.Disconnect();
                joined = false;
            }
            Client.instance.PacketReceived -= PacketReceived;
            PluginUI.instance.serverHubFlowCoordinator.UpdateRoomsList();
        }

        private void PasswordEntered(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                JoinRoom(_serverHubNavigationController, ip, port, roomId, usePassword, input);
            }
        }

        private void ConnectedToServerHub()
        {
            Client.instance.ConnectedToServerHub -= ConnectedToServerHub;
            Client.instance.PacketReceived += PacketReceived;
            if (usePassword)
            {
                Client.instance.JoinRoom(roomId, password);
            }
            else
            {
                Client.instance.JoinRoom(roomId);
            }
        }
        
        private void PacketReceived(BasePacket packet)
        {
            if (!joined)
            {
                if(packet.commandType == CommandType.JoinRoom)
                {
                    switch (packet.additionalData[0])
                    {
                        case 0:
                            {

                                Client.instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                                Client.instance.RequestRoomInfo();
                                joined = true;
                            }
                            break;
                        case 1:
                            {
                                _roomNavigationController.DisplayError("Room not found");
                            }
                            break;
                        case 2:
                            {
                                _roomNavigationController.DisplayError("Incorrect password");
                            }
                            break;
                        case 3:
                            {
                                _roomNavigationController.DisplayError("Too much players");
                            }
                            break;
                        default:
                            {
                                _roomNavigationController.DisplayError("Unknown error");
                            }
                            break;

                    }
                }
            }
            else
            {
                switch (packet.commandType)
                {
                    case CommandType.GetRoomInfo:
                        {
                            int songsCount = BitConverter.ToInt32(packet.additionalData, 0);
                            Dictionary<string, bool> songIds = new Dictionary<string, bool>();
                            availableSongsLevelIDs.Clear();
                            for (int i = 0; i < songsCount; i++)
                            {
                                string levelId = BitConverter.ToString(packet.additionalData.Skip(4 + 16 * i).Take(16).ToArray()).Replace("-", "");
                                songIds.Add(levelId, SongLoader.CustomLevels.Any(x => x.levelID.StartsWith(levelId)));
                                availableSongsLevelIDs.Add(levelId);
                            }

                            roomState = new RoomInfo(packet.additionalData.Skip(4 + 16 * songsCount).ToArray());
                            Client.instance.isHost = Client.instance.playerInfo.Equals(roomState.roomHost);
                            if (Client.instance.isHost)
                            {
                                Log.Info("You are host of this room!");
                            }

                            if (songIds.All(x => x.Value))
                            {
                                Client.instance.playerInfo.playerState = PlayerState.Room;

                                Log.Info("All songs downloaded!");

                                if(Client.instance.isHost)
                                    ShowSongsList();
                            }
                            else
                            {
                                Log.Info("Downloading missing songs...");
                                foreach (var song in songIds.Where(x => !x.Value))
                                {
                                    Client.instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                                    PluginUI.instance.downloadFlowCoordinator.AllSongsDownloaded += DownloadFlowCoordinator_AllSongsDownloaded;
                                    StartCoroutine(PluginUI.instance.downloadFlowCoordinator.EnqueueSongByLevelID(song.Key));
                                }
                            }
                        }
                        break;
                    case CommandType.SetSelectedSong:
                        {
                            SongInfo selectedSong = new SongInfo(packet.additionalData);
                            roomState.selectedSong = selectedSong;
                            SongLoader.Instance.LoadAudioClipForLevel(SongLoader.CustomLevels.First(x => x.levelID.StartsWith(selectedSong.levelId)), SongLoaded);
                        }
                        break;
                    case CommandType.TransferHost:
                        {
                            roomState.roomHost = new PlayerInfo(packet.additionalData);
                            Client.instance.isHost = Client.instance.playerInfo.Equals(roomState.roomHost);
                            switch(roomState.roomState)
                            {
                                case RoomState.SelectingSong:
                                    {
                                        if (Client.instance.isHost)
                                        {
                                            ShowSongsList();
                                        }
                                        else
                                        {
                                            HideSongsList();
                                        }
                                    }break;
                                case RoomState.Preparing:
                                    {
                                        if (Client.instance.isHost)
                                        {
                                            if(roomState.selectedSong != null)
                                                ShowDifficultySelection(SongLoader.CustomLevels.First(x => x.levelID.StartsWith(roomState.selectedSong.levelId)));
                                        }
                                        else
                                        {
                                            HideDifficultySelection();
                                        }
                                    }break;
                            }
                        }
                        break;
                    case CommandType.StartLevel:
                        {
                            byte difficulty = packet.additionalData[0];
                            SongInfo songInfo = new SongInfo(packet.additionalData.Skip(1).ToArray());

                            GameplayOptions gameplayOptions = new GameplayOptions();
                            gameplayOptions.noEnergy = roomState.noFail;

                            MainGameSceneSetupData mainGameSceneSetupData = Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().FirstOrDefault();

                            if (mainGameSceneSetupData != null)
                            {
                                mainGameSceneSetupData.Init(SongLoader.CustomLevels.First(x => x.levelID.StartsWith(songInfo.levelId)).GetDifficultyLevel((LevelDifficulty)difficulty), gameplayOptions, GameplayMode.SoloStandard, 0f);
                                mainGameSceneSetupData.didFinishEvent += MainGameSceneSetupData_didFinishEvent;
                                mainGameSceneSetupData.TransitionToScene(0.7f);
                                return;
                            }
                            else
                            {
                                Console.WriteLine("SceneSetupData is null!");
                            }

                        }
                        break;
                }
            }
        }

        private void SongLoaded(CustomLevel song)
        {

        }

        private void MainGameSceneSetupData_didFinishEvent(MainGameSceneSetupData sender, LevelCompletionResults result)
        {
            sender.didFinishEvent -= MainGameSceneSetupData_didFinishEvent;
            Resources.FindObjectsOfTypeAll<MenuSceneSetupData>().First().TransitionToScene((result == null) ? 0.35f : 1.3f);
        }

        private void DownloadFlowCoordinator_AllSongsDownloaded()
        {
            Client.instance.playerInfo.playerState = PlayerState.Room;
            if (Client.instance.isHost)
                ShowSongsList();
        }


        public void ShowSongsList()
        {
            if(_songSelectionViewController == null)
            {
                _songSelectionViewController = BeatSaberUI.CreateViewController<HostSongSelectionViewController>();
            }
            if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_songSelectionViewController) < 0)
            {
                _roomNavigationController.PushViewController(_songSelectionViewController, false);

                List<CustomLevel> availableSongs = new List<CustomLevel>();
                availableSongsLevelIDs.ForEach(x => availableSongs.Add(SongLoader.CustomLevels.First(y => y.levelID.StartsWith(x))));

                _songSelectionViewController.SetSongs(availableSongs);
                _songSelectionViewController.SongSelected += SongSelected;
            }
            
        }

        private void SongSelected(CustomLevel song)
        {

        }

        public void ShowDifficultySelection(CustomLevel song)
        {

        }

        public void HideSongsList()
        {
            if (_songSelectionViewController != null)
            {
                if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_songSelectionViewController) >= 0)
                {
                    _roomNavigationController.PopViewControllerImmediately();
                    _songSelectionViewController.SongSelected -= SongSelected;
                    Destroy(_songSelectionViewController.gameObject);
                }
            }
        }

        public void HideDifficultySelection()
        {

        }
    }
}
