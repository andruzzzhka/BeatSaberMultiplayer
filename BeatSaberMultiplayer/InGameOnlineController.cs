using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using BeatSaberMultiplayer.VOIP;
using BS_Utils.Gameplay;
using CustomAvatar;
using CustomUI.BeatSaber;
using Lidgren.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using VRUI;

namespace BeatSaberMultiplayer
{
    public enum MessagePosition : byte { Top, Bottom };

    public class InGameOnlineController : MonoBehaviour
    {
        public static Quaternion oculusTouchRotOffset = Quaternion.Euler(-40f, 0f, 0f);
        public static Vector3 oculusTouchPosOffset = new Vector3(0f, 0f, 0.055f);
        public static Quaternion openVrRotOffset = Quaternion.Euler(-4.3f, 0f, 0f);
        public static Vector3 openVrPosOffset = new Vector3(0f, -0.008f, 0f);

        public static InGameOnlineController Instance;

        public bool needToSendUpdates;

        public AudioTimeSyncController audioTimeSync;
        private StandardLevelGameplayManager _gameManager;
        private ScoreController _scoreController;
        private GameEnergyCounter _energyController;
        private PauseMenuManager _pauseMenuManager;
        private VRPlatformHelper _vrPlatformHelper;

        private List<OnlinePlayerController> _players = new List<OnlinePlayerController>();
        private List<PlayerInfoDisplay> _scoreDisplays = new List<PlayerInfoDisplay>();
        private GameObject _scoreScreen;

        private TextMeshPro _messageDisplayText;
        private float _messageDisplayTime;

        private string _currentScene;
        private bool loaded;
        private int sendRateCounter;
        private int fixedSendRate = 0;
        
        SpeexCodex speexDec;
        private WritableAudioPlayer voiceChatPlayer;
        private VoipListener voiceChatListener;

        public bool isRecording;
        
        public static void OnLoad(Scene to)
        {
            if (Instance != null)
                return;
            new GameObject("InGameOnlineController").AddComponent<InGameOnlineController>();
        }

        public void Awake()
        {
            if (Instance != this)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                Client.ClientCreated += ClientCreated;
                _currentScene = SceneManager.GetActiveScene().name;
                
                _messageDisplayText = CustomExtensions.CreateWorldText(transform, "");
                transform.position = new Vector3(0f, 3.75f, 3.75f);
                transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
                _messageDisplayText.overflowMode = TextOverflowModes.Overflow;
                _messageDisplayText.enableWordWrapping = false;
                _messageDisplayText.alignment = TextAlignmentOptions.Center;
                DontDestroyOnLoad(_messageDisplayText.gameObject);

                voiceChatListener = new GameObject("Voice Chat Listener").AddComponent<VoipListener>();

                voiceChatListener.OnAudioGenerated += (fragment) =>
                {
                    if (isRecording)
                    {
                        fragment.playerId = Client.Instance.playerInfo.playerId;
                        Client.Instance.SendVoIPData(fragment);
                    }
                };

                DontDestroyOnLoad(voiceChatListener.gameObject);

                voiceChatPlayer = new GameObject("Voice Chat Player").AddComponent<WritableAudioPlayer>();
                DontDestroyOnLoad(voiceChatPlayer.gameObject);

                CustomAvatar.Plugin.Instance.PlayerAvatarManager.AvatarChanged += PlayerAvatarManager_AvatarChanged;
            }
        }

        public void VoiceChatStartRecording()
        {
            if (voiceChatListener != null)
                voiceChatListener.StartRecording();
        }

        public void VoiceChatStopRecording()
        {
            if (voiceChatListener != null)
                voiceChatListener.StopRecording();
        }

        public void VoiceChatVolumeChanged(float volume)
        {
            if (voiceChatPlayer != null)
            {
                voiceChatPlayer.SetVolume(volume);
            }
        }

        public bool VoiceChatIsTalking(ulong playerId)
        {
            if(Config.Instance.EnableVoiceChat && voiceChatPlayer != null)
            {
                return (playerId == Client.Instance.playerInfo.playerId) ? isRecording : voiceChatPlayer.IsTalking(playerId);
            }
            else
            {
                return false;
            }
        }

