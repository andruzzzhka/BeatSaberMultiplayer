using BeatSaberMarkupLanguage;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers.RadioScreen;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using HMUI;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class RadioFlowCoordinator : FlowCoordinator
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

        public IDifficultyBeatmap lastDifficulty;
        public LevelCompletionResults lastResults;
        
        string ip;
        int port;
        int channelId;
        bool joined;

        ChannelInfo channelInfo;
        float currentTime;
        float totalTime;

        bool nextSongSkipped;

        RoomNavigationController _radioNavController;

        InGameScreenViewController _inGameViewController;
        NextSongScreenViewController _nextSongScreenViewController;
        ResultsScreenViewController _resultsScreenViewController;

        BeatmapLevelsModel _beatmapLevelsModel;

        BeatmapCharacteristicSO[] _beatmapCharacteristics;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
            _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().FirstOrDefault();

            if (firstActivation)
            {
                _radioNavController = BeatSaberUI.CreateViewController<RoomNavigationController>();

                _inGameViewController = BeatSaberUI.CreateViewController<InGameScreenViewController>();
                _inGameViewController.playPressedEvent += PlayNow_Pressed;

                _nextSongScreenViewController = BeatSaberUI.CreateViewController<NextSongScreenViewController>();
                _nextSongScreenViewController.skipPressedEvent += SkipSong_Pressed;

                _resultsScreenViewController = BeatSaberUI.CreateViewController<ResultsScreenViewController>();
            }

            showBackButton = true;
            
            ProvideInitialViewControllers(_radioNavController, null, null);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if(topViewController == _radioNavController)
                LeaveChannel();
        }

        public void JoinChannel(string ip, int port, int channelId)
        {
            this.ip = ip;
            this.port = port;
            this.channelId = channelId;
            
            if (!Client.Instance.connected || (Client.Instance.ip != ip || Client.Instance.port != port))
            {
                Client.Instance.Disconnect();
                Client.Instance.Connect(ip, port);
                Client.Instance.inRadioMode = true;
                Client.Instance.ConnectedToServerHub -= ConnectedToServerHub;
                Client.Instance.ConnectedToServerHub += ConnectedToServerHub;
            }
            else
            {
                ConnectedToServerHub();
            }
        }

        public void LeaveChannel()
        {
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
            
            PreviewPlayer.CrossfadeToDefault();
            PopAllViewControllers();
            didFinishEvent?.Invoke();
        }

        private void ConnectedToServerHub()
        {
            Client.Instance.ConnectedToServerHub -= ConnectedToServerHub;
            Client.Instance.MessageReceived -= MessageReceived;
            Client.Instance.MessageReceived += MessageReceived;
            if (!joined)
            {
                Client.Instance.JoinRadioChannel(channelId);
            }
        }

        public void ReturnToChannel()
        {
            joined = true;
            Client.Instance.MessageReceived -= MessageReceived;
            Client.Instance.MessageReceived += MessageReceived;
            if (Client.Instance.connected)
            {
                Client.Instance.RequestChannelInfo(channelId);
            }
            else
            {
                InGameOnlineController.Instance.needToSendUpdates = false;

                PopAllViewControllers();
                InGameOnlineController.Instance.DestroyPlayerControllers();
                PreviewPlayer.CrossfadeToDefault();
                joined = false;

                _radioNavController.DisplayError("Lost connection to the ServerHub!");
            }
        }

        private void MessageReceived(NetIncomingMessage msg)
        {
            if(msg == null)
            {
                InGameOnlineController.Instance.needToSendUpdates = false;

                PopAllViewControllers();
                InGameOnlineController.Instance.DestroyPlayerControllers();
                PreviewPlayer.CrossfadeToDefault();
                joined = false;

                _radioNavController.DisplayError("Lost connection to the ServerHub!");
                return;
            }
            msg.Position = 0;

            CommandType commandType = (CommandType)msg.ReadByte();
            if (!joined)
            {
                if (commandType == CommandType.JoinChannel)
                {
                    switch (msg.ReadByte())
                    {
                        case 0:
                            {

                                Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;
                                Client.Instance.RequestChannelInfo(channelId);
                                Client.Instance.SendPlayerInfo(true);
                                joined = true;
                                InGameOnlineController.Instance.needToSendUpdates = true;
                            }
                            break;
                        case 1:
                            {
                                _radioNavController.DisplayError("Unable to join channel!\nChannel not found");
                            }
                            break;
                        default:
                            {
                                _radioNavController.DisplayError("Unable to join channel!\nUnknown error");
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
                        case CommandType.GetChannelInfo:
                            {
                                channelInfo = new ChannelInfo(msg);
                                channelInfo.ip = ip;
                                channelInfo.port = port;

                                UpdateUI(channelInfo.state);
                            }
                            break;
                        case CommandType.SetSelectedSong:
                            {

                                if (msg.LengthBytes > 16)
                                {
                                    SongInfo song = new SongInfo(msg);
                                    channelInfo.currentSong = song;
                                    channelInfo.state = ChannelState.NextSong;

                                    UpdateUI(channelInfo.state);
                                }
                            }
                            break;
                        case CommandType.GetSongDuration:
                            {
                                SongInfo requestedSong = new SongInfo(msg);
                                IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));

                                if (level != null)
                                {
                                    LoadBeatmapLevelAsync(level,
                                        (success, beatmapLevel) =>
                                        {
                                            Client.Instance.SendSongDuration(new SongInfo(beatmapLevel));
                                        });
                                }
                            }
                            break;
                        case CommandType.StartLevel:
                            {

                                if (Client.Instance.playerInfo.updateInfo.playerState == PlayerState.DownloadingSongs)
                                    return;

                                if (nextSongSkipped)
                                {
                                    nextSongSkipped = false;
                                    channelInfo.state = ChannelState.InGame;
                                    UpdateUI(channelInfo.state);
                                    return;
                                }

                                LevelOptionsInfo levelInfo = new LevelOptionsInfo(msg);

                                BeatmapCharacteristicSO characteristic = _beatmapCharacteristics.First(x => x.serializedName == levelInfo.characteristicName);

                                SongInfo songInfo = new SongInfo(msg);
                                IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(songInfo.levelId));

                                if(level == null)
                                {
                                    Plugin.log.Error("Unable to start level! Level is null! LevelID="+songInfo.levelId);
                                    return;
                                }

                                LoadBeatmapLevelAsync(level,
                                    (success, beatmapLevel) =>
                                    {
                                        StartLevel(beatmapLevel, characteristic, levelInfo.difficulty, levelInfo.modifiers.ToGameplayModifiers());
                                    });
                            }
                            break;
                        case CommandType.LeaveRoom:
                            {
                                LeaveChannel();
                            }
                            break;
                        case CommandType.UpdatePlayerInfo:
                            {
                                if (channelInfo != null)
                                {
                                    currentTime = msg.ReadFloat();
                                    totalTime = msg.ReadFloat();
                                    
                                    switch (channelInfo.state)
                                    {
                                        case ChannelState.InGame:
                                            UpdateInGameScreen();
                                            break;
                                        case ChannelState.NextSong:
                                            UpdateNextSongScreen();
                                            break;
                                        case ChannelState.Results:
                                            UpdateResultsScreen();
                                            break;
                                    }
                                }
                            }
                            break;
                        case CommandType.Disconnect:
                            {
                                InGameOnlineController.Instance.needToSendUpdates = false;

                                if (msg.LengthBytes > 3)
                                {
                                    string reason = msg.ReadString();

                                    PopAllViewControllers();
                                    InGameOnlineController.Instance.DestroyPlayerControllers();
                                    PreviewPlayer.CrossfadeToDefault();
                                    joined = false;

                                    _radioNavController.DisplayError(reason);
                                }
                                else
                                {
                                    _radioNavController.DisplayError("ServerHub refused connection!");
                                }

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
        
        public void UpdateUI(ChannelState state)
        {
            switch (state)
            {
                case ChannelState.NextSong:
                    {
                        if (!_radioNavController.viewControllers.Contains(_nextSongScreenViewController))
                            PopAllViewControllers();
                        
                        ShowNextSongScreen();

                        IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));

                        if (level == null)
                        {
                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.DownloadingSongs;
                            SongDownloader.Instance.RequestSongByLevelID(channelInfo.currentSong.hash,
                            (song) =>
                            {
                                SongDownloader.Instance.DownloadSong(song,
                                (success) =>
                                {
                                    if (success)
                                    {
                                        void onLoaded(SongCore.Loader sender, Dictionary<string, CustomPreviewBeatmapLevel> songs)
                                        {
                                            SongCore.Loader.SongsLoadedEvent -= onLoaded;
                                            Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Room;
                                            channelInfo.currentSong.UpdateLevelId();
                                            level = songs.FirstOrDefault(x => x.Value.levelID == channelInfo.currentSong.levelId).Value;
                                            if (level != null)
                                                LoadBeatmapLevelAsync(level,
                                                    (loaded, beatmapLevel) =>
                                                    {
                                                        PreviewPlayer.CrossfadeTo(beatmapLevel.beatmapLevelData.audioClip, beatmapLevel.previewStartTime, Math.Max(totalTime - currentTime, beatmapLevel.previewDuration));
                                                    });
                                        }

                                        SongCore.Loader.SongsLoadedEvent += onLoaded;
                                        SongCore.Loader.Instance.RefreshSongs(false);

                                    }
                                },
                                (progress) =>
                                {
                                    _nextSongScreenViewController.SetProgressBarState((progress < 100f), progress);
                                });
                            });
                        }

                        Client.Instance.playerInfo.updateInfo.playerScore = 0;
                        Client.Instance.playerInfo.updateInfo.playerEnergy = 0f;
                        Client.Instance.playerInfo.updateInfo.playerCutBlocks = 0;
                        Client.Instance.playerInfo.updateInfo.playerComboBlocks = 0;
                    }
                    break;
                case ChannelState.InGame:
                    {
                        if (!_radioNavController.viewControllers.Contains(_inGameViewController))
                            PopAllViewControllers();

                        ShowInGameScreen();
                        
                    }
                    break;
                case ChannelState.Results:
                    {
                        if (!_radioNavController.viewControllers.Contains(_resultsScreenViewController))
                            PopAllViewControllers();

                        ShowResultsScreen();
                    }
                    break;
            }
        }

        private async void LoadBeatmapLevelAsync(IPreviewBeatmapLevel selectedLevel, Action<bool, IBeatmapLevel> callback)
        {
            BeatmapLevelsModel.GetBeatmapLevelResult getBeatmapLevelResult = await _beatmapLevelsModel.GetBeatmapLevelAsync(selectedLevel.levelID, new CancellationTokenSource().Token);

            callback?.Invoke(!getBeatmapLevelResult.isError, getBeatmapLevelResult.beatmapLevel);
        }

        public void StartLevel(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers, float startTime = 0f)
        {
            Client.Instance.playerInfo.updateInfo.playerComboBlocks = 0;
            Client.Instance.playerInfo.updateInfo.playerCutBlocks = 0;
            Client.Instance.playerInfo.updateInfo.playerEnergy = 0f;
            Client.Instance.playerInfo.updateInfo.playerScore = 0;
            
            MenuTransitionsHelper menuTransitionHelper = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().FirstOrDefault();

            if (menuTransitionHelper != null)
            {
                PlayerData playerData = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().playerData;

                PlayerSpecificSettings playerSettings = playerData.playerSpecificSettings;
                OverrideEnvironmentSettings environmentOverrideSettings = playerData.overrideEnvironmentSettings;
                ColorSchemesSettings colorSchemesSettings = playerData.colorSchemesSettings;

                channelInfo.state = ChannelState.InGame;
                Client.Instance.playerInfo.updateInfo.playerState = PlayerState.Game;

                IDifficultyBeatmap difficultyBeatmap = level.beatmapLevelData.GetDifficultyBeatmap(characteristic, difficulty);

                Plugin.log.Debug($"Starting song: name={level.songName}, levelId={level.levelID}, difficulty={difficulty}");

                PracticeSettings practiceSettings = new PracticeSettings(PracticeSettings.defaultPracticeSettings);
                
                practiceSettings.startSongTime = startTime + 1.5f;
                practiceSettings.songSpeedMul = modifiers.songSpeedMul;
                practiceSettings.startInAdvanceAndClearNotes = true;

                Client.Instance.MessageReceived -= MessageReceived;

                try
                {
                    BS_Utils.Gameplay.Gamemode.NextLevelIsIsolated("Beat Saber Multiplayer");
                }
                catch
                {

                }

                menuTransitionHelper.StartStandardLevel(difficultyBeatmap, environmentOverrideSettings, colorSchemesSettings.GetColorSchemeForId(colorSchemesSettings.selectedColorSchemeId), modifiers, playerSettings, (startTime > 1f ? practiceSettings : null), "Lobby", false, () => {}, (StandardLevelScenesTransitionSetupDataSO sender, LevelCompletionResults levelCompletionResults) => { InGameOnlineController.Instance.SongFinished(levelCompletionResults, difficultyBeatmap, modifiers, (startTime > 1f)); });
            }
            else
            {
                Plugin.log.Error("SceneSetupData is null!");
            }
        }

        private void PlayNow_Pressed()
        {
            SongInfo info = channelInfo.currentSong;
            IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(info.levelId));
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
                    },
                    (progress) =>
                    {
                        _inGameViewController.SetProgressBarState((progress < 100f), progress);
                    });
                });
            }
            else
            {
                LoadBeatmapLevelAsync(level, 
                    (success, beatmapLevel) =>
                    {
                        StartLevel(beatmapLevel, _beatmapCharacteristics.First(x => x.serializedName == channelInfo.currentLevelOptions.characteristicName), channelInfo.currentLevelOptions.difficulty, channelInfo.currentLevelOptions.modifiers.ToGameplayModifiers(), currentTime);
                    });

            }
        }

        private void PlayNow_SongsLoaded(SongCore.Loader arg1, Dictionary<string, CustomPreviewBeatmapLevel> arg2)
        {
            SongCore.Loader.SongsLoadedEvent -= PlayNow_SongsLoaded;
            channelInfo.currentSong.UpdateLevelId();
            PlayNow_Pressed();
        }
        
        private void SkipSong_Pressed()
        {
            nextSongSkipped = !nextSongSkipped;
            _nextSongScreenViewController.SetSkipState(nextSongSkipped);
        }

        public void PopAllViewControllers()
        {
            HideInGameScreen();
            HideNextSongScreen();
            HideResultsScreen();
        }

