using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using CustomUI.BeatSaber;
using Lidgren.Network;
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
                _roomNavigationController.didFinishEvent += () => { LeaveRoom(); };
            }
            
            ProvideInitialViewControllers(_roomNavigationController, _roomManagementViewController, null);
        }

        private void DestroyRoomPressed()
        {
            if (joined)
            {
                Client.Instance.DestroyRoom();
            }
        }

        public void ReturnToRoom()
        {
            joined = true;
            Client.Instance.MessageReceived += PacketReceived;
            Client.Instance.RequestRoomInfo(false);
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
                if (!Client.Instance.Connected || (Client.Instance.Connected && (Client.Instance.ip != ip || Client.Instance.port != port)))
                {
                    Client.Instance.Disconnect(true);
                    Client.Instance.Connect(ip, port);
                    Client.Instance.ConnectedToServerHub += ConnectedToServerHub;
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
                    Client.Instance.LeaveRoom();
                    joined = false;
                }
                if (Client.Instance.Connected)
                {
                    Client.Instance.Disconnect();
                }
            }
            catch
            {
                Logger.Info("Unable to disconnect from ServerHub properly!");
            }


            InGameOnlineController.Instance.DestroyAvatars();
            PreviewPlayer.CrossfadeToDefault();
            _roomSongList.Clear();
            lastSelectedSong = "";
            PopAllViewControllers(_roomNavigationController);
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
            Client.Instance.ConnectedToServerHub -= ConnectedToServerHub;
            Client.Instance.MessageReceived -= PacketReceived;
            Client.Instance.MessageReceived += PacketReceived;
            if (!joined)
            {
                if (usePassword)
                {
                    Client.Instance.JoinRoom(roomId, password);
                }
                else
                {
                    Client.Instance.JoinRoom(roomId);
                }
            }
        }
        
        private void PacketReceived(NetIncomingMessage msg)
        {
            CommandType commandType = (CommandType)msg.ReadByte();
            if (!joined)
            {
                if(commandType == CommandType.JoinRoom)
                {
                    switch (msg.ReadByte())
                    {
                        case 0:
                            {

                                Client.Instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                                Client.Instance.RequestRoomInfo();
                                Client.Instance.SendPlayerInfo();
                                joined = true;
                            }
                            break;
                        case 1:
                            {
                                _roomNavigationController.DisplayError("Unable to join room!\nRoom not found");
                            }
                            break;
                        case 2:
                            {
                                _roomNavigationController.DisplayError("Unable to join room!\nIncorrect password");
                            }
                            break;
                        case 3:
                            {
                                _roomNavigationController.DisplayError("Unable to join room!\nToo much players");
                            }
                            break;
                        default:
                            {
                                _roomNavigationController.DisplayError("Unable to join room!\nUnknown error");
                            }
                            break;

                    }
                }
            }
            else
            {
                try
                {
                    switch (commandType)
                    {
                        case CommandType.GetRoomInfo:
                            {
                                if (msg.ReadByte() == 1)
                                {
                                    int songsCount = msg.ReadInt32();

                                    Dictionary<SongInfo, bool> songsBuffer = new Dictionary<SongInfo, bool>();
                                    availableSongInfos.Clear();

                                    for (int j = 0; j < songsCount; j++)
                                    {
                                        SongInfo song = new SongInfo(msg);

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
                                    
                                    roomInfo = new RoomInfo(msg);
                                    Client.Instance.isHost = Client.Instance.playerInfo.Equals(roomInfo.roomHost);


                                    if (songsBuffer.All(x => x.Value))
                                    {
                                        Client.Instance.playerInfo.playerState = PlayerState.Room;
#if DEBUG
                                        Logger.Info("All songs downloaded!");
#endif
                                        UpdateUI(roomInfo.roomState);
                                    }
                                    else
                                    {
#if DEBUG
                                        Logger.Info("Downloading missing songs...");
#endif
                                        Client.Instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                                        foreach (var song in songsBuffer.Where(x => !x.Value))
                                        {
                                            StartCoroutine(EnqueueSongByLevelID(song.Key));
                                        }
                                    }
                                }
                                else
                                {                                    
                                    Client.Instance.playerInfo.playerState = PlayerState.Room;
                                    
                                    roomInfo = new RoomInfo(msg);

                                    Client.Instance.isHost = Client.Instance.playerInfo.Equals(roomInfo.roomHost);

                                    availableSongInfos = _roomSongList.ToList();

                                    UpdateUI(roomInfo.roomState);
                                }
                            }
                            break;
                        case CommandType.SetSelectedSong:
                            {

                                if (msg.LengthBytes < 16 || msg.PeekInt32() == 0)
                                {
                                    roomInfo.roomState = RoomState.SelectingSong;
                                    roomInfo.selectedSong = null;

                                    if (Client.Instance.playerInfo.playerState == PlayerState.DownloadingSongs)
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
                                    SongInfo selectedSong = new SongInfo(msg);
                                    roomInfo.selectedSong = selectedSong;

                                    if (Client.Instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                        return;

                                    LevelSO selectedLevel = _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(x => x.levelID.StartsWith(selectedSong.levelId));

                                    PopAllViewControllers(_roomNavigationController);
                                    ShowDifficultySelection(selectedLevel);
                                }
                            }
                            break;
                        case CommandType.TransferHost:
                            {
                                roomInfo.roomHost = new PlayerInfo(msg);
                                Client.Instance.isHost = Client.Instance.playerInfo.Equals(roomInfo.roomHost);

                                if (Client.Instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                    return;

                                UpdateUI(roomInfo.roomState);
                            }
                            break;
                        case CommandType.StartLevel:
                            {

                                if (Client.Instance.playerInfo.playerState == PlayerState.DownloadingSongs)
                                    return;
                                
                                lastSelectedSong = "";

                                Client.Instance.playerInfo.playerComboBlocks = 0;
                                Client.Instance.playerInfo.playerCutBlocks = 0;
                                Client.Instance.playerInfo.playerEnergy = 0f;
                                Client.Instance.playerInfo.playerScore = 0;

                                byte difficulty = msg.ReadByte();
                                SongInfo songInfo = new SongInfo(msg);

                                MenuSceneSetupDataSO menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuSceneSetupDataSO>().FirstOrDefault();

                                if (menuSceneSetupData != null)
                                {
                                    GameplayModifiers gameplayModifiers = new GameplayModifiers();

                                    if (Config.Instance.SpectatorMode)
                                    {
                                        Client.Instance.playerInfo.playerState = PlayerState.Spectating;
                                        gameplayModifiers.noFail = true;
                                    }
                                    else
                                    {
                                        Client.Instance.playerInfo.playerState = PlayerState.Game;
                                        gameplayModifiers.noFail = roomInfo.noFail;
                                    }

                                    PlayerSpecificSettings playerSettings = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().currentLocalPlayer.playerSpecificSettings;

                                    roomInfo.roomState = RoomState.InGame;

                                    Client.Instance.MessageReceived -= PacketReceived;

                                    LevelSO level = _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(x => x.levelID.StartsWith(songInfo.levelId));
                                    IDifficultyBeatmap difficultyBeatmap = level.GetDifficultyBeatmap((BeatmapDifficulty)difficulty);

                                    Logger.Info($"Starting song: name={level.songName}, levelId={level.levelID}, difficulty={(BeatmapDifficulty)difficulty}");

                                    Client.Instance.MessageReceived -= PacketReceived;
                                    menuSceneSetupData.StartStandardLevel(difficultyBeatmap, gameplayModifiers, playerSettings, null, null, (StandardLevelSceneSetupDataSO sender, LevelCompletionResults levelCompletionResults) => { InGameOnlineController.Instance.SongFinished(sender, levelCompletionResults, difficultyBeatmap, gameplayModifiers, false); });
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
                                        float currentTime = msg.ReadFloat();
                                        float totalTime = msg.ReadFloat();

                                        int playersCount = msg.ReadInt32();

                                        List<PlayerInfo> playerInfos = new List<PlayerInfo>();
                                        for (int j = 0; j < playersCount; j++)
                                        {
                                            try
                                            {
                                                playerInfos.Add(new PlayerInfo(msg));
                                            }
                                            catch (Exception e)
                                            {
#if DEBUG
                                                Misc.Logger.Exception($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                                            }
                                        }

                                        if (roomInfo.roomState == RoomState.InGame)
                                        {
                                            playerInfos = playerInfos.Where(x => x.playerScore > 0 && x.playerState == PlayerState.Game).ToList();
                                        }else if(roomInfo.roomState == RoomState.Results)
                                        {
                                            playerInfos = playerInfos.Where(x => x.playerScore > 0 && (x.playerState == PlayerState.Game || x.playerState == PlayerState.Room)).ToList();
                                        }

                                        UpdateLeaderboard(playerInfos.ToArray(), currentTime, totalTime, (roomInfo.roomState == RoomState.Results));
                                    }
                                    else if(roomInfo.roomState == RoomState.SelectingSong && roomInfo.songSelectionType == SongSelectionType.Voting)
                                    {
                                        UpdateVotingList(msg.ReadFloat(), msg.ReadFloat());
                                    }
                                }
                            }
                            break;
                        case CommandType.PlayerReady:
                            {
                                int playersReady = msg.ReadInt32();
                                int playersTotal = msg.ReadInt32();

                                if (roomInfo.roomState == RoomState.Preparing && _difficultySelectionViewController != null)
                                {
                                    _difficultySelectionViewController.SetPlayersReady(playersReady, playersTotal);
                                }
                            }
                            break;
                        case CommandType.Disconnect:
                            {
                                if (msg.LengthBytes > 3)
                                {
                                    string reason = msg.ReadString();

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
                    if (msg != null)
                    {
                        Logger.Exception($"Packet={commandType}, DataLength={msg.LengthBytes}");
                    }
                    Logger.Exception(e.ToString());
                }
            }
        }

        public IEnumerator EnqueueSongByLevelID(SongInfo song)
        {
#if DEBUG
            Logger.Info("Downloading " + song.levelId);
#endif
            if (_downloadQueueViewController == null)
            {
                _downloadQueueViewController = BeatSaberUI.CreateViewController<DownloadQueueViewController>();
                _downloadQueueViewController.allSongsDownloaded += DownloadFlowCoordinator_AllSongsDownloaded;
            }
            if (_roomNavigationController.viewControllers.IndexOf(_downloadQueueViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _downloadQueueViewController, null, true);
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
                Logger.Info("Received response from BeatSaver...");
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
            SongLoader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
            SongLoader.Instance.RefreshSongs(false);
        }

        private void SongLoader_SongsLoadedEvent(SongLoader arg1, List<CustomLevel> arg2)
        {
            SongLoader.SongsLoadedEvent -= SongLoader_SongsLoadedEvent;
            Client.Instance.playerInfo.playerState = PlayerState.Room;
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
                        if (_roomNavigationController.viewControllers.IndexOf(_difficultySelectionViewController) == -1)
                            PopAllViewControllers(_roomNavigationController);
                        if (roomInfo.selectedSong != null)
                        {
                            ShowDifficultySelection(_levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                            Client.Instance.SendPlayerReady(Client.Instance.isHost);
                        }
                    }
                    break;
                case RoomState.InGame:
                    {
                        if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) == -1)
                            PopAllViewControllers(_roomNavigationController);

                        ShowLeaderboard(new PlayerInfo[0], _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).FirstOrDefault(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                    }
                    break;
                case RoomState.Results:
                    {
                        if(_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) == -1)
                            PopAllViewControllers(_roomNavigationController);

                        ShowLeaderboard(new PlayerInfo[0], _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).FirstOrDefault(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId)));
                    }
                    break;
            }
            _roomManagementViewController.UpdateViewController(Client.Instance.isHost);
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
                LevelSO[] levels = _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics);
                
                availableSongInfos.ForEach(x => {
                    LevelSO availableLevel = levels.FirstOrDefault(y => y.levelID.StartsWith(x.levelId));
                    if (availableLevel != null)
                        availableSongs.Add(availableLevel);
                    else
                        Logger.Warning("Unable to find song "+x.songName+"("+x.levelId+")");
                });

                _songSelectionViewController.SetSongs(availableSongs);

                if (!string.IsNullOrEmpty(lastLevelId))
                {
                    _songSelectionViewController.ScrollToLevel(lastLevelId);
                }
                _songSelectionViewController.SongSelected += SongSelected;
            }

            _songSelectionViewController.UpdateViewController(Client.Instance.isHost || roomInfo.songSelectionType == SongSelectionType.Voting);
        }

        public void HideSongsList()
        {
            
            if (_songSelectionViewController != null)
            {
                if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                    _songSelectionViewController.SongSelected -= SongSelected;
                }
            }
        }

        private void SongSelected(LevelSO  song)
        {
            lastSelectedSong = song.levelID;
            Client.Instance.SetSelectedSong(new SongInfo() { songName = song.songName + " " + song.songSubName, levelId = song.levelID.Substring(0, Math.Min(32, song.levelID.Length)) });
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
            _difficultySelectionViewController.UpdateViewController(Client.Instance.isHost);
        }

        private void ReadyPressed(bool ready)
        {
            Client.Instance.SendPlayerReady(ready);
        }

        private void PlayPressed(LevelSO  song, BeatmapDifficulty difficulty)
        {
            Client.Instance.StartLevel(song, difficulty);
        }

        private void DiscardPressed()
        {
            Client.Instance.SetSelectedSong(null);
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
                if (_roomNavigationController.viewControllers.IndexOf(_downloadQueueViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                }
            }
        }
    }
}