        public void ActiveSceneChanged(Scene from, Scene to)
        {
            try
            {
                Misc.Logger.Info($"(OnlineController) Travelling from {from.name} to {to.name}");
                if (to.name == "GameCore" || to.name == "Menu")
                {
                    _currentScene = to.name;
                    if (_currentScene == "GameCore")
                    {
                        DestroyAvatars();
                        DestroyScoreScreens();
                        if (Client.Instance != null && Client.Instance.Connected)
                        {
                            StartCoroutine(WaitForControllers());
                            needToSendUpdates = true;
                        }
                    }
                    else if (_currentScene == "Menu")
                    {
                        loaded = false;
                        DestroyAvatars();
                        if (Client.Instance != null && Client.Instance.Connected)
                        {
                            if (Client.Instance.InRadioMode)
                            {
                                PluginUI.instance.radioFlowCoordinator.ReturnToChannel();
                            }
                            else
                            {
                                PluginUI.instance.roomFlowCoordinator.ReturnToRoom();
                            }
                            needToSendUpdates = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Misc.Logger.Exception($"(OnlineController) Exception on {_currentScene} scene activation! Exception: {e}");
            }
        }

        private void ClientCreated()
        {
            Client.Instance.MessageReceived -= PacketReceived;
            Client.Instance.MessageReceived += PacketReceived;
        }

        private void PacketReceived(NetIncomingMessage msg)
        {
            switch ((CommandType)msg.ReadByte())
            {
                case CommandType.UpdatePlayerInfo:
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

                        playerInfos = playerInfos.Where(x => (x.playerState == PlayerState.Game && _currentScene == "GameCore") || (x.playerState == PlayerState.Room && _currentScene == "Menu") || (x.playerState == PlayerState.DownloadingSongs && _currentScene == "Menu")).OrderByDescending(x => x.playerId).ToList();

                        int localPlayerIndex = playerInfos.FindIndexInList(Client.Instance.playerInfo);

                        if (((ShowAvatarsInGame() && !Config.Instance.SpectatorMode && loaded) || ShowAvatarsInRoom()) && !Client.Instance.InRadioMode)
                        {
                            try
                            {
                                int index = 0;
                                OnlinePlayerController player = null;
                                foreach (PlayerInfo info in playerInfos)
                                {
                                    player = _players.FirstOrDefault(x => x != null && x.PlayerInfo.Equals(info));

                                    if (player != null)
                                    {
                                        player.PlayerInfo = info;
                                        player.avatarOffset = (index - localPlayerIndex) * (_currentScene == "GameCore" ? 5f : 0f);
                                    }
                                    else
                                    {
                                        player = new GameObject("OnlinePlayerController").AddComponent<OnlinePlayerController>();

                                        player.PlayerInfo = info;
                                        player.avatarOffset = (index - localPlayerIndex) * (_currentScene == "GameCore" ? 5f : 0f);

                                        _players.Add(player);
                                    }

                                    index++;
                                }

                                if (_players.Count > playerInfos.Count)
                                {
                                    foreach (OnlinePlayerController controller in _players.Where(x => !playerInfos.Any(y => y.Equals(x.PlayerInfo))))
                                    {
                                        Destroy(controller.gameObject);
                                    }
                                    _players.RemoveAll(x => x == null || x.destroyed || x.gameObject == null);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"PlayerControllers exception: {e}");
                            }
                        }

                        localPlayerIndex = playerInfos.FindIndexInList(Client.Instance.playerInfo);

                        if (_currentScene == "GameCore" && loaded)
                        {
                            playerInfos = playerInfos.OrderByDescending(x => x.playerScore).ToList();

                            if (_scoreDisplays.Count < 5)
                            {
                                _scoreScreen = new GameObject("ScoreScreen");
                                _scoreScreen.transform.position = new Vector3(0f, 4f, 12f);
                                _scoreScreen.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

                                _scoreDisplays.Clear();

                                for (int i = 0; i < 5; i++)
                                {
                                    PlayerInfoDisplay buffer = new GameObject("ScoreDisplay " + i).AddComponent<PlayerInfoDisplay>();
                                    buffer.transform.SetParent(_scoreScreen.transform);
                                    buffer.transform.localPosition = new Vector3(0f, 2.5f - i, 0);

                                    _scoreDisplays.Add(buffer);
                                }
                            }

                            if (playerInfos.Count <= 5)
                            {
                                for (int i = 0; i < playerInfos.Count; i++)
                                {
                                    _scoreDisplays[i].UpdatePlayerInfo(playerInfos[i], playerInfos.FindIndexInList(playerInfos[i]));
                                }
                                for (int i = playerInfos.Count; i < _scoreDisplays.Count; i++)
                                {
                                    _scoreDisplays[i].UpdatePlayerInfo(null, 0);
                                }
                            }
                            else
                            {
                                if (localPlayerIndex < 3)
                                {
                                    for (int i = 0; i < 5; i++)
                                    {
                                        _scoreDisplays[i].UpdatePlayerInfo(playerInfos[i], playerInfos.FindIndexInList(playerInfos[i]));
                                    }
                                }
                                else if (localPlayerIndex > playerInfos.Count - 3)
                                {
                                    for (int i = playerInfos.Count - 5; i < playerInfos.Count; i++)
                                    {
                                        _scoreDisplays[i - (playerInfos.Count - 5)].UpdatePlayerInfo(playerInfos[i], playerInfos.FindIndexInList(playerInfos[i]));
                                    }
                                }
                                else
                                {
                                    for (int i = localPlayerIndex - 2; i < localPlayerIndex + 3; i++)
                                    {
                                        _scoreDisplays[i - (localPlayerIndex - 2)].UpdatePlayerInfo(playerInfos[i], playerInfos.FindIndexInList(playerInfos[i]));
                                    }
                                }

                            }
                        }
                    }
                    break;
                case CommandType.UpdateVoIPData:
                    {
                        if (!Config.Instance.EnableVoiceChat)
                            return;

                        int playersCount = msg.ReadInt32();

                        for (int j = 0; j < playersCount; j++)
                        {
                            try
                            {
                                VoipFragment data = new VoipFragment(msg);

#if DEBUG
                                if (data.data != null && data.data.Length > 0)
#else
                                if (data.data != null && data.data.Length > 0 && data.playerId != Client.Instance.playerInfo.playerId)
#endif
                                {
                                    if (speexDec == null || speexDec.mode != data.mode)
                                    {
                                        speexDec = SpeexCodex.Create(data.mode);
                                    }
                                    voiceChatPlayer.PlayAudio(speexDec.Decode(data.data), data.playerId);
                                }
                            }
                            catch (Exception e)
                            {
#if DEBUG
                                Misc.Logger.Exception($"Unable to parse VoIP fragment! Excpetion: {e}");
#endif
                            }
                        }
                    }
                    break;
                case CommandType.SetGameState:
                    {
                        if (_currentScene == "GameCore" && loaded)
                        {
                            PropertyInfo property = typeof(StandardLevelGameplayManager).GetProperty("gameState");
                            property.DeclaringType.GetProperty("gameState");
                            property.GetSetMethod(true).Invoke(_gameManager, new object[] { (StandardLevelGameplayManager.GameState)msg.ReadByte() });
                        }
                    }
                    break;
                case CommandType.DisplayMessage:
                    {
                        _messageDisplayTime = msg.ReadFloat();
                        _messageDisplayText.fontSize = msg.ReadFloat();

                        _messageDisplayText.text = msg.ReadString();

                        if (msg.LengthBits - msg.Position >= 8)
                        {
                            MessagePosition position = (MessagePosition)msg.ReadByte();

                            switch (position)
                            {
                                default:
                                case MessagePosition.Top:
                                    _messageDisplayText.transform.position = new Vector3(0f, 3.75f, 3.75f);
                                    _messageDisplayText.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
                                    break;
                                case MessagePosition.Bottom:
                                    _messageDisplayText.transform.position = new Vector3(0f, 0f, 2.25f);
                                    _messageDisplayText.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
                                    break;
                            }
                        }
                    }; break;
            }
        }
        
        public void Update()
        {
            if (_messageDisplayTime > 0f)
            {
                _messageDisplayTime -= Time.deltaTime;
                if(_messageDisplayTime <= 0f)
                {
                    _messageDisplayTime = 0f;
                    _messageDisplayText.text = "";
                }
            }

            if (Config.Instance.EnableVoiceChat)
            {
                if (!Config.Instance.PushToTalk)
                {
                    isRecording = true;
                }
                else
                {
                    switch (Config.Instance.PushToTalkButton)
                    {
                        case 0:
                            isRecording = ControllersHelper.GetLeftGrip();
                            break;
                        case 1:
                            isRecording = ControllersHelper.GetRightGrip();
                            break;
                        case 2:
                            isRecording = VRControllersInputManager.TriggerValue(XRNode.LeftHand) > 0.85f;
                            break;
                        case 3:
                            isRecording = VRControllersInputManager.TriggerValue(XRNode.RightHand) > 0.85f;
                            break;
                        case 4:
                            isRecording = ControllersHelper.GetLeftGrip() && ControllersHelper.GetRightGrip();
                            break;
                        case 5:
                            isRecording = VRControllersInputManager.TriggerValue(XRNode.RightHand) > 0.85f && VRControllersInputManager.TriggerValue(XRNode.LeftHand) > 0.85f;
                            break;
                        case 6:
                            isRecording = ControllersHelper.GetLeftGrip() || ControllersHelper.GetRightGrip();
                            break;
                        case 7:
                            isRecording = VRControllersInputManager.TriggerValue(XRNode.RightHand) > 0.85f || VRControllersInputManager.TriggerValue(XRNode.LeftHand) > 0.85f;
                            break;
                        default:
                            isRecording = Input.anyKey;
                            break;
                    }
                }
            }
            else
            {
                isRecording = false;
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.Keypad0))
                {
                    fixedSendRate = 0;
                    Misc.Logger.Info($"Variable send rate");
                }
                else if(Input.GetKeyDown(KeyCode.Keypad1))
                {
                    fixedSendRate = 1;
                    Misc.Logger.Info($"Forced full send rate");
                }
                else if (Input.GetKeyDown(KeyCode.Keypad2))
                {
                    fixedSendRate = 2;
                    Misc.Logger.Info($"Forced half send rate");
                }
                else if(Input.GetKeyDown(KeyCode.Keypad3))
                {
                    fixedSendRate = 3;
                    Misc.Logger.Info($"Forced one third send rate");
                }
            }

            if (needToSendUpdates)
            {
                if (fixedSendRate == 1 || (fixedSendRate == 0 && Client.Instance.Tickrate > 67.5f * (1f / 90 / Time.deltaTime)))
                {
                    sendRateCounter = 0;
                    UpdatePlayerInfo();
#if DEBUG && VERBOSE
                    Misc.Logger.Info($"Full send rate! FPS: {(1f / Time.deltaTime).ToString("0.0")}, TPS: {Client.Instance.Tickrate.ToString("0.0")}");
#endif
                }
                else if (fixedSendRate == 2 || (fixedSendRate == 0 && Client.Instance.Tickrate > 37.5f * (1f / 90 / Time.deltaTime)))
                {
                    sendRateCounter++;
                    if (sendRateCounter >= 1)
                    {
                        sendRateCounter = 0;
                        UpdatePlayerInfo();
#if DEBUG && VERBOSE
                        Misc.Logger.Info($"Half send rate! FPS: {(1f / Time.deltaTime).ToString("0.0")}, TPS: {Client.Instance.Tickrate.ToString("0.0")}");
#endif
                    }
                }
                else if (fixedSendRate == 3 || (fixedSendRate == 0 && Client.Instance.Tickrate <= 37.5f * (1f / 90 / Time.deltaTime)))
                {
                    sendRateCounter++;
                    if (sendRateCounter >= 2)
                    {
                        sendRateCounter = 0;
                        UpdatePlayerInfo();
#if DEBUG && VERBOSE
                        Misc.Logger.Info($"One third send rate! FPS: {(1f / Time.deltaTime).ToString("0.0")}, TPS: {Client.Instance.Tickrate.ToString("0.0")}");
#endif
                    }
                }
            }
        }

