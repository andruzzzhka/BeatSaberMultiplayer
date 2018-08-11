using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class RoomFlowCoordinator : FlowCoordinator
    {
        private SongPreviewPlayer _songPreviewPlayer;

        public SongPreviewPlayer PreviewPlayer
        {
            get
            {
                if (_songPreviewPlayer == null)
                {
                    ObjectProvider[] providers = Resources.FindObjectsOfTypeAll<ObjectProvider>().Where(x => x.name == "SongPreviewPlayerProvider").ToArray();

                    if (providers.Length > 0)
                    {
                        _songPreviewPlayer = providers[0].GetProvidedObject<SongPreviewPlayer>();
                    }
                }

                return _songPreviewPlayer;
            }
            private set { _songPreviewPlayer = value; }
        }

        ServerHubNavigationController _serverHubNavigationController;

        CustomKeyboardViewController _passwordKeyboard;
        RoomNavigationController _roomNavigationController;

        SongSelectionViewController _songSelectionViewController;
        DifficultySelectionViewController _difficultySelectionViewController;
        LeaderboardViewController _leaderboardViewController;

        RoomManagementViewController _roomManagementViewController;

        RoomInfo roomInfo;

        string ip;
        int port;
        uint roomId;
        bool usePassword;
        string password;

        bool joined = false;

        List<string> availableSongsLevelIDs = new List<string>();
        
        public void GetLeftAndRightScreenViewControllers(out VRUIViewController leftScreenViewController, out VRUIViewController rightScreenViewController)
        {
            if(_roomManagementViewController == null)
            {
                _roomManagementViewController = BeatSaberUI.CreateViewController<RoomManagementViewController>();
                _roomManagementViewController.DestroyRoomPressed += DestroyRoomPressed;
            }
            leftScreenViewController = _roomManagementViewController;
            rightScreenViewController = null;
        }

        private void DestroyRoomPressed()
        {
            LeaveRoom(true);
        }

        public void ReturnToRoom(ServerHubNavigationController serverHubNavCon)
        {
            _serverHubNavigationController = serverHubNavCon;

            if (_roomNavigationController == null)
            {
                _roomNavigationController = BeatSaberUI.CreateViewController<RoomNavigationController>();
            }

            _serverHubNavigationController.PresentModalViewController(_roomNavigationController, null, true);

            joined = true;
            ConnectedToServerHub();
            Client.instance.RequestRoomInfo();
        }

        public void JoinRoom(ServerHubNavigationController serverHubNavCon, string ip, int port, uint roomId, bool usePassword, string password = "")
        {
            _serverHubNavigationController = serverHubNavCon;
            this.password = password;
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

        public void LeaveRoom(bool destroyRoom = false)
        {
            if (joined)
            {
                if (destroyRoom)
                {
                    Client.instance.DestroyRoom();
                }
                else
                {
                    Client.instance.LeaveRoom();
                }
                Client.instance.Disconnect();
                joined = false;
            }
            
            HideDifficultySelection();
            HideSongsList();
            HideLeaderboard();
            PluginUI.instance.downloadFlowCoordinator.HideQueue();
            InGameOnlineController.Instance.DestroyAvatars();
            PreviewPlayer.CrossfadeToDefault();
            _roomNavigationController.DismissModalViewController(null, false);
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
            if (!joined)
            {
                if (usePassword)
                {
                    Client.instance.JoinRoom(roomId, password);
                }
                else
                {
                    Client.instance.JoinRoom(roomId);
                }
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
                                Client.instance.SendPlayerInfo();
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
                try
                {
                    switch (packet.commandType)
                    {
                        case CommandType.GetRoomInfo:
                            {
                                if (packet.additionalData[0] == 1)
                                {
                                    int songsCount = BitConverter.ToInt32(packet.additionalData, 1);
                                    Dictionary<string, bool> songIds = new Dictionary<string, bool>();
                                    availableSongsLevelIDs.Clear();
                                    for (int i = 0; i < songsCount; i++)
                                    {
                                        string levelId = BitConverter.ToString(packet.additionalData.Skip(5 + 16 * i).Take(16).ToArray()).Replace("-", "");
                                        songIds.Add(levelId, SongLoader.CustomLevels.Any(x => x.levelID.StartsWith(levelId)));
                                        availableSongsLevelIDs.Add(levelId);
                                    }

                                    roomInfo = new RoomInfo(packet.additionalData.Skip(5 + 16 * songsCount).ToArray());
                                    Client.instance.isHost = Client.instance.playerInfo.Equals(roomInfo.roomHost);

                                    if (songIds.All(x => x.Value))
                                    {
                                        Client.instance.playerInfo.playerState = PlayerState.Room;
#if DEBUG
                                        Log.Info("All songs downloaded!");
#endif
                                        UpdateUI(roomInfo.roomState);
                                    }
                                    else
                                    {
#if DEBUG
                                        Log.Info("Downloading missing songs...");
#endif
                                        foreach (var song in songIds.Where(x => !x.Value))
                                        {
                                            Client.instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                                            PluginUI.instance.downloadFlowCoordinator.AllSongsDownloaded += DownloadFlowCoordinator_AllSongsDownloaded;
                                            StartCoroutine(PluginUI.instance.downloadFlowCoordinator.EnqueueSongByLevelID(song.Key));
                                        }
                                    }
                                }
                                else
                                {
                                    roomInfo = new RoomInfo(packet.additionalData.Skip(1).ToArray());
                                    Client.instance.isHost = Client.instance.playerInfo.Equals(roomInfo.roomHost);

                                    UpdateUI(roomInfo.roomState);
                                }
                            }
                            break;
                        case CommandType.SetSelectedSong:
                            {

                                if (packet.additionalData == null || packet.additionalData.Length == 0)
                                {
                                    roomInfo.roomState = RoomState.SelectingSong;
                                    roomInfo.selectedSong = null;

                                    if (Client.instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                        return;

                                    PreviewPlayer.CrossfadeToDefault();
                                    HideLeaderboard();
                                    HideDifficultySelection();
                                    ShowSongsList();
                                }
                                else
                                {
                                    roomInfo.roomState = RoomState.Preparing;
                                    SongInfo selectedSong = new SongInfo(packet.additionalData);
                                    roomInfo.selectedSong = selectedSong;

                                    if (Client.instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                        return;

                                    CustomLevel selectedLevel = SongLoader.CustomLevels.First(x => x.levelID.StartsWith(selectedSong.levelId));
                                    HideLeaderboard();
                                    HideSongsList();
                                    ShowDifficultySelection(selectedLevel);
                                }
                            }
                            break;
                        case CommandType.TransferHost:
                            {
                                roomInfo.roomHost = new PlayerInfo(packet.additionalData);
                                Client.instance.isHost = Client.instance.playerInfo.Equals(roomInfo.roomHost);

                                if (Client.instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                    return;

                                switch (roomInfo.roomState)
                                {
                                    case RoomState.SelectingSong:
                                        {
                                            ShowSongsList();
                                        }
                                        break;
                                    case RoomState.Preparing:
                                        {
                                            if (roomInfo.selectedSong != null)
                                                ShowDifficultySelection(SongLoader.CustomLevels.First(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                                        }
                                        break;
                                }
                                _roomManagementViewController.UpdateViewController(Client.instance.isHost);
                            }
                            break;
                        case CommandType.StartLevel:
                            {
                                if (Client.instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                    return;

                                Client.instance.playerInfo.playerComboBlocks = 0;
                                Client.instance.playerInfo.playerCutBlocks = 0;
                                Client.instance.playerInfo.playerEnergy = 0f;
                                Client.instance.playerInfo.playerScore = 0;

                                byte difficulty = packet.additionalData[0];
                                SongInfo songInfo = new SongInfo(packet.additionalData.Skip(1).ToArray());

                                GameplayOptions gameplayOptions = new GameplayOptions();
                                gameplayOptions.noEnergy = roomInfo.noFail;

                                MainGameSceneSetupData mainGameSceneSetupData = Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().FirstOrDefault();

                                if (mainGameSceneSetupData != null)
                                {
                                    Client.instance.playerInfo.playerState = PlayerState.Game;
                                    roomInfo.roomState = RoomState.InGame;
                                    mainGameSceneSetupData.Init(SongLoader.CustomLevels.First(x => x.levelID.StartsWith(songInfo.levelId)).GetDifficultyLevel((LevelDifficulty)difficulty), gameplayOptions, GameplayMode.SoloStandard, 0f);
                                    mainGameSceneSetupData.didFinishEvent += InGameOnlineController.Instance.SongFinished;
                                    mainGameSceneSetupData.TransitionToScene(0.7f);
                                    return;
                                }
                                else
                                {
                                    Log.Error("SceneSetupData is null!");
                                }
                            }
                            break;
                        case CommandType.LeaveRoom:
                            {
                                LeaveRoom();
                            }
                            break;
                        case CommandType.UpdatePlayerInfo:
                            {
                                if (roomInfo != null && (roomInfo.roomState == RoomState.InGame || roomInfo.roomState == RoomState.Results))
                                {
                                    int playersCount = BitConverter.ToInt32(packet.additionalData, 8);

                                    Stream byteStream = new MemoryStream(packet.additionalData, 12, packet.additionalData.Length - 12);

                                    List<PlayerInfo> playerInfos = new List<PlayerInfo>();
                                    for (int j = 0; j < playersCount; j++)
                                    {
                                        byte[] sizeBytes = new byte[4];
                                        byteStream.Read(sizeBytes, 0, 4);

                                        int playerInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                                        byte[] playerInfoBytes = new byte[playerInfoSize];
                                        byteStream.Read(playerInfoBytes, 0, playerInfoSize);

                                        playerInfos.Add(new PlayerInfo(playerInfoBytes));
                                    }
                                    UpdateLeaderboard(playerInfos.ToArray(), BitConverter.ToSingle(packet.additionalData, 0), BitConverter.ToSingle(packet.additionalData, 4), (roomInfo.roomState == RoomState.Results));
                                }
                            }
                            break;
                        case CommandType.PlayerReady:
                            {
                                int playersReady = BitConverter.ToInt32(packet.additionalData, 0);
                                int playersTotal = BitConverter.ToInt32(packet.additionalData, 4);

                                if (roomInfo.roomState == RoomState.Preparing && _difficultySelectionViewController != null)
                                {
                                    _difficultySelectionViewController.SetPlayersReady(playersReady, playersTotal);
                                }
                            }
                            break;
                    }
                }catch(Exception e)
                {
                    Log.Exception("Can't parse packet!");
                    if (packet != null)
                    {
                        Log.Exception($"Packet={packet.commandType}, DataLength={packet.additionalData.Length}");
                    }
                    Log.Exception(e.ToString());
                }
            }
        }

        private void DownloadFlowCoordinator_AllSongsDownloaded()
        {
            Client.instance.playerInfo.playerState = PlayerState.Room;
            UpdateUI(roomInfo.roomState);
        }

        public void UpdateUI(RoomState state)
        {
            switch (state)
            {
                case RoomState.SelectingSong:
                    {
                        HideLeaderboard();
                        HideDifficultySelection();
                        ShowSongsList();
                    }
                    break;
                case RoomState.Preparing:
                    {
                        HideLeaderboard();
                        HideSongsList();
                        if (roomInfo.selectedSong != null)
                        {
                            ShowDifficultySelection(SongLoader.CustomLevels.First(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                            Client.instance.SendPlayerReady(Client.instance.isHost);
                        }
                    }
                    break;
                case RoomState.InGame:
                    {
                        HideDifficultySelection();
                        HideSongsList();
                        ShowLeaderboard(new PlayerInfo[0], SongLoader.CustomLevels.First(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                    }
                    break;
                case RoomState.Results:
                    {
                        HideDifficultySelection();
                        HideSongsList();
                        ShowLeaderboard(new PlayerInfo[0], SongLoader.CustomLevels.First(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                    }
                    break;
            }
            _roomManagementViewController.UpdateViewController(Client.instance.isHost);
        }


        public void ShowSongsList()
        {
            if (_songSelectionViewController == null)
            {
                _songSelectionViewController = BeatSaberUI.CreateViewController<SongSelectionViewController>();
            }
            if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_songSelectionViewController) < 0)
            {
                _roomNavigationController.PushViewController(_songSelectionViewController, false);

                List<CustomLevel> availableSongs = new List<CustomLevel>();
                availableSongsLevelIDs.ForEach(x => availableSongs.Add(SongLoader.CustomLevels.First(y => y.levelID.StartsWith(x))));

                _songSelectionViewController.SetSongs(availableSongs);
                _songSelectionViewController.SongSelected += SongSelected;
            }

            _songSelectionViewController.UpdateViewController(Client.instance.isHost);
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

        private void SongSelected(CustomLevel song)
        {
            Client.instance.SetSelectedSong(new SongInfo() { songName = song.songName + " " + song.songSubName, levelId = song.levelID.Substring(0, 32) });
        }

        private void SongLoaded(CustomLevel song)
        {
            if (_difficultySelectionViewController != null)
            {
                _difficultySelectionViewController.SetPlayButtonInteractable(true);
            }
            PreviewPlayer.CrossfadeTo(song.audioClip, song.previewStartTime, (song.audioClip.length - song.previewStartTime), 1f);
        }

        public void ShowDifficultySelection(CustomLevel song)
        {
            if (_difficultySelectionViewController == null)
            {
                _difficultySelectionViewController = BeatSaberUI.CreateViewController<DifficultySelectionViewController>();
            }
            if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_difficultySelectionViewController) < 0)
            {
                _roomNavigationController.PushViewController(_difficultySelectionViewController, false);

                _difficultySelectionViewController.DiscardPressed += DiscardPressed;
                _difficultySelectionViewController.PlayPressed += PlayPressed;
                _difficultySelectionViewController.ReadyPressed += ReadyPressed;
            }
            SongLoader.Instance.LoadAudioClipForLevel(song, SongLoaded);
            _difficultySelectionViewController.SetSelectedSong(song);
            _difficultySelectionViewController.UpdateViewController(Client.instance.isHost);
        }

        private void ReadyPressed(bool ready)
        {
            Client.instance.SendPlayerReady(ready);
        }

        private void PlayPressed(CustomLevel song, LevelDifficulty difficulty)
        {
            Client.instance.StartLevel(song, difficulty);
        }

        private void DiscardPressed()
        {
            Client.instance.SetSelectedSong(null);
        }

        public void HideDifficultySelection()
        {
            if (_difficultySelectionViewController != null)
            {
                if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_difficultySelectionViewController) >= 0)
                {
                    _roomNavigationController.PopViewControllerImmediately();
                    _difficultySelectionViewController.DiscardPressed -= DiscardPressed;
                    _difficultySelectionViewController.PlayPressed -= PlayPressed;
                    Destroy(_difficultySelectionViewController.gameObject);
                }
            }
        }

        public void ShowLeaderboard(PlayerInfo[] playerInfos, CustomLevel song)
        {
            if (_leaderboardViewController == null)
            {
                _leaderboardViewController = BeatSaberUI.CreateViewController<LeaderboardViewController>();
            }
            if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_leaderboardViewController) < 0)
            {
                _roomNavigationController.PushViewController(_leaderboardViewController, false);
            }

            SongLoader.Instance.LoadAudioClipForLevel(song, SongLoaded);
            _leaderboardViewController.SetLeaderboard(playerInfos);
            _leaderboardViewController.SelectedSong = song;
        }

        public void HideLeaderboard()
        {
            if (_leaderboardViewController != null)
            {
                if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_leaderboardViewController) >= 0)
                {
                    _roomNavigationController.PopViewControllerImmediately();
                    Destroy(_leaderboardViewController.gameObject);
                }
            }
            PreviewPlayer.CrossfadeToDefault();
        }

        public void UpdateLeaderboard(PlayerInfo[] playerInfos, float currentTime, float totalTime, bool results)
        {
            if (_leaderboardViewController != null)
            {
                _leaderboardViewController.SetLeaderboard(playerInfos);
                _leaderboardViewController.SetTimer(totalTime - currentTime, results);
            }
        }
    }
}
