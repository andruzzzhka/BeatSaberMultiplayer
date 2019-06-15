using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RadioScreen;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using CustomUI.BeatSaber;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VRUI;

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

        BeatmapLevelsModelSO _beatmapLevelsModel;

        BeatmapCharacteristicSO[] _beatmapCharacteristics;
        BeatmapCharacteristicSO _standardCharacteristics;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
            _standardCharacteristics = _beatmapCharacteristics.First(x => x.serializedName == "Standard");
            _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModelSO>().FirstOrDefault();

            if (firstActivation)
            {
                _radioNavController = BeatSaberUI.CreateViewController<RoomNavigationController>();
                _radioNavController.didFinishEvent += () => { LeaveChannel(); };

                _inGameViewController = BeatSaberUI.CreateViewController<InGameScreenViewController>();
                _inGameViewController.playPressedEvent += PlayNow_Pressed;

                _nextSongScreenViewController = BeatSaberUI.CreateViewController<NextSongScreenViewController>();
                _nextSongScreenViewController.skipPressedEvent += SkipSong_Pressed;

                _resultsScreenViewController = BeatSaberUI.CreateViewController<ResultsScreenViewController>();
            }

            
            ProvideInitialViewControllers(_radioNavController, null, null);
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
                Plugin.log.Info("Unable to disconnect from ServerHub properly!");
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
            Client.Instance.RequestChannelInfo(channelId);
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

            CommandType commandType = (CommandType)msg.ReadByte();
            if (!joined)
            {
                if (commandType == CommandType.JoinChannel)
                {
                    switch (msg.ReadByte())
                    {
                        case 0:
                            {

                                Client.Instance.playerInfo.playerState = PlayerState.Room;
                                Client.Instance.RequestChannelInfo(channelId);
                                Client.Instance.SendPlayerInfo();
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
                                IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));

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

                                if (Client.Instance.playerInfo.playerState == PlayerState.DownloadingSongs)
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
                                IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(songInfo.levelId));

                                if(level == null)
                                {
                                    Plugin.log.Error("Unable to start level! Level is null! LevelID="+songInfo.levelId);
                                    return;
                                }

                                LoadBeatmapLevelAsync(level,
                                    (success, beatmapLevel) =>
                                    {
                                        StartLevel(beatmapLevel, characteristic, levelInfo.difficulty, levelInfo.modifiers);
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
                                            {
                                                UpdateResultsScreen();

                                                int playersCount = msg.ReadInt32();

                                                List<PlayerInfo> playerInfos = new List<PlayerInfo>();

                                                try
                                                {
                                                    for (int j = 0; j < playersCount; j++)
                                                    {

                                                        playerInfos.Add(new PlayerInfo(msg));
                                                    }
                                                }
                                                catch (Exception e)
                                                {
#if DEBUG
                                                    Plugin.log.Critical($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                                                    return;
                                                }

                                                playerInfos = playerInfos.Where(x => x.playerScore > 0 && (x.playerState == PlayerState.Game || x.playerState == PlayerState.Room)).ToList();
                                            }
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

                        IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));

                        if (level == null)
                        {
                            Client.Instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                            SongDownloader.Instance.RequestSongByLevelID(channelInfo.currentSong.hash,
                            (song) =>
                            {
                                SongDownloader.Instance.DownloadSong(song,
                                (success) =>
                                {
                                    if (success)
                                    {
                                        Action<SongCore.Loader, Dictionary<string, CustomPreviewBeatmapLevel>> onLoaded = null;
                                        onLoaded = (sender, songs) =>
                                        {
                                            SongCore.Loader.SongsLoadedEvent -= onLoaded;
                                            Client.Instance.playerInfo.playerState = PlayerState.Room;
                                            level = songs.FirstOrDefault(x => x.Value.levelID.StartsWith(channelInfo.currentSong.levelId)).Value;
                                            if (level != null)
                                                LoadBeatmapLevelAsync(level,
                                                    (loaded, beatmapLevel) =>
                                                    {
                                                        PreviewPlayer.CrossfadeTo(beatmapLevel.beatmapLevelData.audioClip, beatmapLevel.previewStartTime, Math.Max(totalTime - currentTime, beatmapLevel.previewDuration));
                                                    });
                                        };

                                        SongCore.Loader.SongsLoadedEvent += onLoaded;
                                    }
                                },
                                (progress) =>
                                {
                                    _nextSongScreenViewController.SetProgressBarState((progress < 100f), progress);
                                });
                            });
                        }

                        Client.Instance.playerInfo.playerScore = 0;
                        Client.Instance.playerInfo.playerEnergy = 0f;
                        Client.Instance.playerInfo.playerCutBlocks = 0;
                        Client.Instance.playerInfo.playerComboBlocks = 0;
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
            BeatmapLevelsModelSO.GetBeatmapLevelResult getBeatmapLevelResult = await _beatmapLevelsModel.GetBeatmapLevelAsync(selectedLevel.levelID, new CancellationTokenSource().Token);

            callback?.Invoke(!getBeatmapLevelResult.isError, getBeatmapLevelResult.beatmapLevel);
        }

        public void StartLevel(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers, float startTime = 0f)
        {
            Client.Instance.playerInfo.playerComboBlocks = 0;
            Client.Instance.playerInfo.playerCutBlocks = 0;
            Client.Instance.playerInfo.playerEnergy = 0f;
            Client.Instance.playerInfo.playerScore = 0;

            MenuTransitionsHelperSO menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuTransitionsHelperSO>().FirstOrDefault();

            if (menuSceneSetupData != null)
            {
                PlayerSpecificSettings playerSettings = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().currentLocalPlayer.playerSpecificSettings;

                channelInfo.state = ChannelState.InGame;
                Client.Instance.playerInfo.playerState = PlayerState.Game;

                IDifficultyBeatmap difficultyBeatmap = level.GetDifficultyBeatmap(characteristic, difficulty, false);

#if DEBUG
                Plugin.log.Info($"Starting song: name={level.songName}, levelId={level.levelID}, difficulty={difficulty}");
#endif

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

                menuSceneSetupData.StartStandardLevel(difficultyBeatmap, modifiers, playerSettings, (startTime > 1f ? practiceSettings : null), "Lobby", false, () => {}, (StandardLevelScenesTransitionSetupDataSO sender, LevelCompletionResults levelCompletionResults) => { InGameOnlineController.Instance.SongFinished(sender, levelCompletionResults, difficultyBeatmap, modifiers, (practiceSettings != null)); });
                return;
            }
            else
            {
                Plugin.log.Error("SceneSetupData is null!");
            }
        }

        private void PlayNow_Pressed()
        {
            SongInfo info = channelInfo.currentSong;
            IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(info.levelId));
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
                        StartLevel(beatmapLevel, _beatmapCharacteristics.First(x => x.serializedName == channelInfo.currentLevelOptions.characteristicName), channelInfo.currentLevelOptions.difficulty, channelInfo.currentLevelOptions.modifiers, currentTime);
                    });

            }
        }

        private void PlayNow_SongsLoaded(SongCore.Loader arg1, Dictionary<string, CustomPreviewBeatmapLevel> arg2)
        {
            SongCore.Loader.SongsLoadedEvent -= PlayNow_SongsLoaded;
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
                IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
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
                IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
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

                IPreviewBeatmapLevel level = SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
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