#region In-Game Screen
        public void ShowInGameScreen()
        {
            if (!_radioNavController.viewControllers.Contains(_inGameViewController))
            {
                PushViewControllerToNavigationController(_radioNavController, _inGameViewController, null, true);
                _inGameViewController.SetSongInfo(channelInfo.currentSong);
                IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
                if (level != null)
                    LoadBeatmapLevelAsync(level, 
                        (success, beatmapLevel) =>
                        {
                            PreviewPlayer.CrossfadeTo(beatmapLevel.beatmapLevelData.audioClip, beatmapLevel.previewStartTime, Math.Max(totalTime - currentTime, beatmapLevel.previewDuration));
                        });
                else
                {
                    Plugin.log.Error("Unable to play preview for song! Level is null! levelID="+channelInfo.currentSong.levelId);
                }
            }
        }

        public void UpdateInGameScreen()
        {
            _inGameViewController.SetTimer(currentTime, totalTime);
        }

        public void HideInGameScreen()
        {
            if (_radioNavController.viewControllers.Contains(_inGameViewController))
            {
                PopViewControllerFromNavigationController(_radioNavController, null, true);
            }
            PreviewPlayer.CrossfadeToDefault();
        }
#endregion

#region Next Song Screen
        public void ShowNextSongScreen()
        {
            if (!_radioNavController.viewControllers.Contains(_nextSongScreenViewController))
            {
                PushViewControllerToNavigationController(_radioNavController, _nextSongScreenViewController, null, true);
                _nextSongScreenViewController.SetSongInfo(channelInfo.currentSong);
                IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
                if (level != null)
                    LoadBeatmapLevelAsync(level,
                        (success, beatmapLevel) =>
                        {
                            PreviewPlayer.CrossfadeTo(beatmapLevel.beatmapLevelData.audioClip, beatmapLevel.previewStartTime, Math.Max(totalTime - currentTime, beatmapLevel.previewDuration));
                        });
            }
        }

        public void UpdateNextSongScreen()
        {
            _nextSongScreenViewController.SetTimer(currentTime, totalTime);
        }

        public void HideNextSongScreen()
        {
            if (_radioNavController.viewControllers.Contains(_nextSongScreenViewController))
            {
                PopViewControllerFromNavigationController(_radioNavController, null, true);
            }
            PreviewPlayer.CrossfadeToDefault();
        }
