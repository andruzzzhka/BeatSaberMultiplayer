﻿using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.RadioScreen;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using CustomUI.BeatSaber;
using Lidgren.Network;
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

        BeatmapCharacteristicSO[] _beatmapCharacteristics;
        BeatmapCharacteristicSO _standardCharacteristics;
        LevelCollectionSO _levelCollection;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
            _standardCharacteristics = _beatmapCharacteristics.First(x => x.characteristicName == "Standard");
            _levelCollection = SongLoader.CustomLevelCollectionSO;

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
            
            if (!Client.Instance.Connected || (Client.Instance.Connected && (Client.Instance.ip != ip || Client.Instance.port != port)))
            {
                Client.Instance.Disconnect();
                Client.Instance.Connect(ip, port);
                Client.Instance.InRadioMode = true;
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
                if (Client.Instance.Connected)
                {
                    Client.Instance.Disconnect();
                }
            }
            catch
            {
                Misc.Logger.Info("Unable to disconnect from ServerHub properly!");
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
            Client.Instance.MessageReceived += MessageReceived;
            Client.Instance.RequestChannelInfo(channelId);
        }

        private void MessageReceived(NetIncomingMessage msg)
        {
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
                                LevelSO level = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));

                                if (level != null)
                                {
                                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level, 
                                    (loadedLevel) =>
                                    {
                                        Client.Instance.SendSongDuration(new SongInfo() { songName = $"{level.songName} {level.songSubName}", levelId = level.levelID.Substring(0, Math.Min(32, level.levelID.Length)), songDuration = level.audioClip.length });
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

                                byte difficulty = msg.ReadByte();
                                SongInfo songInfo = new SongInfo(msg);
                                LevelSO level = _levelCollection.GetLevelsWithBeatmapCharacteristic(_standardCharacteristics).First(x => x.levelID.StartsWith(songInfo.levelId));

                                SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level,
                                (levelLoaded) =>
                                {

                                    try
                                    {
                                        BS_Utils.Gameplay.Gamemode.NextLevelIsIsolated("Beat Saber Multiplayer");
                                    }
                                    catch
                                    {

                                    }

                                    StartLevel(levelLoaded, (BeatmapDifficulty)difficulty);
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
                                if(msg == null)
                                {
                                    PopAllViewControllers();
                                    InGameOnlineController.Instance.DestroyPlayerControllers();
                                    PreviewPlayer.CrossfadeToDefault();
                                    joined = false;

                                    _radioNavController.DisplayError("Lost connection to the ServerHub!");
                                }
                                else if (msg.LengthBytes > 3)
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
                    Misc.Logger.Exception("Unable to parse packet!");
                    if (msg != null)
                    {
                        Misc.Logger.Exception($"Packet={commandType}, DataLength={msg.LengthBytes}");
                    }
                    Misc.Logger.Exception(e.ToString());
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

                        LevelSO level = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));

                        if (level == null)
                        {
                            Client.Instance.playerInfo.playerState = PlayerState.DownloadingSongs;
                            SongDownloader.Instance.RequestSongByLevelID(channelInfo.currentSong.levelId,
                            (song) =>
                            {
                                SongDownloader.Instance.DownloadSong(song, "RadioSongs",
                                () =>
                                {
                                    SongLoader.Instance.RefreshSongs(false);

                                    Action<SongLoader, List<CustomLevel>> onLoaded = null;
                                    onLoaded = (sender, songs) =>
                                    {
                                        SongLoader.SongsLoadedEvent -= onLoaded;
                                        Client.Instance.playerInfo.playerState = PlayerState.Room;
                                        level = songs.FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
                                        if (level != null)
                                            SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level,
                                             (levelLoaded) =>
                                             {
                                                 PreviewPlayer.CrossfadeTo(levelLoaded.audioClip, levelLoaded.previewStartTime, Math.Max(totalTime - currentTime, levelLoaded.previewDuration));
                                             });
                                    };

                                    SongLoader.SongsLoadedEvent += onLoaded;
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

        public void StartLevel(LevelSO level, BeatmapDifficulty difficulty, float startTime = 0f)
        {
            Client.Instance.playerInfo.playerComboBlocks = 0;
            Client.Instance.playerInfo.playerCutBlocks = 0;
            Client.Instance.playerInfo.playerEnergy = 0f;
            Client.Instance.playerInfo.playerScore = 0;

            MenuSceneSetupDataSO menuSceneSetupData = Resources.FindObjectsOfTypeAll<MenuSceneSetupDataSO>().FirstOrDefault();

            if (menuSceneSetupData != null)
            {
                GameplayModifiers gameplayModifiers = new GameplayModifiers();

                gameplayModifiers.noFail = true;

                PlayerSpecificSettings playerSettings = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().FirstOrDefault().currentLocalPlayer.playerSpecificSettings;

                channelInfo.state = ChannelState.InGame;
                Client.Instance.playerInfo.playerState = PlayerState.Game;

                IDifficultyBeatmap difficultyBeatmap = level.GetDifficultyBeatmap(difficulty);

                if(difficultyBeatmap == null)
                {
                    difficultyBeatmap = level.GetDifficultyBeatmap(GetClosestDifficulty(level, difficulty));
                }

                Misc.Logger.Info($"Starting song: name={level.songName}, levelId={level.levelID}, difficulty={difficulty}");

                PracticeSettings practiceSettings = new PracticeSettings(PracticeSettings.defaultPracticeSettings);
                practiceSettings.startSongTime = startTime + 1.5f;

                Client.Instance.MessageReceived -= MessageReceived;

                try
                {
                    BS_Utils.Gameplay.Gamemode.NextLevelIsIsolated("Beat Saber Multiplayer");
                }
                catch
                {

                }

                menuSceneSetupData.StartStandardLevel(difficultyBeatmap, gameplayModifiers, playerSettings, (startTime > 1f ? practiceSettings : null), null, (StandardLevelSceneSetupDataSO sender, LevelCompletionResults levelCompletionResults) => { InGameOnlineController.Instance.SongFinished(sender, levelCompletionResults, difficultyBeatmap, gameplayModifiers, (practiceSettings != null)); });
                return;
            }
            else
            {
                Misc.Logger.Error("SceneSetupData is null!");
            }
        }

        private void PlayNow_Pressed()
        {
            SongInfo info = channelInfo.currentSong;
            LevelSO level = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(info.levelId));
            if (level == null)
            {
                SongDownloader.Instance.RequestSongByLevelID(info.levelId, 
                (song) =>
                {
                    SongDownloader.Instance.DownloadSong(song, "RadioSongs",
                    () =>
                    {
                        SongLoader.Instance.RefreshSongs(false);
                        SongLoader.SongsLoadedEvent += PlayNow_SongsLoaded;
                    },
                    (progress) =>
                    {
                        _inGameViewController.SetProgressBarState((progress < 100f), progress);
                    });
                });
            }
            else
            {
                SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level, 
                (levelLoaded) =>
                {
                    StartLevel(levelLoaded, channelInfo.preferredDifficulty, currentTime);
                });
            }
        }

        public BeatmapDifficulty GetClosestDifficulty(LevelSO level, BeatmapDifficulty difficulty)
        {
            int num = -1;
            foreach (IDifficultyBeatmap difficultyBeatmap in level.difficultyBeatmaps)
            {
                if (difficulty < difficultyBeatmap.difficulty)
                {
                    break;
                }
                num++;
            }
            if (num == -1)
            {
                num = 0;
            }
            return level.difficultyBeatmaps[num].difficulty;
        }

        private void PlayNow_SongsLoaded(SongLoader arg1, List<CustomLevel> arg2)
        {
            SongLoader.SongsLoadedEvent -= PlayNow_SongsLoaded;
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
                LevelSO level = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
                if (level != null)
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level,
                    (levelLoaded) =>
                    {
                        PreviewPlayer.CrossfadeTo(levelLoaded.audioClip, levelLoaded.previewStartTime, Math.Max(totalTime - currentTime, levelLoaded.previewDuration));
                    });
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
                LevelSO level = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
                if (level != null)
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level,
                     (levelLoaded) =>
                     {
                         PreviewPlayer.CrossfadeTo(levelLoaded.audioClip, levelLoaded.previewStartTime, Math.Max(totalTime - currentTime, levelLoaded.previewDuration));
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
                    _resultsScreenViewController.SetSongInfo(channelInfo.currentSong, channelInfo.preferredDifficulty);
                else
                    _resultsScreenViewController.SetSongInfo(lastDifficulty, lastResults);

                LevelSO level = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(channelInfo.currentSong.levelId));
                if (level != null)
                    SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)level,
                    (levelLoaded) =>
                    {
                        PreviewPlayer.CrossfadeTo(levelLoaded.audioClip, levelLoaded.previewStartTime, Math.Max(totalTime - currentTime, levelLoaded.previewDuration));
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
