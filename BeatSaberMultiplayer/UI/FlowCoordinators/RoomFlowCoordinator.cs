using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using BS_Utils.Utilities;
using Discord;
using HMUI;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

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

        public IDifficultyBeatmap levelDifficultyBeatmap;
        public LevelCompletionResults levelResults;
        public int lastHighscoreForLevel;
        public bool lastHighscoreValid;

        AdditionalContentModel _contentModelSO;
        BeatmapLevelsModel _beatmapLevelsModel;

        RoomNavigationController _roomNavigationController;

        ModalKeyboard _searchKeyboard;
        SongSelectionViewController _songSelectionViewController;
        DifficultySelectionViewController _difficultySelectionViewController;
        MultiplayerResultsViewController _resultsViewController;
        PlayingNowViewController _playingNowViewController;
        LevelPacksUIViewController _levelPacksViewController;

        PlayerManagementViewController _playerManagementViewController;
        QuickSettingsViewController _quickSettingsViewController;

        BeatmapCharacteristicSO[] _beatmapCharacteristics;

        IAnnotatedBeatmapLevelCollection _lastSelectedCollection;
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
            _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().FirstOrDefault();
            _contentModelSO = Resources.FindObjectsOfTypeAll<AdditionalContentModel>().FirstOrDefault();

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
            }

            showBackButton = true;

            ProvideInitialViewControllers(_roomNavigationController, _playerManagementViewController, _quickSettingsViewController);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (topViewController == _roomNavigationController)
                LeaveRoom();
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

            if (!usePassword || !string.IsNullOrEmpty(password))
            {
                if (!Client.Instance.connected || Client.Instance.ip != ip || Client.Instance.port != port)
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
            if (Client.Instance != null && Client.Instance.isHost && !force)
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
                Plugin.log.Warn("Unable to disconnect from ServerHub properly!");
            }

            Client.Instance.MessageReceived -= PacketReceived;
            Client.Instance.ClearMessageQueue();

            if (songToDownload != null)
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
            levelDifficultyBeatmap = null;
            levelResults = null;
            lastHighscoreForLevel = 0;
            lastHighscoreValid = false;
            Client.Instance.inRoom = false;
            PopAllViewControllers();
            SetLeftScreenViewController(_playerManagementViewController);
            PluginUI.instance.SetLobbyDiscordActivity();
            didFinishEvent?.Invoke();
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
            if (msg == null)
            {
                DisconnectCommandReceived(null);
                return;
            }
            msg.Position = 0;

            CommandType commandType = (CommandType)msg.ReadByte();

            if (!joined)
            {
                if (commandType == CommandType.JoinRoom)
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

                                levelDifficultyBeatmap = null;
                                levelResults = null;
                                lastHighscoreForLevel = 0;
                                lastHighscoreValid = false;

                                if (Config.Instance.EnableVoiceChat)
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

                                    if (roomInfo.startLevelInfo == null)
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
                                SongInfo random = new SongInfo(_beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).ToArray().Random());

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
                                roomInfo.selectedSong = songInfo;
                                IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(songInfo.levelId));

                                if (level == null)
                                {
                                    Plugin.log.Error("Unable to start level! Level is null! LevelID: " + songInfo.levelId);
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

                                    if (roomInfo.roomState == RoomState.InGame)
                                        UpdateInGameLeaderboard(currentTime, totalTime);
                                    else if(roomInfo.roomState == RoomState.Results)
                                        UpdateResultsLeaderboard(currentTime, totalTime);

                                    _playerManagementViewController.UpdatePlayerList(roomInfo.roomState);
                                }
                            }
                            break;
                        case CommandType.PlayerReady:
                            {

                                int playersReady = msg.ReadInt32();
                                int playersTotal = msg.ReadInt32();

                                if (_difficultySelectionViewController != null)
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
                }
                catch (Exception e)
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
                        if (roomInfo.songSelectionType == SongSelectionType.Manual)
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
                        if (_roomNavigationController.viewControllers.IndexOf(_resultsViewController) == -1)
                            PopAllViewControllers();

                        ShowInGameLeaderboard(roomInfo.selectedSong);
                    }
                    break;
                case RoomState.Results:
                    {
                        if (_roomNavigationController.viewControllers.IndexOf(_resultsViewController) == -1)
                            PopAllViewControllers();

                        ShowResultsLeaderboard(roomInfo.selectedSong);
                    }
                    break;
            }
            _playerManagementViewController.UpdateViewController(Client.Instance.isHost, (int)state <= 1);

            UpdateDiscordActivity(roomInfo);
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

                    UpdateDiscordActivity(roomInfo);
                }
            }
            catch (Exception e)
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

            MenuTransitionsHelper menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().FirstOrDefault();

            if (_playerManagementViewController != null)
            {
                _playerManagementViewController.SetGameplayModifiers(modifiers);
            }

            if (menuSceneSetupData != null)
            {
                Client.Instance.playerInfo.updateInfo.playerState = Config.Instance.SpectatorMode ? PlayerState.Spectating : PlayerState.Game;

                PlayerData playerData = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().playerData;

                PlayerSpecificSettings playerSettings = playerData.playerSpecificSettings;
                OverrideEnvironmentSettings environmentOverrideSettings = playerData.overrideEnvironmentSettings;

                ColorScheme colorSchemesSettings = playerData.colorSchemesSettings.overrideDefaultColors ? playerData.colorSchemesSettings.GetColorSchemeForId(playerData.colorSchemesSettings.selectedColorSchemeId) : null;

                roomInfo.roomState = RoomState.InGame;

                IDifficultyBeatmap difficultyBeatmap = level.GetDifficultyBeatmap(characteristic, difficulty, false);

                Plugin.log.Debug($"Starting song: name={level.songName}, levelId={level.levelID}, difficulty={difficulty}");

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

                var scoreSaber = IPA.Loader.PluginManager.GetPluginFromId("ScoreSaber");

                if (scoreSaber != null)
                {
                    if (scoreSaber.Metadata.Version.CompareTo(new SemVer.Version(2, 2, 8)) < 0)
                    {
                        ScoreSaberInteraction.FixScoreSaber(difficultyBeatmap);
                        Plugin.log.Info($"Applying fix for outdated ScoreSaber version!");
                    }

                    ScoreSaberInteraction.InitAndSignIn();
                }

                menuSceneSetupData.StartStandardLevel(difficultyBeatmap, environmentOverrideSettings, colorSchemesSettings, modifiers, playerSettings, startTime > 1f ? practiceSettings : null, "Lobby", false, () => { }, InGameOnlineController.Instance.SongFinished);

                UpdateDiscordActivity(roomInfo);
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
            HideInGameLeaderboard();
            HideResultsLeaderboard();
        }

        public void ShowSongsList(string lastLevelId = "")
        {
            if (_songSelectionViewController == null)
            {
                _songSelectionViewController = BeatSaberUI.CreateViewController<SongSelectionViewController>();
                _songSelectionViewController.SongSelected += SongSelected;
                _songSelectionViewController.SortPressed += (sortMode) => { SetSongs(_lastSelectedCollection, sortMode, _lastSearchRequest); };
                _songSelectionViewController.SearchPressed += (value) => { SetSongs(_lastSelectedCollection, _lastSortMode, value); };
            }


            if (_levelPacksViewController == null)
            {
                _levelPacksViewController = BeatSaberUI.CreateViewController<LevelPacksUIViewController>();
                _levelPacksViewController.packSelected += (IAnnotatedBeatmapLevelCollection pack) => { _lastSelectedCollection = pack; _lastSortMode = SortMode.Default; _lastSearchRequest = ""; SetSongs(_lastSelectedCollection, _lastSortMode, _lastSearchRequest); };
            }

            if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _songSelectionViewController, null, true);
                SetSongs(_lastSelectedCollection, _lastSortMode, _lastSearchRequest);

                if (!string.IsNullOrEmpty(lastLevelId))
                {
                    _songSelectionViewController.ScrollToLevel(lastLevelId);
                }
            }


            if (Client.Instance.isHost)
            {
                _levelPacksViewController.gameObject.SetActive(true);
                SetBottomScreenViewController(_levelPacksViewController);
            }
            else
            {
                _levelPacksViewController.gameObject.SetActive(false);
                SetBottomScreenViewController(null);
            }


            _songSelectionViewController.UpdateViewController(Client.Instance.isHost);
        }

        public void HideSongsList()
        {

            if (_songSelectionViewController != null)
            {                
                if (_roomNavigationController.viewControllers.IndexOf(_songSelectionViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                }
            }

            SetBottomScreenViewController(null);
        }

        public void SetSongs(IAnnotatedBeatmapLevelCollection selectedCollection, SortMode sortMode, string searchRequest)
        {
            _lastSortMode = sortMode;
            _lastSearchRequest = searchRequest;
            _lastSelectedCollection = selectedCollection;

            List<IPreviewBeatmapLevel> levels = new List<IPreviewBeatmapLevel>();

            if (_lastSelectedCollection != null)
            {
                levels = _lastSelectedCollection.beatmapLevelCollection.beatmapLevels.ToList();

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
                    levels = levels.Where(x => $"{x.songName} {x.songSubName} {x.levelAuthorName} {x.songAuthorName}".ToLower().Contains(searchRequest)).ToList();
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
                IPreviewBeatmapLevel data = notSorted.FirstOrDefault(x =>
                {
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

            IPreviewBeatmapLevel selectedLevel = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID == song.levelId);

            if (selectedLevel != null)
            {
                _difficultySelectionViewController.playButton.interactable = false;
                _difficultySelectionViewController.SetLoadingState(true);

                LoadBeatmapLevelAsync(selectedLevel,
                    (status, success, level) =>
                    {
                        if (status == AdditionalContentModel.EntitlementStatus.NotOwned)
                        {
                            _difficultySelectionViewController.SetSelectedSong(selectedLevel);
                            Client.Instance.SendPlayerReady(false);
                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;
                            Client.Instance.playerInfo.updateInfo.playerProgress = 0f;
                            selectedLevel.GetPreviewAudioClipAsync(new CancellationToken()).ContinueWith(
                                 (res) =>
                                 {
                                     if (!res.IsFaulted)
                                     {
                                         PreviewPlayer.CrossfadeTo(res.Result, selectedLevel.previewStartTime, res.Result.length - selectedLevel.previewStartTime);
                                     }
                                 });

                        }
                        else if (success)
                        {
                            _difficultySelectionViewController.SetSelectedSong(level);

                            if (level.beatmapLevelData.audioClip != null)
                            {
                                PreviewPlayer.CrossfadeTo(level.beatmapLevelData.audioClip, selectedLevel.previewStartTime, level.beatmapLevelData.audioClip.length - selectedLevel.previewStartTime, 1f);
                                _difficultySelectionViewController.playButton.interactable = true;
                                Client.Instance.SendPlayerReady(true);
                            }
                            else
                            {
                                _difficultySelectionViewController.playButton.interactable = false;
                                Client.Instance.SendPlayerReady(false);
                            }

                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;

                        }
                        else
                        {
                            _difficultySelectionViewController.SetSelectedSong(song);
                            _difficultySelectionViewController.playButton.interactable = false;
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
                _difficultySelectionViewController.playButton.interactable = false;
                SongDownloader.Instance.RequestSongByLevelID(song.hash, (info) =>
                {
                    Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;

                    songToDownload = info;

                    SongDownloader.Instance.DownloadSong(songToDownload,
                        (success) =>
                    {
                        if (success)
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
                                                PreviewPlayer.CrossfadeTo(level.beatmapLevelData.audioClip, level.previewStartTime, level.beatmapLevelData.audioClip.length - level.previewStartTime);
                                                _difficultySelectionViewController.SetSelectedSong(level);
                                                _difficultySelectionViewController.playButton.interactable = true;
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
                        }
                        else
                        {
                            Plugin.log.Error($"Unable to download song! An error occurred");
                            _difficultySelectionViewController.SetProgressBarState(true, 0f, "An error occurred!");
                            Client.Instance.playerInfo.updateInfo.playerProgress = -100f;
                            Client.Instance.SendPlayerReady(false);
                        }
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

        private async void LoadBeatmapLevelAsync(IPreviewBeatmapLevel selectedLevel, Action<AdditionalContentModel.EntitlementStatus, bool, IBeatmapLevel> callback)
        {
            var token = new CancellationTokenSource();

            var entitlementStatus = await _contentModelSO.GetLevelEntitlementStatusAsync(selectedLevel.levelID, token.Token);

            if (entitlementStatus == AdditionalContentModel.EntitlementStatus.Owned)
            {
                BeatmapLevelsModel.GetBeatmapLevelResult getBeatmapLevelResult = await _beatmapLevelsModel.GetBeatmapLevelAsync(selectedLevel.levelID, token.Token);

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

        public void ShowResultsLeaderboard(SongInfo song)
        {
            if (_resultsViewController == null)
            {
                _resultsViewController = BeatSaberUI.CreateViewController<MultiplayerResultsViewController>();
            }
            if (_roomNavigationController.viewControllers.IndexOf(_resultsViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _resultsViewController, null, true);
            }

            IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(song.levelId));

            if (level != null)
            {
                LoadBeatmapLevelAsync(level,
                    (status, success, beatmapLevel) =>
                    {
                        PreviewPlayer.CrossfadeTo(beatmapLevel.beatmapLevelData.audioClip, beatmapLevel.previewStartTime, beatmapLevel.beatmapLevelData.audioClip.length - beatmapLevel.previewStartTime, 1f);
                    });
            }

            _resultsViewController.UpdateLeaderboard();
            _resultsViewController.SetSong(song);
        }

        public void HideResultsLeaderboard()
        {
            if (_resultsViewController != null)
            {
                if (_roomNavigationController.viewControllers.IndexOf(_resultsViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);

                    levelDifficultyBeatmap = null;
                    levelResults = null;
                    lastHighscoreForLevel = 0;
                    lastHighscoreValid = false;
                }
            }
            PreviewPlayer.CrossfadeToDefault();

        }

        public void UpdateResultsLeaderboard(float currentTime, float totalTime)
        {
            if (_resultsViewController != null)
            {
                _resultsViewController.UpdateLeaderboard();
                _resultsViewController.SetTimer((int)(totalTime - currentTime));
            }
        }

        public void ShowInGameLeaderboard(SongInfo song)
        {
            if (_playingNowViewController == null)
            {
                _playingNowViewController = BeatSaberUI.CreateViewController<PlayingNowViewController>();
                _playingNowViewController.playNowPressed += PlayNow_Pressed;
            }
            if (_roomNavigationController.viewControllers.IndexOf(_playingNowViewController) < 0)
            {
                PushViewControllerToNavigationController(_roomNavigationController, _playingNowViewController, null, true);
            }

            _playingNowViewController.perPlayerDifficulty = roomInfo.perPlayerDifficulty;

            IPreviewBeatmapLevel selectedLevel = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(song.levelId));

            if (selectedLevel != null)
            {
                _playingNowViewController.playNowButton.interactable = false;

                LoadBeatmapLevelAsync(selectedLevel,
                    (status, success, level) =>
                    {
                        if (status == AdditionalContentModel.EntitlementStatus.NotOwned)
                        {
                            _playingNowViewController.SetSong(selectedLevel);
                            Client.Instance.SendPlayerReady(false);
                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;
                            Client.Instance.playerInfo.updateInfo.playerProgress = 0f;
                            selectedLevel.GetPreviewAudioClipAsync(new CancellationToken()).ContinueWith(
                                 (res) =>
                                 {
                                     if (!res.IsFaulted)
                                     {
                                         PreviewPlayer.CrossfadeTo(res.Result, selectedLevel.previewStartTime, res.Result.length - selectedLevel.previewStartTime);
                                     }
                                 });

                        }
                        else if (success)
                        {
                            BeatmapCharacteristicSO characteristic = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>().FirstOrDefault(x => x.serializedName == roomInfo.startLevelInfo.characteristicName);
                            PlayerDataModelSO playerData = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();

                            _playingNowViewController.SetSong(level, characteristic, (roomInfo.perPlayerDifficulty ? playerData.playerData.lastSelectedBeatmapDifficulty : roomInfo.startLevelInfo.difficulty));

                            if (level.beatmapLevelData.audioClip != null)
                            {
                                PreviewPlayer.CrossfadeTo(level.beatmapLevelData.audioClip, selectedLevel.previewStartTime, level.beatmapLevelData.audioClip.length - selectedLevel.previewStartTime, 1f);
                                _playingNowViewController.playNowButton.interactable = true;
                                Client.Instance.SendPlayerReady(true);
                            }
                            else
                            {
                                _playingNowViewController.playNowButton.interactable = false;
                                Client.Instance.SendPlayerReady(false);
                            }

                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;

                        }
                        else
                        {
                            _playingNowViewController.SetSong(song);
                            _playingNowViewController.playNowButton.interactable = false;
                            Client.Instance.SendPlayerReady(false);
                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;
                        }
                    });
            }
            else
            {
                _playingNowViewController.SetSong(song);
                _playingNowViewController.playNowButton.interactable = true;
            }

            _playingNowViewController.UpdateLeaderboard();
        }

        public void HideInGameLeaderboard()
        {
            if (_playingNowViewController != null)
            {
                if (_roomNavigationController.viewControllers.IndexOf(_playingNowViewController) >= 0)
                {
                    PopViewControllerFromNavigationController(_roomNavigationController, null, true);
                }
            }
            PreviewPlayer.CrossfadeToDefault();
        }

        public void UpdateInGameLeaderboard(float currentTime, float totalTime)
        {
            if (_playingNowViewController != null)
            {
                _playingNowViewController.UpdateLeaderboard();
                _playingNowViewController.SetTimer(currentTime, totalTime);
            }
        }

        private void PlayNow_Pressed()
        {
            SongInfo info = roomInfo.selectedSong;
            IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID == info.levelId);
            if (level == null)
            {
                _playingNowViewController.playNowButton.interactable = false;
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
                            _playingNowViewController.SetProgressBarState(true, -1f);
                        }
                    },
                    (progress) =>
                    {
                        _playingNowViewController.SetProgressBarState(progress > 0f, progress);
                    });
                });
            }
            else
            {
                StartLevel(_playingNowViewController.selectedLevel, _playingNowViewController.selectedBeatmapCharacteristic, _playingNowViewController.selectedDifficulty, roomInfo.startLevelInfo.modifiers.ToGameplayModifiers(), currentTime);
            }
        }

        private void PlayNow_SongsLoaded(SongCore.Loader arg1, Dictionary<string, CustomPreviewBeatmapLevel> arg2)
        {
            SongCore.Loader.SongsLoadedEvent -= PlayNow_SongsLoaded;
            roomInfo.selectedSong.UpdateLevelId();

            IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID == roomInfo.selectedSong.levelId);

            if (_playingNowViewController.isActivated && level != null)
            {
                _playingNowViewController.SetProgressBarState(true, 1f);

                LoadBeatmapLevelAsync(level,
                    (status, success, beatmapLevel) =>
                    {
                        if (success)
                        {
                            _playingNowViewController.playNowButton.interactable = true;
                            _playingNowViewController.SetProgressBarState(false, 0f);
                            BeatmapCharacteristicSO characteristic = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>().FirstOrDefault(x => x.serializedName == roomInfo.startLevelInfo.characteristicName);
                            PlayerDataModelSO playerData = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();

                            _playingNowViewController.SetSong(beatmapLevel, characteristic, (roomInfo.perPlayerDifficulty ? playerData.playerData.lastSelectedBeatmapDifficulty : roomInfo.startLevelInfo.difficulty));
                        }
                    });
            }
        }

#region Discord rich presence stuff
        public void UpdateDiscordActivity(RoomInfo roomInfo)
        {
            ActivityParty partyInfo = new ActivityParty()
            {
                Id = $"{ip}:{port}?{roomId}",
                Size =
                {
                    CurrentSize = roomInfo.players,
                    MaxSize = roomInfo.maxPlayers == 0 ? 256 : roomInfo.maxPlayers
                }
            };

            ActivityTimestamps timestamps = new ActivityTimestamps()
            {
                Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                End = (roomInfo.roomState == RoomState.InGame) ? (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Mathf.RoundToInt(roomInfo.selectedSong.songDuration)) : 0
            };

            ActivitySecrets secrets = new ActivitySecrets()
            {
                Join = usePassword ? $"{ip}:{port}?{roomId}#{password}" : $"{ip}:{port}?{roomId}#"
            };

            ActivityAssets assets = new ActivityAssets()
            {
                LargeImage = "default",
                LargeText = GetActivityDetails(true),
                SmallImage = roomInfo.roomState == RoomState.InGame ? GetCharacteristicIconID(Client.Instance.playerInfo.updateInfo.playerLevelOptions.characteristicName) : "multiplayer",
                SmallText = roomInfo.roomState == RoomState.InGame ? GetFancyCharacteristicName(Client.Instance.playerInfo.updateInfo.playerLevelOptions.characteristicName) : "Multiplayer"
            };

            Plugin.discordActivity = new Discord.Activity
            {
                State = RoomInfo.StateToActivityState(roomInfo.roomState),
                Details = GetActivityDetails(false),
                Party = partyInfo,
                Timestamps = timestamps,
                Secrets = secrets,
                Assets = assets,
                Instance = true,
            };

            Plugin.discord?.UpdateActivity(Plugin.discordActivity);
        }

        private string GetActivityDetails(bool includeAuthorName)
        {
            if (roomInfo.roomState != RoomState.SelectingSong && roomInfo.songSelected)
            {
                string songSubName = string.Empty;
                if(!string.IsNullOrEmpty(roomInfo.selectedSong.songSubName))
                    songSubName = $" ({roomInfo.selectedSong.songSubName})";

                string songAuthorName = string.Empty;

                if (!string.IsNullOrEmpty(roomInfo.selectedSong.authorName))
                    songAuthorName = $"{roomInfo.selectedSong.authorName} - ";

                return $"{(includeAuthorName ? songAuthorName : "")}{roomInfo.selectedSong.songName}{songSubName} | {Client.Instance.playerInfo.updateInfo.playerLevelOptions.difficulty.ToString().Replace("Plus", "+")}";
            }
            else
                return "In room";
        }

        private string GetFancyCharacteristicName(string charName)
        {
            switch (charName)
            {
                case "360Degree": return "360 Degree";
                case "90Degree": return "90 Degree";
                case "Standard": return "Standard";
                case "OneSaber": return "One Saber";
                case "NoArrows": return "No Arrows";
            }
            return "Unknown";
        }

        private string GetCharacteristicIconID(string charName)
        {
            switch (charName)
            {
                case "360Degree": return "360degree";
                case "90Degree": return "90degree";
                case "Standard": return "multiplayer";
                case "OneSaber": return "one_saber";
                case "NoArrows": return "no_arrows";
            }
            return "empty";
        }
#endregion

    }
}
