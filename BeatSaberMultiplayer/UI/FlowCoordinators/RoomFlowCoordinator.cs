using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using CustomUI.BeatSaber;
using SimpleJSON;
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
using UnityEngine.Networking;
using VRUI;
using Logger = BeatSaberMultiplayer.Misc.Logger;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class RoomFlowCoordinator : FlowCoordinator
    {
        public event Action didFinishEvent;

        private static List<SongInfo> _roomSongList = new List<SongInfo>();

        private SongPreviewPlayer _songPreviewPlayer;

        public SongPreviewPlayer PreviewPlayer
        {
            get
            {
                if (_songPreviewPlayer == null)
                {
                    _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
                }

                return _songPreviewPlayer;
            }
            private set { _songPreviewPlayer = value; }
        }

        CustomKeyboardViewController _passwordKeyboard;
        RoomNavigationController _roomNavigationController;

        SongSelectionViewController _songSelectionViewController;
        DifficultySelectionViewController _difficultySelectionViewController;
        LeaderboardViewController _leaderboardViewController;
        VotingViewController _votingViewController;
        DownloadQueueViewController _downloadQueueViewController;

        RoomManagementViewController _roomManagementViewController;

        LevelCollectionSO _levelCollection;
        BeatmapCharacteristicSO[] _beatmapCharacteristics;
        BeatmapCharacteristicSO _standardCharacteristics;

        RoomInfo roomInfo;
        string lastSelectedSong;

        string ip;
        int port;
        uint roomId;
        bool usePassword;
        string password;

        bool joined = false;

        List<SongInfo> availableSongInfos = new List<SongInfo>();

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
            _standardCharacteristics = _beatmapCharacteristics.First(x => x.characteristicName == "Standard");
            _levelCollection = SongLoader.CustomLevelCollectionSO;

            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _roomManagementViewController = BeatSaberUI.CreateViewController<RoomManagementViewController>();
                _roomManagementViewController.DestroyRoomPressed += DestroyRoomPressed;

                _roomNavigationController = BeatSaberUI.CreateViewController<RoomNavigationController>();
            }
            
            ProvideInitialViewControllers(_roomNavigationController, _roomManagementViewController, null);
        }

        private void DestroyRoomPressed()
        {
            if (joined)
            {
                Client.instance.DestroyRoom();
            }
        }

        public void ReturnToRoom()
        {
            joined = true;
            ConnectedToServerHub();
            Client.instance.RequestRoomInfo(false);
        }

        public void JoinRoom(string ip, int port, uint roomId, bool usePassword, string password = "")
        {
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
                    _passwordKeyboard.backButtonPressed += () => { DismissViewController(_passwordKeyboard); didFinishEvent?.Invoke(); };
                }

                _passwordKeyboard._inputString = "";
                PresentViewController(_passwordKeyboard, null);
            }
            else
            {

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
            try
            {
                if (joined)
                {
                    Client.instance.LeaveRoom();
                    joined = false;
                }
                if (Client.instance.Connected)
                {
                    Client.instance.Disconnect();
                }
            }
            catch
            {
                Logger.Info("Unable to disconnect from ServerHub properly!");
            }


            PopAllViewControllers(_roomNavigationController);
            InGameOnlineController.Instance.DestroyAvatars();
            PreviewPlayer.CrossfadeToDefault();
            _roomSongList.Clear();
            lastSelectedSong = "";
            didFinishEvent?.Invoke();
            PluginUI.instance.serverHubFlowCoordinator.UpdateRoomsList();
        }

        private void PasswordEntered(string input)
        {
            DismissViewController(_passwordKeyboard);
            if (!string.IsNullOrEmpty(input))
            {
                JoinRoom(ip, port, roomId, usePassword, input);
            }
        }

        private void ConnectedToServerHub()
        {
            Client.instance.ConnectedToServerHub -= ConnectedToServerHub;
            Client.instance.PacketReceived -= PacketReceived;
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
                                _roomNavigationController.DisplayError("Unable to join room!\n" + "Room not found");
                            }
                            break;
                        case 2:
                            {
                                _roomNavigationController.DisplayError("Unable to join room!\n" + "Incorrect password");
                            }
                            break;
                        case 3:
                            {
                                _roomNavigationController.DisplayError("Unable to join room!\n" + "Too much players");
                            }
                            break;
                        default:
                            {
                                _roomNavigationController.DisplayError("Unable to join room!\n" + "Unknown error");
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
                                Client.instance.RemovePacketsFromQueue(CommandType.GetRoomInfo);
                                if (packet.additionalData[0] == 1)
                                {
                                    int songsCount = BitConverter.ToInt32(packet.additionalData, 1);

                                    Dictionary<SongInfo, bool> songsBuffer = new Dictionary<SongInfo, bool>();
                                    availableSongInfos.Clear();

                                    Stream byteStream = new MemoryStream(packet.additionalData, 5, packet.additionalData.Length - 5);

                                    for (int j = 0; j < songsCount; j++)
                                    {
                                        byte[] sizeBytes = new byte[4];
                                        byteStream.Read(sizeBytes, 0, 4);

                                        int songInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                                        byte[] songInfoBytes = new byte[songInfoSize];
                                        byteStream.Read(songInfoBytes, 0, songInfoSize);

                                        SongInfo song = new SongInfo(songInfoBytes);

                                        availableSongInfos.Add(song);
                                        if (!songsBuffer.ContainsKey(song))
                                        {
                                            songsBuffer.Add(song, _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).Any(x => x.levelID.StartsWith(song.levelId)));
                                        }
                                        else
                                        {
                                            Logger.Warning("Song "+song.songName+" is already listed!");
                                        }
                                    }

                                    _roomSongList = availableSongInfos.ToList();

                                    byte[] roomInfoSizeBytes = new byte[4];
                                    byteStream.Read(roomInfoSizeBytes, 0, 4);

                                    int roomInfoSize = BitConverter.ToInt32(roomInfoSizeBytes, 0);

                                    byte[] roomInfoBytes = new byte[roomInfoSize];
                                    byteStream.Read(roomInfoBytes, 0, roomInfoSize);

                                    roomInfo = new RoomInfo(roomInfoBytes);
                                    Client.instance.isHost = Client.instance.playerInfo.Equals(roomInfo.roomHost);


                                    if (songsBuffer.All(x => x.Value))
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
                                        Client.instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                                        foreach (var song in songsBuffer.Where(x => !x.Value))
                                        {
                                            StartCoroutine(EnqueueSongByLevelID(song.Key));
                                        }
                                    }
                                }
                                else
                                {                                    
                                    Client.instance.playerInfo.playerState = PlayerState.Room;

                                    if (BitConverter.ToInt32(packet.additionalData, 1) == packet.additionalData.Length - 5)
                                    {
                                        roomInfo = new RoomInfo(packet.additionalData.Skip(5).ToArray());
                                    }
                                    else
                                    {
                                        roomInfo = new RoomInfo(packet.additionalData.Skip(1).ToArray());
                                    }
                                    Client.instance.isHost = Client.instance.playerInfo.Equals(roomInfo.roomHost);

                                    availableSongInfos = _roomSongList.ToList();

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

                                    PopAllViewControllers(_roomNavigationController);
                                    if (roomInfo.songSelectionType == SongSelectionType.Voting)
                                    {
                                        ShowVotingList();
                                    }
                                    else
                                    {
                                        ShowSongsList(lastSelectedSong);
                                    }
                                }
                                else
                                {
                                    roomInfo.roomState = RoomState.Preparing;
                                    SongInfo selectedSong = new SongInfo(packet.additionalData);
                                    roomInfo.selectedSong = selectedSong;

                                    if (Client.instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                        return;

                                    LevelSO selectedLevel = _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(x => x.levelID.StartsWith(selectedSong.levelId));

                                    PopAllViewControllers(_roomNavigationController);
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

                                UpdateUI(roomInfo.roomState);
                            }
                            break;
                        case CommandType.StartLevel:
                            {

                                if (Client.instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                    return;
                                
                                lastSelectedSong = "";

                                Client.instance.playerInfo.playerComboBlocks = 0;
                                Client.instance.playerInfo.playerCutBlocks = 0;
                                Client.instance.playerInfo.playerEnergy = 0f;
                                Client.instance.playerInfo.playerScore = 0;

                                byte difficulty = packet.additionalData[0];
                                SongInfo songInfo = new SongInfo(packet.additionalData.Skip(1).ToArray());

                                MenuSceneSetupDataSO menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuSceneSetupDataSO>().FirstOrDefault();

                                if (menuSceneSetupData != null)
                                {
                                    GameplayModifiers gameplayModifiers = new GameplayModifiers();

                                    if (Config.Instance.SpectatorMode)
                                    {
                                        Client.instance.playerInfo.playerState = PlayerState.Spectating;
                                        gameplayModifiers.noFail = true;
                                    }
                                    else
                                    {
                                        Client.instance.playerInfo.playerState = PlayerState.Game;
                                        gameplayModifiers.noFail = true;
                                    }

                                    PlayerSpecificSettings playerSettings = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().currentLocalPlayer.playerSpecificSettings;

                                    roomInfo.roomState = RoomState.InGame;

                                    Client.instance.PacketReceived -= PacketReceived;

                                    LevelSO level = _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(x => x.levelID.StartsWith(songInfo.levelId));
                                    IDifficultyBeatmap difficultyBeatmap = level.GetDifficultyBeatmap((BeatmapDifficulty)difficulty);

                                    Logger.Info($"Starting song: name={level.songName}, levelId={level.levelID}, difficulty={(BeatmapDifficulty)difficulty}");

                                    menuSceneSetupData.StartStandardLevel(difficultyBeatmap, gameplayModifiers, playerSettings, null, null, (StandardLevelSceneSetupDataSO sender, LevelCompletionResults levelCompletionResults) => { InGameOnlineController.Instance.SongFinished(sender, levelCompletionResults, difficultyBeatmap, gameplayModifiers); });
                                    return;
                                }
                                else
                                {
                                    Logger.Error("SceneSetupData is null!");
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
                                if (roomInfo != null)
                                {
                                    if (roomInfo.roomState == RoomState.InGame || roomInfo.roomState == RoomState.Results)
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

                                        if (roomInfo.roomState == RoomState.InGame)
                                        {
                                            playerInfos = playerInfos.Where(x => x.playerScore > 0 && x.playerState == PlayerState.Game).ToList();
                                        }else if(roomInfo.roomState == RoomState.Results)
                                        {
                                            playerInfos = playerInfos.Where(x => x.playerScore > 0 && (x.playerState == PlayerState.Game || x.playerState == PlayerState.Room)).ToList();
                                        }

                                        UpdateLeaderboard(playerInfos.ToArray(), BitConverter.ToSingle(packet.additionalData, 0), BitConverter.ToSingle(packet.additionalData, 4), (roomInfo.roomState == RoomState.Results));
                                    }
                                    else if(roomInfo.roomState == RoomState.SelectingSong && roomInfo.songSelectionType == SongSelectionType.Voting)
                                    {
                                        UpdateVotingList(BitConverter.ToSingle(packet.additionalData, 0), BitConverter.ToSingle(packet.additionalData, 4));
                                    }
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
                        case CommandType.Disconnect:
                            {
                                if (packet.additionalData != null && packet.additionalData.Length > 0)
                                {
                                    string reason = Encoding.UTF8.GetString(packet.additionalData, 4, BitConverter.ToInt32(packet.additionalData, 0));

                                    PopAllViewControllers(_roomNavigationController);
                                    InGameOnlineController.Instance.DestroyAvatars();
                                    PreviewPlayer.CrossfadeToDefault();
                                    joined = false;
                                    _roomSongList.Clear();
                                    lastSelectedSong = "";

                                    _roomNavigationController.DisplayError(reason);
                                }
                                else
                                {
                                    _roomNavigationController.DisplayError("ServerHub refused connection!");
                                }

                            }
                            break;
                    }
                }catch(Exception e)
                {
                    Logger.Exception("Unable to parse packet!");
                    if (packet != null)
                    {
                        Logger.Exception($"Packet={packet.commandType}, DataLength={packet.additionalData.Length}");
                    }
                    Logger.Exception(e.ToString());
                }
            }
        }

        public IEnumerator EnqueueSongByLevelID(SongInfo song)
        {
#if DEBUG
            Log.Info("Downloading " + song.levelId);
#endif
            if (_downloadQueueViewController == null)
            {
                _downloadQueueViewController = BeatSaberUI.CreateViewController<DownloadQueueViewController>();
            }
            if (_roomNavigationController.viewControllers.IndexOf(_downloadQueueViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _downloadQueueViewController);
            }

            UnityWebRequest wwwId = UnityWebRequest.Get($"{Config.Instance.BeatSaverURL}/api/songs/search/hash/" + song.levelId);
            wwwId.timeout = 10;

            yield return wwwId.SendWebRequest();


            if (wwwId.isNetworkError || wwwId.isHttpError)
            {
                Logger.Error(wwwId.error);
            }
            else
            {
#if DEBUG
                Log.Info("Received response from BeatSaver...");
#endif
                JSONNode node = JSON.Parse(wwwId.downloadHandler.text);

                if (node["songs"].Count == 0)
                {
                    Logger.Error($"Song {song.songName} doesn't exist on BeatSaver!");
                    _downloadQueueViewController.EnqueueSong(new Song() { songName = song.songName, authorName = "", coverUrl = "", songQueueState = SongQueueState.Error, hash = song.levelId });

                    yield break;
                }

                Song _tempSong = Song.FromSearchNode(node["songs"][0]);

                _downloadQueueViewController.EnqueueSong(_tempSong);
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
                        PopAllViewControllers(_roomNavigationController);
                        if (roomInfo.songSelectionType == SongSelectionType.Voting)
                        {
                            ShowVotingList();
                        }
                        else
                        {
                            ShowSongsList(lastSelectedSong);
                        }
                    }
                    break;
                case RoomState.Preparing:
                    {
                        PopAllViewControllers(_roomNavigationController);
                        if (roomInfo.selectedSong != null)
                        {
                            ShowDifficultySelection(_levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                            Client.instance.SendPlayerReady(Client.instance.isHost);
                        }
                    }
                    break;
                case RoomState.InGame:
                    {
                        PopAllViewControllers(_roomNavigationController);
                        ShowLeaderboard(new PlayerInfo[0], _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).FirstOrDefault(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                    }
                    break;
                case RoomState.Results:
                    {
                        PopAllViewControllers(_roomNavigationController);

                        ShowLeaderboard(new PlayerInfo[0], _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).FirstOrDefault(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                    }
                    break;
            }
            _roomManagementViewController.UpdateViewController(Client.instance.isHost);
        }

        public void PopAllViewControllers(VRUINavigationController controller)
        {
            HideQueue();
            HideSongsList();
            HideVotingList();
            HideDifficultySelection();
            HideLeaderboard();
        }

        public void ShowSongsList(string lastLevelId = "")
        {
            if (_songSelectionViewController == null)
            {
                _songSelectionViewController = BeatSaberUI.CreateViewController<SongSelectionViewController>();
            }
            if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _songSelectionViewController, null, true);
                List<LevelSO> availableSongs = new List<LevelSO>();
                availableSongInfos.ForEach(x => availableSongs.Add(_levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(y => y.levelID.StartsWith(x.levelId))));

                _songSelectionViewController.SetSongs(availableSongs);

                if (!string.IsNullOrEmpty(lastLevelId))
                {
                    _songSelectionViewController.ScrollToLevel(lastLevelId);
                }
                _songSelectionViewController.SongSelected += SongSelected;
            }

            _songSelectionViewController.UpdateViewController(Client.instance.isHost || roomInfo.songSelectionType == SongSelectionType.Voting);
        }

        public void HideSongsList()
        {
            
            if (_songSelectionViewController != null)
            {
                if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                    _songSelectionViewController.SongSelected -= SongSelected;
                    Destroy(_songSelectionViewController.gameObject);
                }
            }
        }

        private void SongSelected(LevelSO  song)
        {
            lastSelectedSong = song.levelID;
            Client.instance.SetSelectedSong(new SongInfo() { songName = song.songName + " " + song.songSubName, levelId = song.levelID.Substring(0, Math.Min(32, song.levelID.Length)) });
        }

        private void SongLoaded(LevelSO  song)
        {
            if (_difficultySelectionViewController != null)
            {
                _difficultySelectionViewController.SetPlayButtonInteractable(true);
            }
            PreviewPlayer.CrossfadeTo(song.audioClip, song.previewStartTime, (song.audioClip.length - song.previewStartTime), 1f);
        }

        public void ShowDifficultySelection(LevelSO  song)
        {
            if (_difficultySelectionViewController == null)
            {
                _difficultySelectionViewController = BeatSaberUI.CreateViewController<DifficultySelectionViewController>();
            }
            if (_roomNavigationController.viewControllers.IndexOf(_difficultySelectionViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _difficultySelectionViewController, null, true);

                _difficultySelectionViewController.DiscardPressed += DiscardPressed;
                _difficultySelectionViewController.PlayPressed += PlayPressed;
                _difficultySelectionViewController.ReadyPressed += ReadyPressed;
            }
            if (song is CustomLevel)
            {
                SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)song, SongLoaded);
            }
            else
            {
                _difficultySelectionViewController.SetPlayButtonInteractable(true);
                PreviewPlayer.CrossfadeTo(song.audioClip, song.previewStartTime, (song.audioClip.length - song.previewStartTime), 1f);
            }
            _difficultySelectionViewController.SetSelectedSong(song);
            _difficultySelectionViewController.UpdateViewController(Client.instance.isHost);
        }

        private void ReadyPressed(bool ready)
        {
            Client.instance.SendPlayerReady(ready);
        }

        private void PlayPressed(LevelSO  song, BeatmapDifficulty difficulty)
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
                if (_roomNavigationController.viewControllers.IndexOf(_difficultySelectionViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                    _difficultySelectionViewController.DiscardPressed -= DiscardPressed;
                    _difficultySelectionViewController.PlayPressed -= PlayPressed;
                    Destroy(_difficultySelectionViewController.gameObject);
                }
            }
        }

        public void ShowLeaderboard(PlayerInfo[] playerInfos, LevelSO  song)
        {
            if (_leaderboardViewController == null)
            {
                _leaderboardViewController = BeatSaberUI.CreateViewController<LeaderboardViewController>();
            }
            if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _leaderboardViewController, null, true);
            }
            if (song != null && song is CustomLevel)
            {
                SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)song, SongLoaded);
            }
            else
            {
                PreviewPlayer.CrossfadeTo(song.audioClip, song.previewStartTime, (song.audioClip.length - song.previewStartTime), 1f);
            }
            _leaderboardViewController.SetLeaderboard(playerInfos);
            _leaderboardViewController.SelectedSong = song;
        }

        public void HideLeaderboard()
        {
            if (_leaderboardViewController != null)
            {
                if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
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

        public void ShowVotingList()
        {
            if (_votingViewController == null)
            {
                _votingViewController = BeatSaberUI.CreateViewController<VotingViewController>();
            }
            if (_roomNavigationController.viewControllers.IndexOf(_votingViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _votingViewController, null, true);

                List<LevelSO > availableSongs = new List<LevelSO >();
                availableSongInfos.ForEach(x => availableSongs.Add(_levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(y => y.levelID.StartsWith(x.levelId))));

                _votingViewController.SetSongs(availableSongs);
                _votingViewController.SongSelected += SongSelected;
            }
        }

        public void HideVotingList()
        {
            if (_votingViewController != null)
            {
                if (_roomNavigationController.viewControllers.IndexOf(_votingViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                    Destroy(_votingViewController.gameObject);
                }
            }
        }

        public void UpdateVotingList(float currentTime, float totalTime)
        {
            if (_votingViewController != null)
            {
                _votingViewController.SetTimer(totalTime - currentTime);
            }
        }

        public void HideQueue()
        {
            if (_downloadQueueViewController != null)
            {
                if (_roomNavigationController.viewControllers.IndexOf(_votingViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                    Destroy(_downloadQueueViewController.gameObject);
                }
            }
        }
    }
}