#endregion

#region Results Screen
        public void ShowResultsScreen()
        {
            if (!_radioNavController.viewControllers.Contains(_resultsScreenViewController))
            {
                PushViewControllerToNavigationController(_radioNavController, _resultsScreenViewController, null, true);

                if(lastDifficulty == null || lastResults == null)
                    _resultsScreenViewController.SetSongInfo(channelInfo.currentSong, channelInfo.currentLevelOptions.difficulty);
                else
                    _resultsScreenViewController.SetSongInfo(lastDifficulty, lastResults);

                IPreviewBeatmapLevel level = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
                if (level != null)
                    LoadBeatmapLevelAsync(level,
                        (success, beatmapLevel) =>
                        {
                            PreviewPlayer.CrossfadeTo(beatmapLevel.beatmapLevelData.audioClip, beatmapLevel.previewStartTime, Math.Max(totalTime - currentTime, beatmapLevel.previewDuration));
                        });
            }
        }

        public void UpdateResultsScreen()
        {
            _resultsScreenViewController.SetTimer(currentTime, totalTime);
            if(lastDifficulty != null && lastResults != null)
                _resultsScreenViewController.SetSongInfo(lastDifficulty, lastResults);
        }

        public void HideResultsScreen()
        {
            if (_radioNavController.viewControllers.Contains(_resultsScreenViewController))
            {
                PopViewControllerFromNavigationController(_radioNavController, null, true);
            }
            PreviewPlayer.CrossfadeToDefault();
        }
#endregion
    }
}
