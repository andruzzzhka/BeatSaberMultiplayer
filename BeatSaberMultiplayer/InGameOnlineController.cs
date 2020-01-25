using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.OverriddenClasses;
using BeatSaberMultiplayer.UI;
using BeatSaberMultiplayer.VOIP;
using BS_Utils.Gameplay;
using CustomAvatar;
using Lidgren.Network;
using SongCore.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BeatSaberMultiplayer
{
    public enum MessagePosition : byte { Top, Bottom };

    public class InGameOnlineController : MonoBehaviour
    {
        private const float _PTTReleaseDelay = 0.2f;
        private const string _gameSceneName = "GameCore";
        private const string _menuSceneName = "MenuViewControllers";

        public static Quaternion oculusTouchRotOffset = Quaternion.Euler(-40f, 0f, 0f);
        public static Vector3 oculusTouchPosOffset = new Vector3(0f, 0f, 0.055f);
        public static Quaternion openVrRotOffset = Quaternion.Euler(-4.3f, 0f, 0f);
        public static Vector3 openVrPosOffset = new Vector3(0f, -0.008f, 0f);

        public static InGameOnlineController Instance;

        public bool needToSendUpdates;

        public bool isVoiceChatActive;
        public bool isRecording;

        private float _PTTReleaseTime;
        private bool _waitingForRecordingDelay;

        public List<ulong> mutedPlayers = new List<ulong>();

        public AudioTimeSyncController audioTimeSync;
        private StandardLevelGameplayManager _gameManager;
        private ScoreController _scoreController;
        private GameEnergyCounter _energyController;
        private PauseMenuManager _pauseMenuManager;
        private VRPlatformHelper _vrPlatformHelper;
        private VRControllersInputManager _vrInputManager;

        private PlayerAvatarInput _avatarInput;

        public Dictionary<ulong, OnlinePlayerController> players = new Dictionary<ulong, OnlinePlayerController>();
        public List<PlayerScore> playerScores;
        private List<ulong> _playerIds;
        private List<PlayerInfoDisplay> _scoreDisplays = new List<PlayerInfoDisplay>();
        private GameObject _scoreScreen;

        public bool sendFullUpdate;
        private string _prevAvatarHash;

        private TextMeshPro _messageDisplayText;
        private float _messageDisplayTime;

        private string _currentScene;
        private bool _loaded;
        private int _sendRateCounter;
        private int _fixedSendRate = 0;
        private bool _spectatorInRoom;
        private float _colorCounter;

        SpeexCodex speexDec;
        private VoipListener voiceChatListener;
        
        public static void OnLoad()
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

                Client.Instance.MessageReceived -= PacketReceived;
                Client.Instance.MessageReceived += PacketReceived;
                _currentScene = SceneManager.GetActiveScene().name;
                
                _messageDisplayText = CustomExtensions.CreateWorldText(transform, "");
                transform.position = new Vector3(40f, -43.75f, 3.75f);
                transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
                _messageDisplayText.overflowMode = TextOverflowModes.Overflow;
                _messageDisplayText.enableWordWrapping = false;
                _messageDisplayText.alignment = TextAlignmentOptions.Center;
                DontDestroyOnLoad(_messageDisplayText.gameObject);
                CustomAvatar.Plugin.Instance.PlayerAvatarManager.AvatarChanged += PlayerAvatarManager_AvatarChanged;

                if (Config.Instance.EnableVoiceChat)
                {
                    voiceChatListener = new GameObject("Voice Chat Listener").AddComponent<VoipListener>();

                    voiceChatListener.OnAudioGenerated -= ProcesVoiceFragment;
                    voiceChatListener.OnAudioGenerated += ProcesVoiceFragment;

                    DontDestroyOnLoad(voiceChatListener.gameObject);
                    
                    isVoiceChatActive = true;
                }
                
            }
        }

        public void ToggleVoiceChat(bool enabled)
        {
            Config.Instance.EnableVoiceChat = enabled;
            Config.Instance.Save();
            if (enabled && !isVoiceChatActive)
            {
                voiceChatListener = new GameObject("Voice Chat Listener").AddComponent<VoipListener>();
                voiceChatListener.OnAudioGenerated -= ProcesVoiceFragment;
                voiceChatListener.OnAudioGenerated += ProcesVoiceFragment;
                DontDestroyOnLoad(voiceChatListener.gameObject);

                if (Client.Instance.inRoom)
                    VoiceChatStartRecording();
            }
            else if (!enabled && isVoiceChatActive)
            {
                Destroy(voiceChatListener.gameObject);
                voiceChatListener.OnAudioGenerated -= ProcesVoiceFragment;
                isRecording = false;
                voiceChatListener.isListening = isRecording;
            }
            
            isVoiceChatActive = enabled;
        }

        private void ProcesVoiceFragment(VoipFragment fragment)
        {
            if (voiceChatListener.isListening)
            {
                fragment.playerId = Client.Instance.playerInfo.playerId;
                Client.Instance.SendVoIPData(fragment);
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
            if (players != null)
            {
                foreach (var player in players.Values.Where(x => x != null && !x.destroyed)) {
                    player.SetVoIPVolume(volume);
                }
            }
        }

        public void VoiceChatSpatialAudioChanged(bool enabled)
        {
            if (players != null)
            {
                foreach (var player in players.Values.Where(x => x != null && !x.destroyed))
                {
                    player.SetSpatialAudioState(enabled);
                }
            }
        }

        public bool VoiceChatIsTalking(ulong playerId)
        {
            if(Config.Instance.EnableVoiceChat && players != null)
                if (playerId == Client.Instance.playerInfo.playerId)
                    return isRecording;
                else
                    if (players.TryGetValue(playerId, out OnlinePlayerController player))
                        if (player != null && !player.destroyed)
                            return player.IsTalking();
                        else
                            return false;
                    else
                        return false;
            else
                return false;
        }

        public void SetSeparatePublicAvatarState(bool enabled)
        {
            Config.Instance.SeparateAvatarForMultiplayer = enabled;

            if (Client.Instance.connected)
            {
                if (enabled)
                {
                    Client.Instance.playerInfo.avatarHash = Config.Instance.PublicAvatarHash;

                    if (string.IsNullOrEmpty(Client.Instance.playerInfo.avatarHash))
                    {
                        Client.Instance.playerInfo.avatarHash = PlayerInfo.avatarHashPlaceholder;
                    }
                }
                else
                {
                    Client.Instance.playerInfo.avatarHash = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == CustomAvatar.Plugin.Instance.PlayerAvatarManager.GetCurrentAvatar()).Key;

                    if (Client.Instance.playerInfo.avatarHash == null)
                    {
                        Client.Instance.playerInfo.avatarHash = PlayerInfo.avatarHashPlaceholder;
                    }
                }
            }
        }

        public void SetSeparatePublicAvatarHash(string hash)
        {
            Config.Instance.PublicAvatarHash = hash;
            if (Client.Instance.connected && Config.Instance.SeparateAvatarForMultiplayer)
            {
                Client.Instance.playerInfo.avatarHash = Config.Instance.PublicAvatarHash;
            }
        }

        public bool IsPlayerVisible(ulong playerId)
        {
            if(players.TryGetValue(playerId, out OnlinePlayerController player))
            {
                return  (player.playerInfo.updateInfo.playerState == PlayerState.Game && _currentScene == _gameSceneName && Config.Instance.ShowAvatarsInGame && !Config.Instance.SpectatorMode) ||
                        (player.playerInfo.updateInfo.playerState == PlayerState.Room && _currentScene == _menuSceneName && Config.Instance.ShowAvatarsInRoom) ||
                        (player.playerInfo.updateInfo.playerState == PlayerState.DownloadingSongs && _currentScene == _menuSceneName && Config.Instance.ShowAvatarsInRoom);
            }
            else
                return false;
        }

        public void MenuSceneLoaded()
        {
            _currentScene = _menuSceneName;
            _loaded = false;
            DestroyPlayerControllers();
            if (Client.Instance != null && Client.Instance.connected)
            {
                needToSendUpdates = true;
                if (Client.Instance.inRadioMode)
                {
                    //PluginUI.instance.radioFlowCoordinator.ReturnToChannel();
                }
                else
                {
                    PluginUI.instance.roomFlowCoordinator.ReturnToRoom();
                }
            }
        }

        public void GameSceneLoaded()
        {
            _currentScene = _gameSceneName;
            DestroyPlayerControllers();
            DestroyScoreScreens();
            if (Client.Instance != null && Client.Instance.connected)
            {
                StartCoroutine(WaitForControllers());
                needToSendUpdates = true;
                if (Config.Instance.SubmitScores == 0 || Config.Instance.SpectatorMode || Client.disableScoreSubmission)
                    BS_Utils.Gameplay.ScoreSubmission.DisableSubmission("Beat Saber Multiplayer");
            }
        }

        public void PacketReceived(NetIncomingMessage msg)
        {
            if(msg == null)
            {
                if (_currentScene == _gameSceneName && _loaded)
                {
                    PropertyInfo property = typeof(StandardLevelGameplayManager).GetProperty("gameState");
                    property.DeclaringType.GetProperty("gameState");
                    property.GetSetMethod(true).Invoke(_gameManager, new object[] { StandardLevelGameplayManager.GameState.Failed });
                }
                return;
            }
            msg.Position = 0;

            switch ((CommandType)msg.ReadByte())
            {
                case CommandType.UpdatePlayerInfo:
                    {
                        msg.Position += 64;

                        bool fullUpdate = (msg.ReadByte() == 1);

                        int playersCount = msg.ReadInt32();

                        if (_playerIds == null)
                            _playerIds = new List<ulong>(playersCount);
                        else if (_playerIds.Count > playersCount)
                            _playerIds.Clear();

                        if (playerScores == null)
                            playerScores = new List<PlayerScore>(playersCount);
                        else if (playerScores.Count > playersCount)
                            playerScores.Clear();

                        _spectatorInRoom = false;
                        for (int i = 0; i < playersCount; i++)
                        {
                            ulong playerId = msg.ReadUInt64();

                            if (_playerIds.Count > i)
                                _playerIds[i] = playerId;
                            else
                                _playerIds.Add(playerId);

                            if (players.TryGetValue(playerId, out OnlinePlayerController player))
                            {
                                if (player == null)
                                {
                                    player = new GameObject("OnlinePlayerController").AddComponent<OnlinePlayerController>();
                                    players[playerId] = player;
                                }

                                if (fullUpdate)
                                {
                                    PlayerInfo playerInfo = new PlayerInfo(msg);
                                    player.playerInfo = playerInfo;
                                    _spectatorInRoom |= playerInfo.updateInfo.playerState == PlayerState.Spectating;
                                }
                                else
                                {
                                    PlayerUpdate update = new PlayerUpdate(msg);
                                    player.NewUpdateReceived(update);

                                    byte hitCount = msg.ReadByte();

                                    if(player.playerInfo.hitsLastUpdate.Count > 0)
                                        player.playerInfo.hitsLastUpdate.Clear();

                                    for (int j = 0; j < hitCount; j++)
                                    {
                                        player.playerInfo.hitsLastUpdate.Add(new HitData(msg));
                                    }

                                    _spectatorInRoom |= update.playerState == PlayerState.Spectating;
                                }
                            }
                            else
                            {
                                if (fullUpdate)
                                {
                                    player = new GameObject("OnlinePlayerController").AddComponent<OnlinePlayerController>();
                                    PlayerInfo playerInfo = new PlayerInfo(msg);
                                    player.playerInfo = playerInfo;
                                    _spectatorInRoom |= playerInfo.updateInfo.playerState == PlayerState.Spectating;
                                    players.Add(playerId, player);
                                }
                                else
                                {
                                    Plugin.log.Error("Not enough info to create new player controller! Waiting for full update...");
                                    sendFullUpdate = true;
                                    new PlayerUpdate(msg);
                                    byte hitCount = msg.ReadByte();
                                    msg.ReadBytes(hitCount*5);
                                }
                            }

                            if (player != null)
                            {
                                player.SetAvatarState(!Client.Instance.inRadioMode && IsPlayerVisible(player.playerInfo.playerId));
                                player.SetBlocksState(!Client.Instance.inRadioMode &&  _currentScene == _gameSceneName && Config.Instance.ShowOtherPlayersBlocks && !Client.Instance.playerInfo.Equals(player.playerInfo) && !Config.Instance.SpectatorMode && player.playerInfo.updateInfo.playerState == PlayerState.Game);
                            }
                        }

                        _playerIds.Sort();

                        int localPlayerIndex = _playerIds.IndexOf(Client.Instance.playerInfo.playerId);
                        bool needToRemovePlayer = false;

                        for(int i = 0; i < playerScores.Count; i++)
                        {
                            playerScores[i] = default;
                        }

                        foreach (var pair in players)
                        {
                            if (!_playerIds.Contains(pair.Key))
                            {
                                needToRemovePlayer = true;
                            }
                            else
                            {
                                int playerIndex = _playerIds.IndexOf(pair.Key);
                                pair.Value.avatarOffset = (playerIndex - localPlayerIndex) * (_currentScene == _gameSceneName ? 5f : 0f);

                                while (playerScores.Count <= playerIndex)
                                    playerScores.Add(default);

                                playerScores[playerIndex] = new PlayerScore(pair.Value);
                            }
                        }

                        if (needToRemovePlayer)
                        {
                            Plugin.log.Debug("Player(s) left! Removing player controllers...");
                            List<ulong> removed = new List<ulong>();

                            foreach (var pair in players)
                                if (!_playerIds.Contains(pair.Key))
                                {
                                    Plugin.log.Debug("Removed player controller with ID " + pair.Key);
                                    removed.Add(pair.Key);
                                    if (pair.Value != null && !pair.Value.destroyed)
                                    {
                                        Destroy(pair.Value);
                                    }
                                }

                            foreach(ulong key in removed)
                                players.Remove(key);
                        }

                        if (_currentScene == _gameSceneName && _loaded)
                        {
                            playerScores.Sort();
                            localPlayerIndex = playerScores.FindIndex((x) => { return x.id == Client.Instance.playerInfo.playerId; });

                            if (_scoreDisplays.Count < 5)
                            {
                                if (_scoreScreen == null)
                                {
                                    _scoreScreen = new GameObject("ScoreScreen", typeof(RectTransform));
                                    _scoreScreen.transform.position = new Vector3(0f, 5f, 12f) + Config.Instance.ScoreScreenPosOffset;
                                    _scoreScreen.transform.rotation = Quaternion.Euler(Config.Instance.ScoreScreenRotOffset);
                                    _scoreScreen.transform.localScale = Config.Instance.ScoreScreenScale;

                                    var rotator = GameObject.FindObjectOfType<FlyingGameHUDRotation>();

                                    if(rotator != null)
                                    {
                                        _scoreScreen.transform.SetParent(rotator.transform, true);
                                    }

                                    var bg = new GameObject("Background", typeof(Canvas), typeof(CanvasRenderer)).AddComponent<Image>();
                                    bg.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                                    bg.rectTransform.SetParent(_scoreScreen.transform);
                                    bg.rectTransform.localScale = new Vector3(0.18f, 0.0525f, 1f);
                                    bg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                                    bg.rectTransform.anchoredPosition3D = new Vector3(-0.75f, 2.5f, 0.01f);
                                    bg.sprite = Sprites.whitePixel;
                                    bg.material = Sprites.NoGlowMat;
                                    bg.color = new Color(0f, 0f, 0f, 0.5f);
                                }

                                foreach (var display in _scoreDisplays)
                                    Destroy(display.gameObject);

                                _scoreDisplays.Clear();

                                for (int i = 0; i < 5; i++)
                                {
                                    PlayerInfoDisplay buffer = new GameObject("ScoreDisplay " + i).AddComponent<PlayerInfoDisplay>();
                                    buffer.transform.SetParent(_scoreScreen.transform, false);
                                    buffer.transform.localPosition = new Vector3(0f, 2.5f - i, 0);
                                    buffer.transform.localScale = Vector3.one;
                                    buffer.transform.localRotation = Quaternion.identity;

                                    _scoreDisplays.Add(buffer);
                                }
                            }

                            if (playerScores.Count <= 5)
                            {
                                for (int i = 0; i < playerScores.Count; i++)
                                {
                                    _scoreDisplays[i].UpdatePlayerInfo(playerScores[i], i);
                                }
                                for (int i = playerScores.Count; i < _scoreDisplays.Count; i++)
                                {
                                    _scoreDisplays[i].UpdatePlayerInfo(default, 0);
                                }
                            }
                            else
                            {
                                if (localPlayerIndex < 3)
                                {
                                    for (int i = 0; i < 5; i++)
                                    {
                                        _scoreDisplays[i].UpdatePlayerInfo(playerScores[i], i);
                                    }
                                }
                                else if (localPlayerIndex > playerScores.Count - 3)
                                {
                                    for (int i = playerScores.Count - 5; i < playerScores.Count; i++)
                                    {
                                        _scoreDisplays[i - (playerScores.Count - 5)].UpdatePlayerInfo(playerScores[i], i);
                                    }
                                }
                                else
                                {
                                    for (int i = localPlayerIndex - 2; i < localPlayerIndex + 3; i++)
                                    {
                                        _scoreDisplays[i - (localPlayerIndex - 2)].UpdatePlayerInfo(playerScores[i], i);
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

                        foreach (var playerPair in players)
                        {
                            playerPair.Value.VoIPUpdate();
                        }

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
                                        Plugin.log.Debug("New Speex decoder created!");
                                    }

                                    if(players.TryGetValue(data.playerId, out OnlinePlayerController player))
                                    {
                                        player?.PlayVoIPFragment(speexDec.Decode(data.data), data.index);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
#if DEBUG
                                Plugin.log.Error($"Unable to parse VoIP fragment! Excpetion: {e}");
#endif
                            }
                        }
                    }
                    break;
                case CommandType.SetGameState:
                    {
                        var targetState = (StandardLevelGameplayManager.GameState)msg.ReadByte();

                        if (_currentScene == _gameSceneName && _loaded)
                        {
                            PropertyInfo property = typeof(StandardLevelGameplayManager).GetProperty("gameState");
                            property.DeclaringType.GetProperty("gameState");
                            property.GetSetMethod(true).Invoke(_gameManager, new object[] { targetState });
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
            if (!Client.Instance.connected)
                return;

            if (_messageDisplayTime > 0f)
            {
                _messageDisplayTime -= Time.deltaTime;
                if(_messageDisplayTime <= 0f)
                {
                    _messageDisplayTime = 0f;
                    if(_messageDisplayText != null)
                        _messageDisplayText.text = "";
                }
            }

            if (_vrInputManager == null)
            {
                _vrInputManager = Resources.FindObjectsOfTypeAll<VRController>().FirstOrDefault(x => !(x is OnlineVRController)).GetPrivateField<VRControllersInputManager>("_vrControllersInputManager");
            }

            if (Config.Instance.EnableVoiceChat)
            {
                if (Config.Instance.MicEnabled)
                    if (!Config.Instance.PushToTalk)
                        isRecording = true;
                    else
                        switch (Config.Instance.PushToTalkButton)
                        {
                            case 0:
                                isRecording = ControllersHelper.GetLeftGrip();
                                break;
                            case 1:
                                isRecording = ControllersHelper.GetRightGrip();
                                break;
                            case 2:

                                isRecording = _vrInputManager.TriggerValue(XRNode.LeftHand) > 0.85f;
                                break;
                            case 3:
                                isRecording = _vrInputManager.TriggerValue(XRNode.RightHand) > 0.85f;
                                break;
                            case 4:
                                isRecording = ControllersHelper.GetLeftGrip() && ControllersHelper.GetRightGrip();
                                break;
                            case 5:
                                isRecording = _vrInputManager.TriggerValue(XRNode.RightHand) > 0.85f && _vrInputManager.TriggerValue(XRNode.LeftHand) > 0.85f;
                                break;
                            case 6:
                                isRecording = ControllersHelper.GetLeftGrip() || ControllersHelper.GetRightGrip();
                                break;
                            case 7:
                                isRecording = _vrInputManager.TriggerValue(XRNode.RightHand) > 0.85f || _vrInputManager.TriggerValue(XRNode.LeftHand) > 0.85f;
                                break;
                            default:
                                isRecording = Input.anyKey;
                                break;
                        }
                else
                    isRecording = false;

#if DEBUG
                if ((_vrInputManager.TriggerValue(XRNode.LeftHand) > 0.85f && ControllersHelper.GetRightGrip() && _vrInputManager.TriggerValue(XRNode.RightHand) > 0.85f && ControllersHelper.GetLeftGrip()) || Input.GetKey(KeyCode.P))

#else
                if (_vrInputManager.TriggerValue(XRNode.LeftHand) > 0.85f && ControllersHelper.GetRightGrip() && _vrInputManager.TriggerValue(XRNode.RightHand) > 0.85f && ControllersHelper.GetLeftGrip())
#endif
                {
                    _colorCounter += Time.deltaTime;
                    if (_colorCounter > 7.5f)
                    {
                        Client.Instance.playerInfo.updateInfo.playerFlags.rainbowName ^= true;
                        _colorCounter = 0f;
                    }
                }
                else
                {
                    _colorCounter = 0f;
                }
            }
            else
            {
                isRecording = false;
            }

            if (isVoiceChatActive && voiceChatListener != null)
            {
                if (!isRecording && voiceChatListener.isListening && !_waitingForRecordingDelay)
                {
                    _PTTReleaseTime = Time.time;
                    _waitingForRecordingDelay = true;
                }
                else if(!isRecording && voiceChatListener.isListening && Time.time - _PTTReleaseTime < _PTTReleaseDelay)
                {
                    //Do nothing
                }
                else
                {
                    voiceChatListener.isListening = isRecording;
                    _waitingForRecordingDelay = false;
                }
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.Keypad0))
                {
                    _fixedSendRate = 0;
                    Plugin.log.Info($"Variable send rate");
                }
                else if(Input.GetKeyDown(KeyCode.Keypad1))
                {
                    _fixedSendRate = 1;
                    Plugin.log.Info($"Forced full send rate");
                }
                else if (Input.GetKeyDown(KeyCode.Keypad2))
                {
                    _fixedSendRate = 2;
                    Plugin.log.Info($"Forced half send rate");
                }
                else if(Input.GetKeyDown(KeyCode.Keypad3))
                {
                    _fixedSendRate = 3;
                    Plugin.log.Info($"Forced one third send rate");
                }
            }

            if (needToSendUpdates)
            {
                if (_fixedSendRate == 1 || (_fixedSendRate == 0 && Client.Instance.tickrate > (2f / (3f * Time.deltaTime) + 5f)) || _spectatorInRoom)
                {
                    _sendRateCounter = 0;
                    UpdatePlayerInfo();
#if DEBUG && VERBOSE
                    Plugin.log.Info($"Full send rate! FPS: {(1f / Time.deltaTime).ToString("0.0")}, TPS: {Client.Instance.Tickrate.ToString("0.0")}, Trigger: TPS>{1f / Time.deltaTime / 3f * 2f + 5f}");
#endif
                }
                else if (_fixedSendRate == 2 || (_fixedSendRate == 0 && Client.Instance.tickrate > (1f / (3f * Time.deltaTime) + 5f)))
                {
                    _sendRateCounter++;
                    if (_sendRateCounter >= 1)
                    {
                        _sendRateCounter = 0;
                        UpdatePlayerInfo();
#if DEBUG && VERBOSE
                        Plugin.log.Info($"Half send rate! FPS: {(1f / Time.deltaTime).ToString("0.0")}, TPS: {Client.Instance.Tickrate.ToString("0.0")}, Trigger: TPS>{1f / Time.deltaTime / 3f + 5f}");
#endif
                    }
                }
                else if (_fixedSendRate == 3 || (_fixedSendRate == 0 && Client.Instance.tickrate <= (1f / (3f * Time.deltaTime) + 5f)))
                {
                    _sendRateCounter++;
                    if (_sendRateCounter >= 2)
                    {
                        _sendRateCounter = 0;
                        UpdatePlayerInfo();
#if DEBUG && VERBOSE
                        Plugin.log.Info($"One third send rate! FPS: {(1f / Time.deltaTime).ToString("0.0")}, TPS: {Client.Instance.Tickrate.ToString("0.0")}, Trigger: TPS<={1f / Time.deltaTime / 3f + 5f}");
#endif
                    }
                }
            }
        }

        private void PlayerAvatarManager_AvatarChanged(CustomAvatar.CustomAvatar obj)
        {
            if (!Config.Instance.SeparateAvatarForMultiplayer && Client.Instance.connected)
            {
                Client.Instance.playerInfo.avatarHash = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == CustomAvatar.Plugin.Instance.PlayerAvatarManager.GetCurrentAvatar()).Key;
                sendFullUpdate = true;

                if (string.IsNullOrEmpty(Client.Instance.playerInfo.avatarHash))
                {
                    Client.Instance.playerInfo.avatarHash = PlayerInfo.avatarHashPlaceholder;
                }
            }
        }

        public void UpdatePlayerInfo()
        {

            if (Client.Instance.playerInfo.avatarHash == null || Client.Instance.playerInfo.avatarHash.Length == 0 || Client.Instance.playerInfo.avatarHash == PlayerInfo.avatarHashPlaceholder)
            {
                if (Config.Instance.SeparateAvatarForMultiplayer)
                {
                    Client.Instance.playerInfo.avatarHash = Config.Instance.PublicAvatarHash;
                    sendFullUpdate = true;
                }
                else
                {
                    Client.Instance.playerInfo.avatarHash = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == CustomAvatar.Plugin.Instance.PlayerAvatarManager.GetCurrentAvatar()).Key;
                    sendFullUpdate = true;
                }
#if DEBUG
                if (Client.Instance.playerInfo.avatarHash != PlayerInfo.avatarHashPlaceholder)
                {
                    Plugin.log.Debug("Updating avatar hash... New hash: " + (Client.Instance.playerInfo.avatarHash));
                }
#endif
            }

            if(_avatarInput == null)
            {
                _avatarInput = CustomAvatar.Plugin.Instance.PlayerAvatarManager._playerAvatarInput;
            }

            var head = _avatarInput.HeadPosRot;
            var leftHand = _avatarInput.LeftPosRot;
            var rightHand = _avatarInput.RightPosRot;

            Client.Instance.playerInfo.updateInfo.headPos = head.Position;
            Client.Instance.playerInfo.updateInfo.headRot = head.Rotation;

            Client.Instance.playerInfo.updateInfo.leftHandPos = leftHand.Position;
            Client.Instance.playerInfo.updateInfo.leftHandRot = leftHand.Rotation;

            Client.Instance.playerInfo.updateInfo.rightHandPos = rightHand.Position;
            Client.Instance.playerInfo.updateInfo.rightHandRot = rightHand.Rotation;

            if (CustomAvatar.Plugin.IsFullBodyTracking)
            {
                Client.Instance.playerInfo.updateInfo.fullBodyTracking = true;

                var pelvis = _avatarInput.PelvisPosRot;
                var leftLeg = _avatarInput.LeftLegPosRot;
                var rightLeg = _avatarInput.RightLegPosRot;

                Client.Instance.playerInfo.updateInfo.pelvisPos = pelvis.Position;
                Client.Instance.playerInfo.updateInfo.pelvisRot = pelvis.Rotation;

                Client.Instance.playerInfo.updateInfo.leftLegPos = leftLeg.Position;
                Client.Instance.playerInfo.updateInfo.leftLegRot = leftLeg.Rotation;

                Client.Instance.playerInfo.updateInfo.rightLegPos = rightLeg.Position;
                Client.Instance.playerInfo.updateInfo.rightLegRot = rightLeg.Rotation;
            }
            else
            {
                Client.Instance.playerInfo.updateInfo.fullBodyTracking = false;
            }

            if (_vrPlatformHelper == null)
            {
                _vrPlatformHelper = PersistentSingleton<VRPlatformHelper>.instance;
            }

            if (_vrPlatformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                Client.Instance.playerInfo.updateInfo.leftHandRot *= oculusTouchRotOffset;
                Client.Instance.playerInfo.updateInfo.leftHandPos += oculusTouchPosOffset;
                Client.Instance.playerInfo.updateInfo.rightHandRot *= oculusTouchRotOffset;
                Client.Instance.playerInfo.updateInfo.rightHandPos += oculusTouchPosOffset;
            }
            else if (_vrPlatformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
            {
                Client.Instance.playerInfo.updateInfo.leftHandRot *= openVrRotOffset;
                Client.Instance.playerInfo.updateInfo.leftHandPos += openVrPosOffset;
                Client.Instance.playerInfo.updateInfo.rightHandRot *= openVrRotOffset;
                Client.Instance.playerInfo.updateInfo.rightHandPos += openVrPosOffset;
            }

            if (_currentScene == _gameSceneName && _loaded)
            {
                Client.Instance.playerInfo.updateInfo.playerProgress = audioTimeSync.songTime;
            }
            else if(Client.Instance.playerInfo.updateInfo.playerState != PlayerState.DownloadingSongs && Client.Instance.playerInfo.updateInfo.playerState != PlayerState.Game)
            {
                Client.Instance.playerInfo.updateInfo.playerProgress = 0;
            }

            if (Config.Instance.SpectatorMode)
            {
                Client.Instance.playerInfo.updateInfo.playerScore = 0;
                Client.Instance.playerInfo.updateInfo.playerEnergy = 0f;
                Client.Instance.playerInfo.updateInfo.playerCutBlocks = 0;
                Client.Instance.playerInfo.updateInfo.playerComboBlocks = 0;
            }

            if (Client.Instance.playerInfo.avatarHash != _prevAvatarHash)
            {
                sendFullUpdate = true;
                _prevAvatarHash = Client.Instance.playerInfo.avatarHash;
            }

            Client.Instance.SendPlayerInfo(sendFullUpdate);
            sendFullUpdate = false;
        }

        private bool ShowAvatarsInGame()
        {
            return Config.Instance.ShowAvatarsInGame && _currentScene == _gameSceneName;
        }

        private bool ShowAvatarsInRoom()
        {
            return Config.Instance.ShowAvatarsInRoom && _currentScene == _menuSceneName;
        }

        public void DestroyPlayerControllers()
        {
            try
            {
                foreach(var playerPair in players)
                {
                    if (playerPair.Value != null && !playerPair.Value.destroyed)
                        Destroy(playerPair.Value.gameObject);
                }
                players.Clear();
                Plugin.log.Debug("Destroyed player controllers!");
            }catch(Exception e)
            {
                Plugin.log.Critical(e);
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
                Plugin.log.Critical(e);
            }
        }

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO sender, LevelCompletionResults levelCompletionResults)
        {
            GameplayCoreSceneSetupData sceneData = sender.sceneSetupDataArray.First(x => x is GameplayCoreSceneSetupData) as GameplayCoreSceneSetupData;

            IDifficultyBeatmap difficultyBeatmap = sceneData.difficultyBeatmap;

            SoloFreePlayFlowCoordinator freePlayCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();

            PlayerDataModelSO dataModel = freePlayCoordinator.GetPrivateField<PlayerDataModelSO>("_playerDataModel");

            var playerLevelStats = dataModel.playerData.GetPlayerLevelStatsData(difficultyBeatmap);

            if (Client.Instance.inRoom)
            {
                PluginUI.instance.roomFlowCoordinator.levelDifficultyBeatmap = difficultyBeatmap;
                PluginUI.instance.roomFlowCoordinator.levelResults = levelCompletionResults;


                PluginUI.instance.roomFlowCoordinator.lastHighscoreValid = playerLevelStats.validScore;
                PluginUI.instance.roomFlowCoordinator.lastHighscoreForLevel = playerLevelStats.highScore;

                Plugin.log.Debug("Set level results!");
            }

            if (Config.Instance.SpectatorMode || Client.disableScoreSubmission || ScoreSubmission.Disabled || ScoreSubmission.ProlongedDisabled || sceneData.practiceSettings != null)
            {
                List<string> reasons = new List<string>();

                if (Config.Instance.SpectatorMode) reasons.Add("Spectator mode");
                if (Client.disableScoreSubmission) reasons.Add("Multiplayer score submission disabled by another mod");
                if (ScoreSubmission.Disabled) reasons.Add("Score submission is disabled by "+ ScoreSubmission.ModString);
                if (ScoreSubmission.ProlongedDisabled) reasons.Add("Score submission is disabled for a prolonged time by " + ScoreSubmission.ProlongedModString);
                if (sceneData.practiceSettings != null) reasons.Add("Practice mode");

                Plugin.log.Warn("\nScore submission is disabled! Reason:\n" +string.Join(",\n", reasons));
                return;
            }

            if (Config.Instance.SubmitScores == 0)
            {
                Plugin.log.Warn("Score submission is disabled!");
                return;
            }
            else if (Config.Instance.SubmitScores == 1)
            {
                bool submitScore = false;
                if (ScrappedData.Downloaded)
                {
                    ScrappedSong song = ScrappedData.Songs.FirstOrDefault(x => x.Hash == SongCore.Collections.hashForLevelID(difficultyBeatmap.level.levelID));
                    if (song != default)
                    {
                        DifficultyStats stats = song.Diffs.FirstOrDefault(x => x.Diff == difficultyBeatmap.difficulty.ToString().Replace("+", "Plus"));
                        if (stats != default)
                        {
                            if (stats.Ranked != 0)
                            {
                                submitScore = true;
                            }
                            else
                                Plugin.log.Warn("Song is unrakned!");
                        }
                        else
                            Plugin.log.Warn("Difficulty not found!");
                    }
                    else
                        Plugin.log.Warn("Song not found!");
                }
                else
                    Plugin.log.Warn("Scrapped data is not downloaded!");

                if (!submitScore)
                {
                    Plugin.log.Warn("Score submission is disabled!");
                    return;
                }
            }
            
            dataModel.playerData.playerAllOverallStatsData.UpdateSoloFreePlayOverallStatsData(levelCompletionResults, difficultyBeatmap);
            dataModel.Save();

            if (levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Failed && levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared)
            {
                return;
            }

            playerLevelStats.IncreaseNumberOfGameplays();
            if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
            {
                Plugin.log.Info("Submitting score...");
                playerLevelStats.UpdateScoreData(levelCompletionResults.modifiedScore, levelCompletionResults.maxCombo, levelCompletionResults.fullCombo, levelCompletionResults.rank);
                freePlayCoordinator.GetPrivateField<PlatformLeaderboardsModel>("_platformLeaderboardsModel").UploadScore(difficultyBeatmap, levelCompletionResults.rawScore, levelCompletionResults.modifiedScore, levelCompletionResults.fullCombo, levelCompletionResults.goodCutsCount, levelCompletionResults.badCutsCount, levelCompletionResults.missedCount, levelCompletionResults.maxCombo, levelCompletionResults.gameplayModifiers);
                Plugin.log.Info("Score submitted!");
            }
        }

        IEnumerator WaitForControllers()
        {
            Plugin.log.Debug("Waiting for game controllers...");

            yield return new WaitUntil(delegate () { return FindObjectOfType<ScoreController>() != null; });

            Plugin.log.Debug("Game controllers found!");

            _scoreController = FindObjectOfType<ScoreController>();

            if (_scoreController != null)
            {
                _scoreController.scoreDidChangeEvent += ScoreChanged;
                _scoreController.noteWasCutEvent += NoteWasCutEvent;
                _scoreController.comboDidChangeEvent += ComboDidChangeEvent;
                _scoreController.noteWasMissedEvent += NoteWasMissedEvent;
            }
            Plugin.log.Debug("Found score controller");

            _energyController = FindObjectOfType<GameEnergyCounter>();

            if (_energyController != null)
            {
                _energyController.gameEnergyDidChangeEvent += EnergyDidChangeEvent;
            }
            Plugin.log.Debug("Found energy controller");

            audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault(x => !(x is OnlineAudioTimeController));

            _pauseMenuManager = FindObjectsOfType<PauseMenuManager>().First();
            
            if (_pauseMenuManager != null)
            {
                _pauseMenuManager.GetPrivateField<Button>("_restartButton").interactable = false;
            }

            Plugin.log.Debug("Found pause manager");

            _loaded = true;
        }
        
        private void ShowMenu()
        {
            try
            {
                _pauseMenuManager.ShowMenu();
            }
            catch(Exception e)
            {
                Plugin.log.Error("Unable to show menu! Exception: " +e);
            }
        }

        public void PauseSong()
        {
            Resources.FindObjectsOfTypeAll<GameSongController>().First().PauseSong();
        }

        public void ResumeSong()
        {
            Resources.FindObjectsOfTypeAll<GameSongController>().First().ResumeSong();
        }

        private void EnergyDidChangeEvent(float energy)
        {
            Client.Instance.playerInfo.updateInfo.playerEnergy = energy * 100;
        }

        private void ComboDidChangeEvent(int obj)
        {
            Client.Instance.playerInfo.updateInfo.playerComboBlocks = (uint)obj;
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
                    Client.Instance.playerInfo.updateInfo.playerCutBlocks++;
                    Client.Instance.playerInfo.updateInfo.playerTotalBlocks++;
                }
                else
                {
                    Client.Instance.playerInfo.hitsLastUpdate.Add(new HitData(arg1, true, arg2));
                    Client.Instance.playerInfo.updateInfo.playerTotalBlocks++;
                }
            }
        }

        private void NoteWasMissedEvent(NoteData arg1, int arg2)
        {
            Client.Instance.playerInfo.hitsLastUpdate.Add(new HitData(arg1, false));
            Client.Instance.playerInfo.updateInfo.playerTotalBlocks++;
        }

        private void ScoreChanged(int rawScore, int score)
        {
            Client.Instance.playerInfo.updateInfo.playerScore = (uint)score;
        }
    }

}
