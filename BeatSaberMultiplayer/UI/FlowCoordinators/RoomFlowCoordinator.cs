using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using CustomUI.BeatSaber;
using HMUI;
using Lidgren.Network;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

        AdditionalContentModelSO _contentModelSO;
        BeatmapLevelsModelSO _beatmapLevelsModel;

        CustomKeyboardViewController _passwordKeyboard;
        RoomNavigationController _roomNavigationController;

        CustomKeyboardViewController _searchKeyboard;
        SongSelectionViewController _songSelectionViewController;
        DifficultySelectionViewController _difficultySelectionViewController;
        LeaderboardViewController _leaderboardViewController;
        LevelPacksViewController _packsViewController;

        PlayerManagementViewController _playerManagementViewController;
        QuickSettingsViewController _quickSettingsViewController;
        
        BeatmapCharacteristicSO[] _beatmapCharacteristics;

        IBeatmapLevelPack _lastSelectedPack;
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

        float roomInfoRequestTime;

        bool joined = false;
        private SimpleDialogPromptViewController _passHostDialog;
        private SimpleDialogPromptViewController _hostLeaveDialog;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
            _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModelSO>().FirstOrDefault();
            _contentModelSO = Resources.FindObjectsOfTypeAll<AdditionalContentModelSO>().FirstOrDefault();

            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _playerManagementViewController = BeatSaberUI.CreateViewController<PlayerManagementViewController>();
                _playerManagementViewController.gameplayModifiersChanged += UpdateLevelOptions;
                _playerManagementViewController.transferHostButtonPressed += TransferHostConfirmation;

                var dialogOrig = ReflectionUtil.GetPrivateField<SimpleDialogPromptViewController>(FindObjectOfType<MainFlowCoordinator>(), "_simpleDialogPromptViewController");
                _passHostDialog = Instantiate(dialogOrig.gameObject).GetComponent<SimpleDialogPromptViewController>();
                _hostLeaveDialog = Instantiate(dialogOrig.gameObject).GetComponent<SimpleDialogPromptViewController>();

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

        public void ReturnToRoom()
        {
            joined = true;
            Client.Instance.MessageReceived -= PacketReceived;
            Client.Instance.MessageReceived += PacketReceived;
            if (Client.Instance.connected)
            {
                Client.Instance.RequestRoomInfo();
                roomInfoRequestTime = Time.time;
            }
            else
            {
                DisconnectCommandReceived(null);
            }
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
                if (!Client.Instance.connected || (Client.Instance.ip != ip || Client.Instance.port != port))
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

        public void LeaveRoom(bool force = false)
        {
            if(Client.Instance != null && Client.Instance.isHost && !force )
            {
                _hostLeaveDialog.Init("Leave room?", $"You're the host, are you sure you want to leave the room?", "Leave", "Cancel",
                (selectedButton) =>
                {
                    DismissViewController(_hostLeaveDialog);
                    if (selectedButton == 0)
                    {
                        LeaveRoom(true);
                    }
                });
                PresentViewController(_hostLeaveDialog);
                return;
            }

            try
            {
                if (joined)
                {
                    InGameOnlineController.Instance.needToSendUpdates = false;
                    Client.Instance.LeaveRoom();
                    joined = false;
                }
                if (Client.Instance.connected)
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
                Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Lobby;
            }

            InGameOnlineController.Instance.DestroyPlayerControllers();
            InGameOnlineController.Instance.VoiceChatStopRecording();
            PreviewPlayer.CrossfadeToDefault();
            lastSelectedSong = "";
            _lastSortMode = SortMode.Default;
            _lastSearchRequest = "";
            Client.Instance.inRoom = false;
            PopAllViewControllers();
            if(leftScreenViewController == _passHostDialog)
            {
                SetLeftScreenViewController(_playerManagementViewController);
            }
            didFinishEvent?.Invoke();
        }

        private void PasswordEntered(string input)
        {
            DismissViewController(_passwordKeyboard);
            if (!string.IsNullOrEmpty(input))
            {
                JoinRoom(ip, port, roomId, usePassword, input.ToUpper());
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
                PopAllViewControllers();
                InGameOnlineController.Instance.DestroyPlayerControllers();
                PreviewPlayer.CrossfadeToDefault();
                joined = false;

                _roomNavigationController.DisplayError("Lost connection to the ServerHub!");
            }
            else if (msg.LengthBytes > 3)
            {
                string reason = msg.ReadString();

                PopAllViewControllers();
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
            msg.Position = 0;

            CommandType commandType = (CommandType)msg.ReadByte();

            if (!joined)
            {
                if(commandType == CommandType.JoinRoom)
                {
                    switch (msg.ReadByte())
                    {
                        case 0:
                            {

                                Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;
                                Client.Instance.RequestRoomInfo();
                                roomInfoRequestTime = Time.time;
                                Client.Instance.SendPlayerInfo(true);
                                Client.Instance.inRoom = true;
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
                    if (roomInfo == null && commandType != CommandType.GetRoomInfo)
                        if (Time.time - roomInfoRequestTime < 1.5f)
                            return;
                        else
                            throw new ArgumentNullException("RoomInfo is null! Need to wait for it to arrive...");

                    switch (commandType)
                    {
                        case CommandType.GetRoomInfo:
                            {
                                roomInfo = new RoomInfo(msg);

                                Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;

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
                                        Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;
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

                                _playerManagementViewController.SetGameplayModifiers(roomInfo.startLevelInfo.modifiers.ToGameplayModifiers());

                                if (roomInfo.roomState == RoomState.Preparing)
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
                                SongInfo random = new SongInfo(SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).ToArray().Random());

                                Client.Instance.SetSelectedSong(random);
                            }
                            break;
                        case CommandType.TransferHost:
                            {
                                roomInfo.roomHost = new PlayerInfo(msg);
                                Client.Instance.isHost = Client.Instance.playerInfo.Equals(roomInfo.roomHost);

                                if (Client.Instance.playerInfo.updateInfo.playerState == PlayerState.DownloadingSongs)
                                    return;

                                UpdateUI(roomInfo.roomState);
                            }
                            break;
                        case CommandType.StartLevel:
                            {

                                if (Client.Instance.playerInfo.updateInfo.playerState == PlayerState.DownloadingSongs)
                                    return;

                                LevelOptionsInfo levelInfo = new LevelOptionsInfo(msg);

                                BeatmapCharacteristicSO characteristic = _beatmapCharacteristics.First(x => x.serializedName == levelInfo.characteristicName);
                                
                                SongInfo songInfo = new SongInfo(msg);
                                IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(songInfo.levelId));

                                if(level == null)
                                {
                                    Plugin.log.Error("Unable to start level! Level is null! LevelID="+songInfo.levelId);
                                }
                                else
                                {
                                    LoadBeatmapLevelAsync(level,
                                        (status, success, beatmapLevel) =>
                                        {
                                            if (roomInfo.perPlayerDifficulty && _difficultySelectionViewController != null)
                                            {

                                                StartLevel(beatmapLevel, characteristic, _difficultySelectionViewController.selectedDifficulty, levelInfo.modifiers.ToGameplayModifiers());
                                            }
                                            else
                                            {
                                                StartLevel(beatmapLevel, characteristic, levelInfo.difficulty, levelInfo.modifiers.ToGameplayModifiers());
                                            }
                                        });
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

                                    if(roomInfo.roomState == RoomState.InGame || roomInfo.roomState == RoomState.Results)
                                        UpdateLeaderboard(currentTime, totalTime, roomInfo.roomState == RoomState.Results);

                                    _playerManagementViewController.UpdatePlayerList(roomInfo.roomState);
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

        public void UpdateUI(RoomState state)
        {
            switch (state)
            {
                case RoomState.SelectingSong:
                    {
                        PopAllViewControllers();
                        if(roomInfo.songSelectionType == SongSelectionType.Manual)
                            ShowSongsList(lastSelectedSong);
                    }
                    break;
                case RoomState.Preparing:
                    {
                        if (!_roomNavigationController.viewControllers.Contains(_difficultySelectionViewController))
                            PopAllViewControllers();

                        if (roomInfo.selectedSong != null)
                        {
                            ShowDifficultySelection(roomInfo.selectedSong);
                            _playerManagementViewController.SetGameplayModifiers(roomInfo.startLevelInfo.modifiers.ToGameplayModifiers());
                        }
                    }
                    break;
                case RoomState.InGame:
                    {
                        if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) == -1)
                            PopAllViewControllers();

                        ShowLeaderboard(null, roomInfo.selectedSong);
                    }
                    break;
                case RoomState.Results:
                    {
                        if (_roomNavigationController.viewControllers.IndexOf(_leaderboardViewController) == -1)
                            PopAllViewControllers();

                        ShowLeaderboard(null, roomInfo.selectedSong);
                    }
                    break;
            }
            _playerManagementViewController.UpdateViewController(Client.Instance.isHost, (int)state <= 1); 
        }
        
        private void UpdateLevelOptions()
        {
            try
            {
                if (_playerManagementViewController != null && roomInfo != null)
                {
                    if (Client.Instance.isHost)
                    {
                        if (roomInfo.roomState == RoomState.Preparing && _difficultySelectionViewController != null)
                        {
                            LevelOptionsInfo info = new LevelOptionsInfo(_difficultySelectionViewController.selectedDifficulty, _playerManagementViewController.modifiers, _difficultySelectionViewController.selectedCharacteristic.serializedName);
                            Client.Instance.SetLevelOptions(info);
                            roomInfo.startLevelInfo = info;
                            Client.Instance.playerInfo.updateInfo.playerLevelOptions = info;
                        }
                        else
                        {
                            LevelOptionsInfo info = new LevelOptionsInfo(BeatmapDifficulty.Hard, _playerManagementViewController.modifiers, "Standard");
                            Client.Instance.SetLevelOptions(info);
                            roomInfo.startLevelInfo = info;
                            Client.Instance.playerInfo.updateInfo.playerLevelOptions = info;
                        }
                    }
                    else
                    {
                        if (roomInfo.roomState == RoomState.Preparing && _difficultySelectionViewController != null)
                        {
                            Client.Instance.playerInfo.updateInfo.playerLevelOptions = new LevelOptionsInfo(_difficultySelectionViewController.selectedDifficulty, _playerManagementViewController.modifiers, _difficultySelectionViewController.selectedCharacteristic.serializedName);
                        }
                        else
                        {
                            Client.Instance.playerInfo.updateInfo.playerLevelOptions = new LevelOptionsInfo(BeatmapDifficulty.Hard, _playerManagementViewController.modifiers, "Standard");
                        }
                    }
                }
            }catch(Exception e)
            {
                Plugin.log.Critical($"Unable to update level options! Exception: {e}");
            }
        }

        public void TransferHostConfirmation(PlayerInfo newHost)
        {
            _passHostDialog.Init("Pass host?", $"Are you sure you want to pass host to <b>{newHost.playerName}</b>?", "Pass host", "Cancel",
                (selectedButton) =>
                {
                    SetLeftScreenViewController(_playerManagementViewController);
                    if (selectedButton == 0)
                    {
                        Client.Instance.TransferHost(newHost);
                    }
                });
            SetLeftScreenViewController(_passHostDialog);
        }

        public void StartLevel(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers, float startTime = 0f)
        {
            Client.Instance.playerInfo.updateInfo.playerComboBlocks = 0;
            Client.Instance.playerInfo.updateInfo.playerCutBlocks = 0;
            Client.Instance.playerInfo.updateInfo.playerTotalBlocks = 0;
            Client.Instance.playerInfo.updateInfo.playerEnergy = 0f;
            Client.Instance.playerInfo.updateInfo.playerScore = 0;
            Client.Instance.playerInfo.updateInfo.playerLevelOptions = new LevelOptionsInfo(difficulty, modifiers, characteristic.serializedName);

            MenuTransitionsHelperSO menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().FirstOrDefault();

            if(_playerManagementViewController != null)
            {
                _playerManagementViewController.SetGameplayModifiers(modifiers);
            }

            if (menuSceneSetupData != null)
            {
                Client.Instance.playerInfo.updateInfo.playerState = Config.Instance.SpectatorMode ? PlayerState.Spectating : PlayerState.Game;

                PlayerSpecificSettings playerSettings = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().currentLocalPlayer.playerSpecificSettings;

                roomInfo.roomState = RoomState.InGame;
                
                IDifficultyBeatmap difficultyBeatmap = level.GetDifficultyBeatmap(characteristic, difficulty, false);

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
                practiceSettings.startInAdvanceAndClearNotes = true;

                menuSceneSetupData.StartStandardLevel(difficultyBeatmap, modifiers, playerSettings, (startTime > 1f ? practiceSettings : null), "Lobby", false, () => {}, (StandardLevelScenesTransitionSetupDataSO sender, LevelCompletionResults levelCompletionResults) => { InGameOnlineController.Instance.SongFinished(sender, levelCompletionResults, difficultyBeatmap, modifiers, startTime > 1f); });
            }
            else
            {
                Plugin.log.Error("SceneSetupData is null!");
            }
        }

        public void PopAllViewControllers()
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
                _songSelectionViewController.SortPressed += (sortMode) => { SetSongs(_lastSelectedPack, sortMode, _lastSearchRequest);  };
                _songSelectionViewController.SearchPressed += () => { _searchKeyboard.inputString = ""; PresentViewController(_searchKeyboard, null);};
            }

            if(_packsViewController == null)
            {
                _packsViewController = Instantiate(Resources.FindObjectsOfTypeAll<LevelPacksViewController>().First(x => x.name != "CustomLevelPacksViewController"));
                _packsViewController.name = "CustomLevelPacksViewController";


                if(_lastSelectedPack == null)
                {
                    _lastSelectedPack = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks[0];
                }

                _packsViewController.didSelectPackEvent += (sender, selectedPack) => { SetSongs(selectedPack, _lastSortMode, _lastSearchRequest); };
            }

            _packsViewController.SetData(SongCore.Loader.CustomBeatmapLevelPackCollectionSO, SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.FindIndexInArray(_lastSelectedPack));
            
            if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _songSelectionViewController, null, true);
                SetSongs(_lastSelectedPack, _lastSortMode, _lastSearchRequest);

                if (!string.IsNullOrEmpty(lastLevelId))
                {
                    _songSelectionViewController.ScrollToLevel(lastLevelId);
                }
            }

            if (Client.Instance.isHost)
            {
                _packsViewController.gameObject.SetActive(true);
                SetBottomScreenViewController(_packsViewController);
            }
            else
            {
                _packsViewController.gameObject.SetActive(false);
                SetBottomScreenViewController(null);
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

            if(bottomScreenViewController != null)
            {
                SetBottomScreenViewController(null);
            }
        }

        public void SearchPressed(string input)
        {
            DismissViewController(_searchKeyboard);
            SetSongs(_lastSelectedPack,  _lastSortMode, input);
        }

        public void SetSongs(IBeatmapLevelPack selectedPack, SortMode sortMode, string searchRequest)
        {
            _lastSortMode = sortMode;
            _lastSearchRequest = searchRequest;
            _lastSelectedPack = selectedPack;

            List<IPreviewBeatmapLevel> levels = new List<IPreviewBeatmapLevel>();

            if (_lastSelectedPack != null)
            {
                levels = _lastSelectedPack.beatmapLevelCollection.beatmapLevels.ToList();

                if (string.IsNullOrEmpty(searchRequest))
                {
                    switch (sortMode)
                    {
                        case SortMode.Newest: { levels = SortLevelsByCreationTime(levels); }; break;
                        case SortMode.Difficulty:
                            {
                                levels = levels.AsParallel().OrderByDescending(x =>
                                {
                                    var diffs = ScrappedData.Songs.FirstOrDefault(y => x.levelID.Contains(y.Hash)).Diffs;
                                    if (diffs != null && diffs.Count > 0)
                                        return diffs.Max(y => y.Stars);
                                    else
                                        return -1;
                                }).ToList();
                            }; break;
                    }
                }
                else
                {
                    levels = levels.Where(x => ($"{x.songName} {x.songSubName} {x.levelAuthorName} {x.songAuthorName}".ToLower().Contains(searchRequest))).ToList();
                }
            }
            
            _songSelectionViewController.SetSongs(levels);
        }

        public List<IPreviewBeatmapLevel> SortLevelsByCreationTime(List<IPreviewBeatmapLevel> levels)
        {
            DirectoryInfo customSongsFolder = new DirectoryInfo(CustomLevelPathHelper.customLevelsDirectoryPath);

            List<string> sortedFolders = customSongsFolder.GetDirectories().OrderByDescending(x => x.CreationTime.Ticks).Select(x => x.FullName).ToList();

            List<string> sortedLevelPaths = new List<string>();

            for (int i = 0; i < sortedFolders.Count; i++)
            {
                if (SongCore.Loader.CustomLevels.TryGetValue(sortedFolders[i], out var song))
                {
                    sortedLevelPaths.Add(song.customLevelPath);
                }
            }
            List<IPreviewBeatmapLevel> notSorted = new List<IPreviewBeatmapLevel>(levels);

            List<IPreviewBeatmapLevel> sortedLevels = new List<IPreviewBeatmapLevel>();

            for (int i2 = 0; i2 < sortedLevelPaths.Count; i2++)
            {
                IPreviewBeatmapLevel data = notSorted.FirstOrDefault(x => {
                    if (x is CustomPreviewBeatmapLevel)
                        return (x as CustomPreviewBeatmapLevel).customLevelPath == sortedLevelPaths[i2];
                    else
                        return false;
                });
                if (data != null)
                {
                    sortedLevels.Add(data);
                }

            }
            sortedLevels.AddRange(notSorted.Except(sortedLevels));

            return sortedLevels;
        }

        private void SongSelected(IPreviewBeatmapLevel song)
        {
            lastSelectedSong = song.levelID;
            Client.Instance.SetSelectedSong(new SongInfo(song));
            UpdateLevelOptions();
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
            
            _difficultySelectionViewController.UpdateViewController(Client.Instance.isHost, roomInfo.perPlayerDifficulty);

            IPreviewBeatmapLevel selectedLevel = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID == song.levelId);

            if (selectedLevel != null)
            {
                _difficultySelectionViewController.SetPlayButtonInteractable(false);
                _difficultySelectionViewController.SetLoadingState(true);

                LoadBeatmapLevelAsync(selectedLevel,
                    (status, success, level) =>
                    {
                        if(status == AdditionalContentModelSO.EntitlementStatus.NotOwned)
                        {
                            _difficultySelectionViewController.SetSelectedSong(selectedLevel);
                            _difficultySelectionViewController.SetPlayButtonInteractable(false);
                            Client.Instance.SendPlayerReady(false);
                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;
                            Client.Instance.playerInfo.updateInfo.playerProgress = 0f; selectedLevel.GetPreviewAudioClipAsync(new CancellationToken()).ContinueWith(
                                 (res) =>
                                 {
                                     if (!res.IsFaulted)
                                     {
                                         PreviewPlayer.CrossfadeTo(res.Result, selectedLevel.previewStartTime, (res.Result.length - selectedLevel.previewStartTime));
                                         _difficultySelectionViewController.SetSongDuration(res.Result.length);
                                     }
                                 });

                        }
                        else if (success)
                        {
                            _difficultySelectionViewController.SetSelectedSong(level);

                            if (level.beatmapLevelData.audioClip != null)
                            {
                                PreviewPlayer.CrossfadeTo(level.beatmapLevelData.audioClip, selectedLevel.previewStartTime, (level.beatmapLevelData.audioClip.length - selectedLevel.previewStartTime), 1f);
                                _difficultySelectionViewController.SetPlayButtonInteractable(true);
                                Client.Instance.SendPlayerReady(true);
                            }
                            else
                            {
                                _difficultySelectionViewController.SetPlayButtonInteractable(false);
                                Client.Instance.SendPlayerReady(false);
                            }

                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;

                        }
                        else
                        {
                            _difficultySelectionViewController.SetSelectedSong(song);
                            _difficultySelectionViewController.SetPlayButtonInteractable(false);
                            Client.Instance.SendPlayerReady(false);
                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;
                        }
                    });
            }
            else
            {
                _difficultySelectionViewController.SetSelectedSong(song);
                Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;
                Client.Instance.SendPlayerReady(false);
                _difficultySelectionViewController.SetPlayButtonInteractable(false);
                SongDownloader.Instance.RequestSongByLevelID(song.hash, (info)=>
                {
                    Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;

                    songToDownload = info;

                    SongDownloader.Instance.DownloadSong(songToDownload, 
                        (success) =>
                    {
                        void onLoaded(SongCore.Loader sender, Dictionary<string, CustomPreviewBeatmapLevel> songs)
                        {
                            SongCore.Loader.SongsLoadedEvent -= onLoaded;
                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;
                            roomInfo.selectedSong.UpdateLevelId();
                            selectedLevel = songs.FirstOrDefault(x => x.Value.levelID == roomInfo.selectedSong.levelId).Value;
                            if (selectedLevel != null)
                            {
                                LoadBeatmapLevelAsync(selectedLevel,
                                    (status, loaded, level) =>
                                    {
                                        if (loaded)
                                        {
                                            PreviewPlayer.CrossfadeTo(level.beatmapLevelData.audioClip, level.previewStartTime, (level.beatmapLevelData.audioClip.length - level.previewStartTime));
                                            _difficultySelectionViewController.SetSelectedSong(level);
                                            _difficultySelectionViewController.SetPlayButtonInteractable(true);
                                            _difficultySelectionViewController.SetProgressBarState(false, 1f);
                                            Client.Instance.SendPlayerReady(true);
                                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;
                                        }
                                        else
                                        {
                                            Plugin.log.Error($"Unable to load level!");
                                        }
                                    });
                            }
                            else
                            {
                                Plugin.log.Error($"Level with ID {roomInfo.selectedSong.levelId} not found!");
                            }
                        }

                        SongCore.Loader.SongsLoadedEvent += onLoaded;

                        SongCore.Loader.Instance.RefreshSongs(false);
                        songToDownload = null;
                    },
                    (progress) =>
                    {
                        float clampedProgress = Math.Min(progress, 0.99f);
                        _difficultySelectionViewController.SetProgressBarState(true, clampedProgress);
                        Client.Instance.playerInfo.updateInfo.playerProgress = 100f * clampedProgress;
                    });
                });
            }
        }

        private async void LoadBeatmapLevelAsync(IPreviewBeatmapLevel selectedLevel, Action<AdditionalContentModelSO.EntitlementStatus, bool, IBeatmapLevel> callback)
        {
            var token = new CancellationTokenSource();

            var entitlementStatus  = await _contentModelSO.GetLevelEntitlementStatusAsync(selectedLevel.levelID, token.Token);

            if (entitlementStatus == AdditionalContentModelSO.EntitlementStatus.Owned)
            {
                BeatmapLevelsModelSO.GetBeatmapLevelResult getBeatmapLevelResult = await _beatmapLevelsModel.GetBeatmapLevelAsync(selectedLevel.levelID, token.Token);

                callback?.Invoke(entitlementStatus, !getBeatmapLevelResult.isError, getBeatmapLevelResult.beatmapLevel);
            }
            else
            {
                callback?.Invoke(entitlementStatus, false, null);
            }
        }

        private void PlayPressed(IPreviewBeatmapLevel song, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers)
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

            IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(song.levelId));

            if (level != null)
            {
                LoadBeatmapLevelAsync(level,
                    (status, success, beatmapLevel) =>
                    {
                        PreviewPlayer.CrossfadeTo(beatmapLevel.beatmapLevelData.audioClip, beatmapLevel.previewStartTime, (beatmapLevel.beatmapLevelData.audioClip.length - beatmapLevel.previewStartTime), 1f);
                    });
            }
            _leaderboardViewController.SetLeaderboard();
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

        public void UpdateLeaderboard(float currentTime, float totalTime, bool results)
        {
            if (_leaderboardViewController != null)
            {
                _leaderboardViewController.SetLeaderboard();
                _leaderboardViewController.SetTimer((int)(totalTime - currentTime), results);
            }
        }

        private void PlayNow_Pressed()
        {
            SongInfo info = roomInfo.selectedSong;
            IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID == info.levelId);
            if (level == null)
            {
                SongDownloader.Instance.RequestSongByLevelID(info.hash,
                (song) =>
                {
                    SongDownloader.Instance.DownloadSong(song,
                    (success) =>
                    {
                        if (success)
                        {
                            SongCore.Loader.SongsLoadedEvent += PlayNow_SongsLoaded;
                            SongCore.Loader.Instance.RefreshSongs(false);
                        }
                        else
                        {
                            _leaderboardViewController.SetProgressBarState(true, 1f);
                        }
                    },
                    (progress) =>
                    {
                        _leaderboardViewController.SetProgressBarState((progress > 0f), progress);
                    });
                });
            }
            else
            {
                _leaderboardViewController.SetProgressBarState(true, 1f);
                LoadBeatmapLevelAsync(level, 
                    (status, success, beatmapLevel) =>
                    {

                        _leaderboardViewController.SetProgressBarState(false, 0f);

                        BeatmapCharacteristicSO characteristic = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>().FirstOrDefault(x => x.serializedName == roomInfo.startLevelInfo.characteristicName);
                        StartLevel(beatmapLevel, characteristic, roomInfo.startLevelInfo.difficulty, roomInfo.startLevelInfo.modifiers.ToGameplayModifiers(), currentTime);

                    });

            }
        }

        private void PlayNow_SongsLoaded(SongCore.Loader arg1, Dictionary<string, CustomPreviewBeatmapLevel> arg2)
        {
            SongCore.Loader.SongsLoadedEvent -= PlayNow_SongsLoaded;
            roomInfo.selectedSong.UpdateLevelId();
            PlayNow_Pressed();
        }
    }
}