        private void PlayerAvatarManager_AvatarChanged(CustomAvatar.CustomAvatar obj)
        {
            if (!Config.Instance.SeparateAvatarForMultiplayer)
            {
                Client.Instance.playerInfo.avatarHash = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == CustomAvatar.Plugin.Instance.PlayerAvatarManager.GetCurrentAvatar()).Key;

                if (Client.Instance.playerInfo.avatarHash == null)
                {
                    Client.Instance.playerInfo.avatarHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
                }
            }
        }

        public void UpdatePlayerInfo()
        {

            if (Client.Instance.playerInfo.avatarHash == null || Client.Instance.playerInfo.avatarHash == "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")
            {
                if (Config.Instance.SeparateAvatarForMultiplayer)
                {
                    Client.Instance.playerInfo.avatarHash = Config.Instance.PublicAvatarHash;
                }
                else
                {
                    Client.Instance.playerInfo.avatarHash = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == CustomAvatar.Plugin.Instance.PlayerAvatarManager.GetCurrentAvatar()).Key;
                }
#if DEBUG
                Misc.Logger.Info("Updating avatar hash... New hash: "+(Client.Instance.playerInfo.avatarHash == null ? "NULL" : Client.Instance.playerInfo.avatarHash));
#endif
            }

            if (Client.Instance.playerInfo.avatarHash == null)
            {
                Client.Instance.playerInfo.avatarHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            }

            Client.Instance.playerInfo.headPos = GetXRNodeWorldPosRot(XRNode.Head).Position;
            Client.Instance.playerInfo.headRot = GetXRNodeWorldPosRot(XRNode.Head).Rotation;

            Client.Instance.playerInfo.leftHandPos = GetXRNodeWorldPosRot(XRNode.LeftHand).Position;
            Client.Instance.playerInfo.leftHandRot = GetXRNodeWorldPosRot(XRNode.LeftHand).Rotation;

            Client.Instance.playerInfo.rightHandPos = GetXRNodeWorldPosRot(XRNode.RightHand).Position;
            Client.Instance.playerInfo.rightHandRot = GetXRNodeWorldPosRot(XRNode.RightHand).Rotation;

            if(_vrPlatformHelper == null)
            {
                _vrPlatformHelper = PersistentSingleton<VRPlatformHelper>.instance;
            }

            if (_vrPlatformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                Client.Instance.playerInfo.leftHandRot *= oculusTouchRotOffset;
                Client.Instance.playerInfo.leftHandPos += Client.Instance.playerInfo.leftHandRot * oculusTouchPosOffset;
                Client.Instance.playerInfo.rightHandRot *= oculusTouchRotOffset;
                Client.Instance.playerInfo.rightHandPos += Client.Instance.playerInfo.rightHandRot * oculusTouchPosOffset;
            }
            else if (_vrPlatformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
            {
                Client.Instance.playerInfo.leftHandRot *= openVrRotOffset;
                Client.Instance.playerInfo.leftHandPos += Client.Instance.playerInfo.leftHandRot * openVrPosOffset;
                Client.Instance.playerInfo.rightHandRot *= openVrRotOffset;
                Client.Instance.playerInfo.rightHandPos += Client.Instance.playerInfo.rightHandRot * openVrPosOffset;
            }

            if (_currentScene == "GameCore" && loaded)
            {
                Client.Instance.playerInfo.playerProgress = audioTimeSync.songTime;
            }
            else if(Client.Instance.playerInfo.playerState != PlayerState.DownloadingSongs)
            {
                Client.Instance.playerInfo.playerProgress = 0;
            }

            if (Config.Instance.SpectatorMode)
            {
                Client.Instance.playerInfo.playerScore = 0;
                Client.Instance.playerInfo.playerEnergy = 0f;
                Client.Instance.playerInfo.playerCutBlocks = 0;
                Client.Instance.playerInfo.playerComboBlocks = 0;
            }

            Client.Instance.SendPlayerInfo();
        }

        private bool ShowAvatarsInGame()
        {
            return Config.Instance.ShowAvatarsInGame && _currentScene == "GameCore";
        }

        private bool ShowAvatarsInRoom()
        {
            return Config.Instance.ShowAvatarsInRoom && _currentScene == "Menu";
        }

        public static PosRot GetXRNodeWorldPosRot(XRNode node)
        {
            var pos = InputTracking.GetLocalPosition(node);
            var rot = InputTracking.GetLocalRotation(node);

            var roomCenter = BeatSaberUtil.GetRoomCenter();
            var roomRotation = BeatSaberUtil.GetRoomRotation();

            pos = roomRotation * pos;
            pos += roomCenter;
            rot = roomRotation * rot;
            return new PosRot(pos, rot);
        }

        public void DestroyAvatars()
        {
            try
            {
#if DEBUG
                Misc.Logger.Info("Destroying player controllers...");
#endif
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i] != null)
                        Destroy(_players[i].gameObject);
                }
                _players.Clear();
            }catch(Exception e)
            {
                Misc.Logger.Exception($"Unable to destroy player controllers! Exception: {e}");
            }
        }

        public void DestroyScoreScreens()
        {
            try
            {
                for (int i = 0; i < _scoreDisplays.Count; i++)
                {
                    if (_scoreDisplays[i] != null)
                        Destroy(_scoreDisplays[i].gameObject);
                }
                _scoreDisplays.Clear();
                Destroy(_scoreScreen);
            }
            catch (Exception e)
            {
                Misc.Logger.Exception($"Unable to destroy score screens! Exception: {e}");
            }
        }

        public void SongFinished(StandardLevelSceneSetupDataSO sender, LevelCompletionResults levelCompletionResults, IDifficultyBeatmap difficultyBeatmap, GameplayModifiers gameplayModifiers, bool practice)
        {
            if(Client.Instance.InRadioMode)
            {
                PluginUI.instance.radioFlowCoordinator.lastDifficulty = difficultyBeatmap;
                PluginUI.instance.radioFlowCoordinator.lastResults = levelCompletionResults;
            }

            if (Config.Instance.SpectatorMode || Client.disableScoreSubmission || ScoreSubmission.Disabled || ScoreSubmission.ProlongedDisabled)
                return;
            
            PlayerDataModelSO _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
            
            _playerDataModel.currentLocalPlayer.playerAllOverallStatsData.soloFreePlayOverallStatsData.UpdateWithLevelCompletionResults(levelCompletionResults);
            _playerDataModel.Save();
            if (levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Failed && levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared)
            {
                return;
            }
            
            PlayerDataModelSO.LocalPlayer currentLocalPlayer = _playerDataModel.currentLocalPlayer;
            bool cleared = levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared;
            string levelID = difficultyBeatmap.level.levelID;
            BeatmapDifficulty difficulty = difficultyBeatmap.difficulty;
            PlayerLevelStatsData playerLevelStatsData = currentLocalPlayer.GetPlayerLevelStatsData(levelID, difficulty);
            bool newHighScore = playerLevelStatsData.highScore < levelCompletionResults.score;
            playerLevelStatsData.IncreaseNumberOfGameplays();
            if (cleared)
            {
                playerLevelStatsData.UpdateScoreData(levelCompletionResults.score, levelCompletionResults.maxCombo, levelCompletionResults.fullCombo, levelCompletionResults.rank);
                Resources.FindObjectsOfTypeAll<PlatformLeaderboardsModel>().First().AddScore(difficultyBeatmap, levelCompletionResults.unmodifiedScore, gameplayModifiers);
            }
        }

        IEnumerator WaitForControllers()
        {
#if DEBUG
            Misc.Logger.Info("Waiting for game controllers...");
#endif
            yield return new WaitUntil(delegate () { return FindObjectOfType<ScoreController>() != null; });
#if DEBUG
            Misc.Logger.Info("Game controllers found!");
#endif
            _gameManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();

            if (_gameManager != null)
            {
                try
                {
                    if (ReflectionUtil.GetPrivateField<IPauseTrigger>(_gameManager, "_pauseTrigger") != null)
                    {
                        ReflectionUtil.GetPrivateField<IPauseTrigger>(_gameManager, "_pauseTrigger").pauseTriggeredEvent -= _gameManager.HandlePauseTriggered;
                        ReflectionUtil.GetPrivateField<IPauseTrigger>(_gameManager, "_pauseTrigger").pauseTriggeredEvent += ShowMenu;
                    }

                    if (ReflectionUtil.GetPrivateField<VRPlatformHelper>(_gameManager, "_vrPlatformHelper") != null)
                    {
                        ReflectionUtil.GetPrivateField<VRPlatformHelper>(_gameManager, "_vrPlatformHelper").inputFocusWasCapturedEvent -= _gameManager.HandleInputFocusWasCaptured;
                        ReflectionUtil.GetPrivateField<VRPlatformHelper>(_gameManager, "_vrPlatformHelper").inputFocusWasCapturedEvent += ShowMenu;
                    }
                }
                catch (Exception e)
                {
                    Misc.Logger.Exception(e.ToString());
                }
            }
#if DEBUG
            Misc.Logger.Info("Disabled pause button!");
#endif
            _scoreController = FindObjectOfType<ScoreController>();

            if (_scoreController != null)
            {
                _scoreController.scoreDidChangeEvent += ScoreChanged;
                _scoreController.noteWasCutEvent += NoteWasCutEvent;
                _scoreController.comboDidChangeEvent += ComboDidChangeEvent;
                _scoreController.noteWasMissedEvent += NoteWasMissedEvent;
            }
#if DEBUG
            Misc.Logger.Info("Found score controller");
#endif

            _energyController = FindObjectOfType<GameEnergyCounter>();

            if (_energyController != null)
            {
                _energyController.gameEnergyDidChangeEvent += EnergyDidChangeEvent;
            }
#if DEBUG
            Misc.Logger.Info("Found energy controller");
#endif

            audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();

            _pauseMenuManager = FindObjectsOfType<PauseMenuManager>().First();
            
            if (_pauseMenuManager != null)
            {
                _pauseMenuManager.GetPrivateField<Button>("_restartButton").interactable = false;
            }

#if DEBUG
            Misc.Logger.Info("Found pause manager");
#endif

            loaded = true;
        }
        
        private void ShowMenu()
        {
            try
            {
                _pauseMenuManager.ShowMenu();
            }
            catch(Exception e)
            {
                Misc.Logger.Error("Unable to show menu! Exception: "+e);
            }
        }

        public void PauseSong()
        {
            Resources.FindObjectsOfTypeAll<SongController>().First().PauseSong();
        }

        public void ResumeSong()
        {
            Resources.FindObjectsOfTypeAll<SongController>().First().ResumeSong();
        }

        private void EnergyDidChangeEvent(float energy)
        {
            Client.Instance.playerInfo.playerEnergy = (int)Math.Round(energy * 100);
        }

        private void ComboDidChangeEvent(int obj)
        {
            Client.Instance.playerInfo.playerComboBlocks = (uint)obj;
        }

        private void NoteWasCutEvent(NoteData arg1, NoteCutInfo arg2, int score)
        {
            if (arg1.noteType == NoteType.Bomb)
            {
                Client.Instance.playerInfo.hitsLastUpdate.Add(new HitData(arg1, true, arg2));
            }
            else
            {
                if (arg2.allIsOK)
                {
                    Client.Instance.playerInfo.hitsLastUpdate.Add(new HitData(arg1, true, arg2));
                    Client.Instance.playerInfo.playerCutBlocks++;
                    Client.Instance.playerInfo.playerTotalBlocks++;
                }
                else
                {
                    Client.Instance.playerInfo.hitsLastUpdate.Add(new HitData(arg1, true, arg2));
                    Client.Instance.playerInfo.playerTotalBlocks++;
                }
            }
        }

        private void NoteWasMissedEvent(NoteData arg1, int arg2)
        {
            Client.Instance.playerInfo.hitsLastUpdate.Add(new HitData(arg1, false));
            Client.Instance.playerInfo.playerTotalBlocks++;
        }

        private void ScoreChanged(int score)
        {
            Client.Instance.playerInfo.playerScore = (uint)score;
        }
    }

}
