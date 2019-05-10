using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using CustomUI.BeatSaber;
using HMUI;
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

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    public enum SortMode { Default, Difficulty, Newest };

    class RoomFlowCoordinator : FlowCoordinator
    {
        public event Action didFinishEvent;

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

        CustomKeyboardViewController _searchKeyboard;
        SongSelectionViewController _songSelectionViewController;
        DifficultySelectionViewController _difficultySelectionViewController;
        LeaderboardViewController _leaderboardViewController;

        PlayerManagementViewController _playerManagementViewController;
        QuickSettingsViewController _quickSettingsViewController;
        
        BeatmapCharacteristicSO[] _beatmapCharacteristics;
        BeatmapCharacteristicSO _standardCharacteristic;

        BeatmapCharacteristicSO _lastCharacteristic;
        SortMode _lastSortMode;
        string _lastSearchRequest;

        RoomInfo roomInfo;
        string lastSelectedSong;

        float currentTime;
        float totalTime;

        Song songToDownload;

        string ip;
        int port;
        uint roomId;
        bool usePassword;
        string password;

        bool joined = false;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
            _standardCharacteristic = _beatmapCharacteristics.First(x => x.characteristicName == "Standard");

            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _lastCharacteristic = _standardCharacteristic;

                _playerManagementViewController = BeatSaberUI.CreateViewController<PlayerManagementViewController>();
                _playerManagementViewController.gameplayModifiersChanged += UpdateLevelOptions;

                _quickSettingsViewController = BeatSaberUI.CreateViewController<QuickSettingsViewController>();

                _roomNavigationController = BeatSaberUI.CreateViewController<RoomNavigationController>();
                _roomNavigationController.didFinishEvent += () => { LeaveRoom(); };
                
                _searchKeyboard = BeatSaberUI.CreateViewController<CustomKeyboardViewController>();
                _searchKeyboard.enterButtonPressed += SearchPressed;
                _searchKeyboard.backButtonPressed += () => { DismissViewController(_searchKeyboard); };
                _searchKeyboard.allowEmptyInput = true;
            }

            ProvideInitialViewControllers(_roomNavigationController, _playerManagementViewController, _quickSettingsViewController);
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
            Client.Instance.MessageReceived -= PacketReceived;
            Client.Instance.MessageReceived += PacketReceived;
            Client.Instance.RequestRoomInfo();
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

                _passwordKeyboard.inputString = "";
                PresentViewController(_passwordKeyboard, null);
            }
            else
            {
                if (!Client.Instance.Connected || (Client.Instance.ip != ip || Client.Instance.port != port))
                {
                    Client.Instance.Disconnect();
                    Client.Instance.Connect(ip, port);
                    Client.Instance.ConnectedToServerHub -= ConnectedToServerHub;
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
                    InGameOnlineController.Instance.needToSendUpdates = false;
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
                Plugin.log.Info("Unable to disconnect from ServerHub properly!");
            }

            Client.Instance.MessageReceived -= PacketReceived;
            Client.Instance.ClearMessageQueue();

            if(songToDownload != null)
            {
                songToDownload.songQueueState = SongQueueState.Error;
                Client.Instance.playerInfo.playerState = PlayerState.Lobby;
            }

            InGameOnlineController.Instance.DestroyPlayerControllers();
            InGameOnlineController.Instance.VoiceChatStopRecording();
            PreviewPlayer.CrossfadeToDefault();
            lastSelectedSong = "";
            _lastCharacteristic = _standardCharacteristic;
            _lastSortMode = SortMode.Default;
            _lastSearchRequest = "";
            Client.Instance.InRoom = false;
            PopAllViewControllers(_roomNavigationController);
            didFinishEvent?.Invoke();
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
        
        private void DisconnectCommandReceived(NetIncomingMessage msg)
        {
            InGameOnlineController.Instance.needToSendUpdates = false;
            if (msg == null)
            {
                PopAllViewControllers(_roomNavigationController);
                InGameOnlineController.Instance.DestroyPlayerControllers();
                PreviewPlayer.CrossfadeToDefault();
                joined = false;

                _roomNavigationController.DisplayError("Lost connection to the ServerHub!");
            }
            else if (msg.LengthBytes > 3)
            {
                string reason = msg.ReadString();

                PopAllViewControllers(_roomNavigationController);
                InGameOnlineController.Instance.DestroyPlayerControllers();
                PreviewPlayer.CrossfadeToDefault();
                joined = false;
                lastSelectedSong = "";

                _roomNavigationController.DisplayError(reason);
            }
            else
            {
                _roomNavigationController.DisplayError("ServerHub refused connection!");
            }
        }

        private void PacketReceived(NetIncomingMessage msg)
        {
            if(msg == null)
            {
                DisconnectCommandReceived(null);
                return;
            }

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
                                Client.Instance.InRoom = true;
                                joined = true;
                                InGameOnlineController.Instance.needToSendUpdates = true;
                                if(Config.Instance.EnableVoiceChat)
                                    InGameOnlineController.Instance.VoiceChatStartRecording();
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
                                Client.Instance.playerInfo.playerState = PlayerState.Room;
                                    
                                roomInfo = new RoomInfo(msg);

                                Client.Instance.isHost = Client.Instance.playerInfo.Equals(roomInfo.roomHost);

                                UpdateUI(roomInfo.roomState);
                            }
                            break;
                        case CommandType.SetSelectedSong:
                            {
                                if (msg.LengthBytes < 16)
                                {
                                    roomInfo.roomState = RoomState.SelectingSong;
                                    roomInfo.selectedSong = null;

                                    PreviewPlayer.CrossfadeToDefault();

                                    UpdateUI(roomInfo.roomState);

                                    if (songToDownload != null)
                                    {
                                        songToDownload.songQueueState = SongQueueState.Error;
                                        Client.Instance.playerInfo.playerState = PlayerState.Room;
                                    }
                                }
                                else
                                {
                                    roomInfo.roomState = RoomState.Preparing;
                                    SongInfo selectedSong = new SongInfo(msg);
                                    roomInfo.selectedSong = selectedSong;

                                    if(roomInfo.startLevelInfo == null)
                                        roomInfo.startLevelInfo = new LevelOptionsInfo(BeatmapDifficulty.Hard, GameplayModifiers.defaultModifiers, "Standard");

                                    if (songToDownload != null)
                                    {
                                        songToDownload.songQueueState = SongQueueState.Error;
                                    }

                                    UpdateUI(roomInfo.roomState);
                                }
                            }
                            break;
                        case CommandType.SetLevelOptions:
                            {
                                roomInfo.startLevelInfo = new LevelOptionsInfo(msg);

                                _playerManagementViewController.SetGameplayModifiers(roomInfo.startLevelInfo.modifiers);

                                if (roomInfo.roomState == RoomState.SelectingSong)
                                {
                                    _difficultySelectionViewController.SetBeatmapCharacteristic(_beatmapCharacteristics.First(x => x.serializedName == roomInfo.startLevelInfo.characteristicName));

                                    if (!roomInfo.perPlayerDifficulty)
                                    {
                                        _difficultySelectionViewController.SetBeatmapDifficulty(roomInfo.startLevelInfo.difficulty);
                                    }
                                }
                            }
                            break;
                        case CommandType.GetRandomSongInfo:
                            {
                                SongInfo random = new SongInfo(SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).ToArray().Random() as BeatmapLevelSO);

                                Client.Instance.SetSelectedSong(random);
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

                                LevelOptionsInfo levelInfo = new LevelOptionsInfo(msg);

                                BeatmapCharacteristicSO characteristic = _beatmapCharacteristics.First(x => x.serializedName == levelInfo.characteristicName);
                                
                                SongInfo songInfo = new SongInfo(msg);
                                BeatmapLevelSO level = SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(songInfo.levelId)) as BeatmapLevelSO;

                                if(level == null)
                                {
                                    Plugin.log.Error("Unable to start level! Level is null! LevelID="+songInfo.levelId);
                                }

                                if (roomInfo.perPlayerDifficulty && _difficultySelectionViewController != null)
                                {

                                    StartLevel(level, characteristic, _difficultySelectionViewController.selectedDifficulty, levelInfo.modifiers);
                                }
                                else
                                {
                                    StartLevel(level, characteristic, levelInfo.difficulty, levelInfo.modifiers);
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
                                    currentTime = msg.ReadFloat();
                                    totalTime = msg.ReadFloat();

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
                                            Plugin.log.Critical($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                                        }
                                    }
                                    
                                    switch (roomInfo.roomState)
                                    {
                                        case RoomState.InGame:
                                            playerInfos = playerInfos.Where(x => x.playerScore > 0 && x.playerState == PlayerState.Game).ToList();
                                            UpdateLeaderboard(playerInfos, currentTime, totalTime, false);
                                            break;

                                        case RoomState.Results:
                                            playerInfos = playerInfos.Where(x => x.playerScore > 0 && (x.playerState == PlayerState.Game || x.playerState == PlayerState.Room)).ToList();
                                            UpdateLeaderboard(playerInfos, currentTime, totalTime, true);
                                            break;
                                    }

                                    _playerManagementViewController.UpdatePlayerList(playerInfos, roomInfo.roomState);
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
                                DisconnectCommandReceived(msg);
                            }
                            break;
                    }
                }catch(Exception e)
                {
                    Plugin.log.Error($"Unable to parse packet! Packet={commandType}, DataLength={msg.LengthBytes}\nException: {e}");
                }
            }
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
                        if(roomInfo.songSelectionType == SongSelectionType.Manual)
                            ShowSongsList(lastSelectedSong);
                    }
                    break;
                case RoomState.Preparing:
                    {
                        if (!_roomNavigationController.viewControllers.Contains(_difficultySelectionViewController))
                            PopAllViewControllers(_roomNavigationController);

                        if (roomInfo.selectedSong != null)
                        {
                            ShowDifficultySelection(roomInfo.selectedSong);
                            _playerManagementViewController.SetGameplayModifiers(roomInfo.startLevelInfo.modifiers);
                        }
                    }
                    break;
                case RoomState.InGame:
                    {
                        if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) == -1)
                            PopAllViewControllers(_roomNavigationController);

                        ShowLeaderboard(null, roomInfo.selectedSong);
                    }
                    break;
                case RoomState.Results:
                    {
                        if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) == -1)
                            PopAllViewControllers(_roomNavigationController);

                        ShowLeaderboard(null, roomInfo.selectedSong);
                    }
                    break;
            }
            _playerManagementViewController.UpdateViewController(Client.Instance.isHost);
        }
        
        private void UpdateLevelOptions()
        {
            if (Client.Instance.isHost && _playerManagementViewController != null)
            {
                if (roomInfo.roomState == RoomState.Preparing && _difficultySelectionViewController != null)
                {
                    LevelOptionsInfo info = new LevelOptionsInfo(_difficultySelectionViewController.selectedDifficulty, _playerManagementViewController.modifiers, _difficultySelectionViewController.selectedCharacteristic.serializedName);
                    Client.Instance.SetLevelOptions(info);
                    roomInfo.startLevelInfo = info;
                    Client.Instance.playerInfo.playerLevelOptions = info;
                }
                else
                {
                    LevelOptionsInfo info = new LevelOptionsInfo(BeatmapDifficulty.Hard, _playerManagementViewController.modifiers, "Standard");
                    Client.Instance.SetLevelOptions(info);
                    roomInfo.startLevelInfo = info;
                    Client.Instance.playerInfo.playerLevelOptions = info;
                }
            }
            else
            {
                if (roomInfo.roomState == RoomState.Preparing && _difficultySelectionViewController != null)
                {
                    Client.Instance.playerInfo.playerLevelOptions = new LevelOptionsInfo(_difficultySelectionViewController.selectedDifficulty, _playerManagementViewController.modifiers, _difficultySelectionViewController.selectedCharacteristic.serializedName);
                }
                else
                {
                    Client.Instance.playerInfo.playerLevelOptions = new LevelOptionsInfo(BeatmapDifficulty.Hard, _playerManagementViewController.modifiers, "Standard");
                }
            }
        }

        public void StartLevel(BeatmapLevelSO level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers, float startTime = 0f)
        {
            Client.Instance.playerInfo.playerComboBlocks = 0;
            Client.Instance.playerInfo.playerCutBlocks = 0;
            Client.Instance.playerInfo.playerTotalBlocks = 0;
            Client.Instance.playerInfo.playerEnergy = 0f;
            Client.Instance.playerInfo.playerScore = 0;
            Client.Instance.playerInfo.playerLevelOptions = new LevelOptionsInfo(difficulty, modifiers, characteristic.serializedName);

            MenuTransitionsHelperSO menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().FirstOrDefault();

            if(_playerManagementViewController != null)
            {
                _playerManagementViewController.SetGameplayModifiers(modifiers);
            }

            if (menuSceneSetupData != null)
            {
                if (Config.Instance.SpectatorMode)
                {
                    Client.Instance.playerInfo.playerState = PlayerState.Spectating;
                    modifiers.noFail = true;
                }
                else
                {
                    Client.Instance.playerInfo.playerState = PlayerState.Game;
                }

                PlayerSpecificSettings playerSettings = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().currentLocalPlayer.playerSpecificSettings;

                roomInfo.roomState = RoomState.InGame;
                
                IDifficultyBeatmap difficultyBeatmap = level.GetDifficultyBeatmap(characteristic, difficulty, true);

#if DEBUG
                Plugin.log.Info($"Starting song: name={level.songName}, levelId={level.levelID}, difficulty={difficulty}");
#endif

                Client.Instance.MessageReceived -= PacketReceived;

                try
                {
                    BS_Utils.Gameplay.Gamemode.NextLevelIsIsolated("Beat Saber Multiplayer");
                }
                catch
                {

                }

                PracticeSettings practiceSettings = new PracticeSettings(PracticeSettings.defaultPracticeSettings);
                practiceSettings.startSongTime = startTime + 1.5f;
                practiceSettings.songSpeedMul = modifiers.songSpeedMul;

                menuSceneSetupData.StartStandardLevel(difficultyBeatmap, modifiers, playerSettings, (startTime > 1f ? practiceSettings : null), false, () => {}, (StandardLevelScenesTransitionSetupDataSO sender, LevelCompletionResults levelCompletionResults) => { InGameOnlineController.Instance.SongFinished(sender, levelCompletionResults, difficultyBeatmap, modifiers, false); });
                return;
            }
            else
            {
                Plugin.log.Error("SceneSetupData is null!");
            }
        }

        public void PopAllViewControllers(VRUINavigationController controller)
        {
            HideSongsList();
            HideDifficultySelection();
            HideLeaderboard();
        }

        public void ShowSongsList(string lastLevelId = "")
        {
            if (_songSelectionViewController == null)
            {
                _songSelectionViewController = BeatSaberUI.CreateViewController<SongSelectionViewController>();
                _songSelectionViewController.SongSelected += SongSelected;
                _songSelectionViewController.SortPressed += (sortMode) => { SetSongs(sortMode, _lastSearchRequest);  };
                _songSelectionViewController.SearchPressed += () => { _searchKeyboard.inputString = ""; PresentViewController(_searchKeyboard, null);};
            }
            if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _songSelectionViewController, null, true);
                SetSongs(_lastSortMode, _lastSearchRequest);

                if (!string.IsNullOrEmpty(lastLevelId))
                {
                    _songSelectionViewController.ScrollToLevel(lastLevelId);
                }
            }

            _songSelectionViewController.UpdateViewController(Client.Instance.isHost);
        }

        public void HideSongsList()
        {
            
            if (_songSelectionViewController != null)
            {
                if(_roomNavigationController.childViewController == _searchKeyboard)
                {
                    DismissViewController(_searchKeyboard, null, true);
                }
                if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                }
            }
        }

        public void SearchPressed(string input)
        {
            DismissViewController(_searchKeyboard);
            SetSongs(_lastSortMode, input);
        }

        public void SetSongs(SortMode sortMode, string searchRequest)
        {
            _lastSortMode = sortMode;
            _lastSearchRequest = searchRequest;

            List<BeatmapLevelSO> levels = new List<BeatmapLevelSO>();
            
            levels = SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.Where(x => x.packID == "CustomMaps" || CustomExtensions.basePackIDs.Contains(x.packID)).SelectMany(x => x.beatmapLevelCollection.beatmapLevels).Cast<BeatmapLevelSO>().ToList();

            if (string.IsNullOrEmpty(searchRequest))
            {
                switch (sortMode)
                {
                    case SortMode.Newest: { levels = SortLevelsByCreationTime(levels); }; break;
                    case SortMode.Difficulty:
                        {
                            levels = levels.AsParallel().OrderBy(x => { int index = ScrappedData.Songs.FindIndex(y => x.levelID.StartsWith(y.Hash)); return (index == -1 ? (x.levelID.Length < 32 ? int.MaxValue : int.MaxValue - 1) : index); }).ToList();
                        }; break;
                }
            }
            else
            {
                levels = levels.Where(x => ($"{x.songName} {x.songSubName} {x.levelAuthorName} {x.songAuthorName}".ToLower().Contains(searchRequest))).ToList();
            }
            
            _songSelectionViewController.SetSongs(levels);
        }

        public List<BeatmapLevelSO> SortLevelsByCreationTime(List<BeatmapLevelSO> levels)
        {
            DirectoryInfo customSongsFolder = new DirectoryInfo(Environment.CurrentDirectory.Replace('\\', '/') + "/CustomSongs/");

            List<string> sortedFolders = customSongsFolder.GetDirectories().OrderByDescending(x => x.CreationTime.Ticks).Select(x => x.FullName.Replace('\\', '/')).ToList();

            List<string> sortedLevelIDs = new List<string>();

            foreach (string path in sortedFolders)
            {
                CustomLevel song = SongLoader.CustomLevels.FirstOrDefault(x => x.customSongInfo.path.StartsWith(path));
                if (song != null)
                {
                    sortedLevelIDs.Add(song.levelID);
                }
            }

            List<BeatmapLevelSO> notSorted = new List<BeatmapLevelSO>(levels);

            List<BeatmapLevelSO> sortedLevels = new List<BeatmapLevelSO>();

            foreach (string levelId in sortedLevelIDs)
            {
                BeatmapLevelSO data = notSorted.FirstOrDefault(x => x.levelID == levelId);
                if (data != null)
                {
                    sortedLevels.Add(data);
                }
            }

            sortedLevels.AddRange(notSorted.Except(sortedLevels));

            return sortedLevels;
        }

        private void SongSelected(BeatmapLevelSO  song)
        {
            lastSelectedSong = song.levelID;
            Client.Instance.SetSelectedSong(new SongInfo(song));
            UpdateLevelOptions();
        }

        private void SongLoaded(BeatmapLevelSO  song)
        {
            if (_difficultySelectionViewController != null)
            {
                _difficultySelectionViewController.SetPlayButtonInteractable(true);
            }
            PreviewPlayer.CrossfadeTo(song.beatmapLevelData.audioClip, song.previewStartTime, (song.beatmapLevelData.audioClip.length - song.previewStartTime), 1f);
        }

        public void ShowDifficultySelection(SongInfo song)
        {
            if (song == null)
                return;

            if (_difficultySelectionViewController == null)
            {
                _difficultySelectionViewController = BeatSaberUI.CreateViewController<DifficultySelectionViewController>();
                _difficultySelectionViewController.discardPressed += DiscardPressed;
                _difficultySelectionViewController.playPressed += (level, characteristic, difficulty) => { PlayPressed(level, characteristic, difficulty, _playerManagementViewController.modifiers); };
                _difficultySelectionViewController.levelOptionsChanged += UpdateLevelOptions;
            }

            if (!_roomNavigationController.viewControllers.Contains(_difficultySelectionViewController))
            {
                PushViewControllerToNavigationController(_roomNavigationController, _difficultySelectionViewController, null, true);
            }            

            _difficultySelectionViewController.SetSelectedSong(song);
            _difficultySelectionViewController.UpdateViewController(Client.Instance.isHost, roomInfo.perPlayerDifficulty);
            
            BeatmapLevelSO selectedLevel = SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.Where(x => x.packID == "CustomMaps" || CustomExtensions.basePackIDs.Contains(x.packID)).SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(song.levelId)) as BeatmapLevelSO;

            if (selectedLevel != null)
            {
                if (selectedLevel is CustomLevel)
                {
                    _difficultySelectionViewController.SetPlayButtonInteractable(false);
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)selectedLevel, SongLoaded);
                }
                else
                {
                    _difficultySelectionViewController.SetPlayButtonInteractable(true);
                    PreviewPlayer.CrossfadeTo(selectedLevel.beatmapLevelData.audioClip, selectedLevel.previewStartTime, (selectedLevel.beatmapLevelData.audioClip.length - selectedLevel.previewStartTime), 1f);
                }
                Client.Instance.SendPlayerReady(true);
                Client.Instance.playerInfo.playerState = PlayerState.Room;
            }
            else
            {
                Client.Instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                Client.Instance.SendPlayerReady(false);
                _difficultySelectionViewController.SetPlayButtonInteractable(false);
                SongDownloader.Instance.RequestSongByLevelID(song.levelId, (info)=>
                {
                    Client.Instance.playerInfo.playerState = PlayerState.DownloadingSongs;

                    songToDownload = info;

                    SongDownloader.Instance.DownloadSong(songToDownload, "MultiplayerSongs", () =>
                    {
                        Action<SongLoader, List<CustomLevel>> onLoaded = null;
                        onLoaded = (sender, songs) =>
                        {
                            SongLoader.SongsLoadedEvent -= onLoaded;
                            Client.Instance.playerInfo.playerState = PlayerState.Room;
                            selectedLevel = songs.FirstOrDefault(x => x.levelID.StartsWith(roomInfo.selectedSong.levelId));
                            if (selectedLevel != null)
                            {
                                SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)selectedLevel,
                                 (levelLoaded) =>
                                 {
                                     PreviewPlayer.CrossfadeTo(levelLoaded.beatmapLevelData.audioClip, levelLoaded.previewStartTime, (levelLoaded.beatmapLevelData.audioClip.length - levelLoaded.previewStartTime));
                                 });
                                _difficultySelectionViewController.SetSelectedSong(song);
                                _difficultySelectionViewController.SetPlayButtonInteractable(true);
                                _difficultySelectionViewController.SetProgressBarState(false, 1f);
                                Client.Instance.SendPlayerReady(true);
                                Client.Instance.playerInfo.playerState = PlayerState.Room;
                            }
                        };

                        SongLoader.SongsLoadedEvent += onLoaded;
                        songToDownload = null;
                    },
                    (progress) =>
                    {
                        float clampedProgress = Math.Min(progress, 0.99f);
                        _difficultySelectionViewController.SetProgressBarState(true, clampedProgress);
                        Client.Instance.playerInfo.playerProgress = 100f * clampedProgress;
                    });
                });
            }
        }

        private void PlayPressed(BeatmapLevelSO song, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers)
        {
            Client.Instance.StartLevel(song, characteristic, difficulty, modifiers);
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
                }
            }
        }

        public void ShowLeaderboard(List<PlayerInfo> playerInfos, SongInfo song)
        {
            if (_leaderboardViewController == null)
            {
                _leaderboardViewController = BeatSaberUI.CreateViewController<LeaderboardViewController>();
                _leaderboardViewController.playNowButtonPressed += PlayNow_Pressed;
            }
            if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _leaderboardViewController, null, true);
            }

            BeatmapLevelSO level = SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(song.levelId)) as BeatmapLevelSO;

            if (level != null)
            {
                if (level is CustomLevel)
                {
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level, SongLoaded);
                }
                else
                {
                    PreviewPlayer.CrossfadeTo(level.beatmapLevelData.audioClip, level.previewStartTime, (level.beatmapLevelData.audioClip.length - level.previewStartTime), 1f);
                }
            }
            _leaderboardViewController.SetLeaderboard(playerInfos);
            _leaderboardViewController.SetSong(song);
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

        public void UpdateLeaderboard(List<PlayerInfo> playerInfos, float currentTime, float totalTime, bool results)
        {
            if (_leaderboardViewController != null)
            {
                _leaderboardViewController.SetLeaderboard(playerInfos);
                _leaderboardViewController.SetTimer(totalTime - currentTime, results);
            }
        }

        private void PlayNow_Pressed()
        {
            SongInfo info = roomInfo.selectedSong;
            BeatmapLevelSO level = SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(info.levelId)) as BeatmapLevelSO;
            if (level == null)
            {
                SongDownloader.Instance.RequestSongByLevelID(info.levelId,
                (song) =>
                {
                    SongDownloader.Instance.DownloadSong(song, "MultiplayerSongs",
                    () =>
                    {
                        SongLoader.SongsLoadedEvent += PlayNow_SongsLoaded;
                        _leaderboardViewController.SetProgressBarState(false, 0f);
                    },
                    (progress) =>
                    {
                        _leaderboardViewController.SetProgressBarState((progress > 0f), progress);
                    });
                });
            }
            else
            {
                if (level is CustomLevel)
                {
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level,
                    (levelLoaded) =>
                    {
                        _leaderboardViewController.SetProgressBarState(false, 0f);
                        BeatmapCharacteristicSO characteristic = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>().FirstOrDefault(x => x.serializedName == roomInfo.startLevelInfo.characteristicName);
                        StartLevel(levelLoaded, characteristic, roomInfo.startLevelInfo.difficulty, roomInfo.startLevelInfo.modifiers, currentTime);
                    });
                }
                else
                {
                    BeatmapCharacteristicSO characteristic = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>().FirstOrDefault(x => x.serializedName == roomInfo.startLevelInfo.characteristicName);
                    StartLevel(level, characteristic, roomInfo.startLevelInfo.difficulty, roomInfo.startLevelInfo.modifiers, currentTime);
                }
            }
        }

        private void PlayNow_SongsLoaded(SongLoader arg1, List<CustomLevel> arg2)
        {
            SongLoader.SongsLoadedEvent -= PlayNow_SongsLoaded;
            PlayNow_Pressed();
        }
    }
}
