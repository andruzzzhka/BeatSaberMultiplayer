using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.OverriddenClasses;
using BeatSaberMultiplayer.UI;
using BeatSaberMultiplayer.VOIP;
using BS_Utils.Gameplay;
using CustomAvatar;
using CustomUI.BeatSaber;
using Lidgren.Network;
using SongCore.Utilities;
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
        private const float _PTTReleaseDelay = 0.2f;

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

        private PlayerAvatarInput _avatarInput;

        private Dictionary<ulong, OnlinePlayerController> _players = new Dictionary<ulong, OnlinePlayerController>();
        private List<PlayerInfoDisplay> _scoreDisplays = new List<PlayerInfoDisplay>();
        private GameObject _scoreScreen;

        private TextMeshPro _messageDisplayText;
        private float _messageDisplayTime;

        private string _currentScene;
        private bool _loaded;
        private int _sendRateCounter;
        private int _fixedSendRate = 0;
        private bool _spectatorInRoom;

        private HSBColor _color;
        private float _colorCounter;
        private bool _colorChanger;

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
            if (_players != null)
            {
                foreach (var player in _players.Values.Where(x => x != null && !x.destroyed)) {
                    player.SetVoIPVolume(volume);
                }
            }
        }

        public void VoiceChatSpatialAudioChanged(bool enabled)
        {
            if (_players != null)
            {
                foreach (var player in _players.Values.Where(x => x != null && !x.destroyed))
                {
                    player.SetSpatialAudioState(enabled);
                }
            }
        }

        public bool VoiceChatIsTalking(ulong playerId)
        {
            if(Config.Instance.EnableVoiceChat && _players != null)
                if (playerId == Client.Instance.playerInfo.playerId)
                    return isRecording;
                else
                    if (_players.TryGetValue(playerId, out OnlinePlayerController player))
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

        public void MenuSceneLoaded()
        {
            _currentScene = "MenuCore";
            _loaded = false;
            DestroyPlayerControllers();
            if (Client.Instance != null && Client.Instance.connected)
            {
                if (Client.Instance.inRadioMode)
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

        public void GameSceneLoaded()
        {
            _currentScene = "GameCore";
            DestroyPlayerControllers();
            DestroyScoreScreens();
            if (Client.Instance != null && Client.Instance.connected)
            {
                StartCoroutine(WaitForControllers());
                needToSendUpdates = true;
            }
        }


        private void PacketReceived(NetIncomingMessage msg)
        {
            if(msg == null)
            {
                if (_currentScene == "GameCore" && _loaded)
                {
                    PropertyInfo property = typeof(StandardLevelGameplayManager).GetProperty("gameState");
                    property.DeclaringType.GetProperty("gameState");
                    property.GetSetMethod(true).Invoke(_gameManager, new object[] { StandardLevelGameplayManager.GameState.Failed });
                }
                return;
            }

            switch ((CommandType)msg.ReadByte())
            {
                case CommandType.UpdatePlayerInfo:
                    {
                        float currentTime = msg.ReadFloat();
                        float totalTime = msg.ReadFloat();

                        int playersCount = msg.ReadInt32();
                        List<PlayerInfo> playerInfos = new List<PlayerInfo>(playersCount);
                        try
                        {
                            PlayerInfo newPlayer;
                            _spectatorInRoom = false;

                            for (int j = 0; j < playersCount; j++)
                            {
                                newPlayer = new PlayerInfo(msg);
                                if( (newPlayer.playerState == PlayerState.Game && _currentScene == "GameCore") || 
                                    (newPlayer.playerState == PlayerState.Room && _currentScene == "MenuCore") || 
                                    (newPlayer.playerState == PlayerState.DownloadingSongs && _currentScene == "MenuCore"))
                                    playerInfos.Add(newPlayer);
                                _spectatorInRoom |= newPlayer.playerState == PlayerState.Spectating;
                            }
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            Plugin.log.Critical($"Unable to parse PlayerInfo! Player count={playersCount} Message size={msg.LengthBytes} Excpetion: {e}");
#endif
                            return;
                        }

                        int localPlayerIndex = playerInfos.FindIndexInList(Client.Instance.playerInfo);
                        
                        try
                        {
                            int index = 0;
                            OnlinePlayerController player = null;

                            foreach (PlayerInfo info in playerInfos)
                            {
                                try
                                {
                                    if (_players.ContainsKey(info.playerId))
                                    {
                                        player = _players[info.playerId];
                                        if(player == null)
                                        {
                                            player = new GameObject("OnlinePlayerController").AddComponent<OnlinePlayerController>();
                                            _players[info.playerId] = player;
                                        }
                                    }
                                    else
                                    { 
                                        player = new GameObject("OnlinePlayerController").AddComponent<OnlinePlayerController>();
                                        _players.Add(info.playerId, player);
                                    }

                                    player.PlayerInfo = info;
                                    player.avatarOffset = (index - localPlayerIndex) * (_currentScene == "GameCore" ? 5f : 0f);
                                    player.SetAvatarState(((ShowAvatarsInGame() && !Config.Instance.SpectatorMode && _loaded) || ShowAvatarsInRoom()) && !Client.Instance.inRadioMode);

                                    index++;
                                }
                                catch(Exception e)
                                {
                                    Console.WriteLine($"PlayerController exception: {e}");
                                }
                            }


                            



                            if (_players.Count > playerInfos.Count)
                            {
                                Dictionary<ulong, OnlinePlayerController> FindMissing(Dictionary<ulong, OnlinePlayerController> players, List<PlayerInfo> infos)
                                {
                                    Dictionary<ulong, OnlinePlayerController> missing = new Dictionary<ulong, OnlinePlayerController>();

                                    HashSet<ulong> s = new HashSet<ulong>();
                                    for (int i = 0; i < infos.Count; i++)
                                        s.Add(infos[i].playerId);

                                    foreach (var pair in players)
                                        if (!s.Contains(pair.Key))
                                            missing.Add(pair.Key, pair.Value);

                                    return missing;
                                }


                                var playersToRemove = FindMissing(_players, playerInfos);

                                foreach (var playerPair in playersToRemove)
                                {
                                    if (playerPair.Value != null && !playerPair.Value.destroyed)
                                    {
                                        Destroy(playerPair.Value.gameObject);
                                        _players.Remove(playerPair.Key);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"PlayerControllers exception: {e}");
                        }
                        
                        if (_currentScene == "GameCore" && _loaded)
                        {
                            playerInfos = playerInfos.OrderByDescending(x => x.playerScore).ToList();
                            localPlayerIndex = playerInfos.FindIndexInList(Client.Instance.playerInfo);
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

                        foreach (var playerPair in _players)
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
                                        Plugin.log.Info("New Speex decoder created!");
                                    }

                                    if(_players.TryGetValue(data.playerId, out OnlinePlayerController player))
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
                        if (_currentScene == "GameCore" && _loaded)
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
                else
                    isRecording = false;

                if (VRControllersInputManager.TriggerValue(XRNode.LeftHand) > 0.85f && ControllersHelper.GetRightGrip() && VRControllersInputManager.TriggerValue(XRNode.RightHand) > 0.85f && ControllersHelper.GetLeftGrip())
                {
                    _colorCounter += Time.deltaTime;
                    if (_colorCounter > 7.5f)
                    {
                        _color = new HSBColor(0f, 1f, 1f);
                        _colorChanger = !_colorChanger;
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
                if (_colorChanger)
                {
                    Client.Instance.playerInfo.playerNameColor = HSBColor.ToColor(_color);
                    _color.h += 0.001388f;
                    if(_color.h >= 1f)
                    {
                        _color.h = 0f;
                    }
                }
                if (_fixedSendRate == 1 || (_fixedSendRate == 0 && Client.Instance.tickrate > (1f / Time.deltaTime / 3f * 2f + 5f)) || _spectatorInRoom)
                {
                    _sendRateCounter = 0;
                    UpdatePlayerInfo();
#if DEBUG && VERBOSE
                    Plugin.log.Info($"Full send rate! FPS: {(1f / Time.deltaTime).ToString("0.0")}, TPS: {Client.Instance.Tickrate.ToString("0.0")}, Trigger: TPS>{1f / Time.deltaTime / 3f * 2f + 5f}");
#endif
                }
                else if (_fixedSendRate == 2 || (_fixedSendRate == 0 && Client.Instance.tickrate > (1f / Time.deltaTime / 3f + 5f)))
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
                else if (_fixedSendRate == 3 || (_fixedSendRate == 0 && Client.Instance.tickrate <= (1f / Time.deltaTime / 3f + 5f)))
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

                if (string.IsNullOrEmpty(Client.Instance.playerInfo.avatarHash))
                {
                    Client.Instance.playerInfo.avatarHash = PlayerInfo.avatarHashPlaceholder;
                }
            }
        }

        public void UpdatePlayerInfo()
        {

            if (string.IsNullOrEmpty(Client.Instance.playerInfo.avatarHash) || Client.Instance.playerInfo.avatarHash == PlayerInfo.avatarHashPlaceholder)
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
                if (Client.Instance.playerInfo.avatarHash != PlayerInfo.avatarHashPlaceholder)
                {
                    Plugin.log.Info("Updating avatar hash... New hash: " + (Client.Instance.playerInfo.avatarHash));
                }
#endif
            }

            if(_avatarInput == null)
            {
                _avatarInput = CustomAvatar.Plugin.Instance.PlayerAvatarManager._playerAvatarInput;
            }

            Client.Instance.playerInfo.headPos = _avatarInput.HeadPosRot.Position;
            Client.Instance.playerInfo.headRot = _avatarInput.HeadPosRot.Rotation;

            Client.Instance.playerInfo.leftHandPos = _avatarInput.LeftPosRot.Position;
            Client.Instance.playerInfo.leftHandRot = _avatarInput.LeftPosRot.Rotation;

            Client.Instance.playerInfo.rightHandPos = _avatarInput.RightPosRot.Position;
            Client.Instance.playerInfo.rightHandRot = _avatarInput.RightPosRot.Rotation;

            if (CustomAvatar.Plugin.IsFullBodyTracking)
            {
                Client.Instance.playerInfo.fullBodyTracking = true;

                Client.Instance.playerInfo.pelvisPos = _avatarInput.PelvisPosRot.Position;
                Client.Instance.playerInfo.pelvisRot = _avatarInput.PelvisPosRot.Rotation;

                Client.Instance.playerInfo.leftLegPos = _avatarInput.LeftLegPosRot.Position;
                Client.Instance.playerInfo.leftLegRot = _avatarInput.LeftLegPosRot.Rotation;

                Client.Instance.playerInfo.rightLegPos = _avatarInput.RightLegPosRot.Position;
                Client.Instance.playerInfo.rightLegRot = _avatarInput.RightLegPosRot.Rotation;
            }
            else
            {
                Client.Instance.playerInfo.fullBodyTracking = false;
            }

            if(_vrPlatformHelper == null)
            {
                _vrPlatformHelper = PersistentSingleton<VRPlatformHelper>.instance;
            }

            if (_vrPlatformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                Client.Instance.playerInfo.leftHandRot *= oculusTouchRotOffset;
                Client.Instance.playerInfo.leftHandPos += oculusTouchPosOffset;
                Client.Instance.playerInfo.rightHandRot *= oculusTouchRotOffset;
                Client.Instance.playerInfo.rightHandPos += oculusTouchPosOffset;
            }
            else if (_vrPlatformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
            {
                Client.Instance.playerInfo.leftHandRot *= openVrRotOffset;
                Client.Instance.playerInfo.leftHandPos += openVrPosOffset;
                Client.Instance.playerInfo.rightHandRot *= openVrRotOffset;
                Client.Instance.playerInfo.rightHandPos += openVrPosOffset;
            }

            if (_currentScene == "GameCore" && _loaded)
            {
                Client.Instance.playerInfo.playerProgress = audioTimeSync.songTime;
            }
            else if(Client.Instance.playerInfo.playerState != PlayerState.DownloadingSongs && Client.Instance.playerInfo.playerState != PlayerState.Game)
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
            return Config.Instance.ShowAvatarsInRoom && _currentScene == "MenuCore";
        }

        public void DestroyPlayerControllers()
        {
            try
            {
                foreach(var playerPair in _players)
                {
                    if (playerPair.Value != null && !playerPair.Value.destroyed)
                        Destroy(playerPair.Value.gameObject);
                }
                _players.Clear();
                Plugin.log.Info("Destroyed player controllers!");
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

        public void SongFinished(StandardLevelScenesTransitionSetupDataSO sender, LevelCompletionResults levelCompletionResults, IDifficultyBeatmap difficultyBeatmap, GameplayModifiers gameplayModifiers, bool practice)
        {
            if(Client.Instance.inRadioMode)
            {
                PluginUI.instance.radioFlowCoordinator.lastDifficulty = difficultyBeatmap;
                PluginUI.instance.radioFlowCoordinator.lastResults = levelCompletionResults;
            }

            if (Config.Instance.SpectatorMode || Client.disableScoreSubmission || ScoreSubmission.Disabled || ScoreSubmission.ProlongedDisabled || practice)
            {
                List<string> reasons = new List<string>();

                if (Config.Instance.SpectatorMode) reasons.Add("Spectator mode");
                if (Client.disableScoreSubmission) reasons.Add("Multiplayer score submission disabled by another mod");
                if (ScoreSubmission.Disabled) reasons.Add("Score submission is disabled by "+ ScoreSubmission.ModString);
                if (ScoreSubmission.ProlongedDisabled) reasons.Add("Score submission is disabled for a prolonged time by " + ScoreSubmission.ProlongedModString);
                if (practice) reasons.Add("Practice mode");

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

            AchievementsEvaluationHandler achievementsHandler = Resources.FindObjectsOfTypeAll<AchievementsEvaluationHandler>().First();
            achievementsHandler.ProcessLevelFinishData(difficultyBeatmap, levelCompletionResults);
            achievementsHandler.ProcessSoloFreePlayLevelFinishData(difficultyBeatmap, levelCompletionResults);

            SoloFreePlayFlowCoordinator freePlayCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
            
            PlayerDataModelSO.LocalPlayer currentLocalPlayer = freePlayCoordinator.GetPrivateField<PlayerDataModelSO>("_playerDataModel").currentLocalPlayer;

            currentLocalPlayer.playerAllOverallStatsData.soloFreePlayOverallStatsData.UpdateWithLevelCompletionResults(levelCompletionResults);

            string levelID = difficultyBeatmap.level.levelID;
            BeatmapDifficulty difficulty = difficultyBeatmap.difficulty;
            BeatmapCharacteristicSO beatmapCharacteristic = difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic;
            PlayerLevelStatsData playerLevelStatsData = currentLocalPlayer.GetPlayerLevelStatsData(levelID, difficulty, beatmapCharacteristic);
            playerLevelStatsData.IncreaseNumberOfGameplays();
            if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
            {
                Plugin.log.Info("Submitting score...");
                playerLevelStatsData.UpdateScoreData(levelCompletionResults.modifiedScore, levelCompletionResults.maxCombo, levelCompletionResults.fullCombo, levelCompletionResults.rank);
                freePlayCoordinator.GetPrivateField<PlatformLeaderboardsModel>("_platformLeaderboardsModel").AddScore(difficultyBeatmap, levelCompletionResults.rawScore, levelCompletionResults.modifiedScore, gameplayModifiers);
                Plugin.log.Info("Score submitted!");
            }
        }

        IEnumerator WaitForControllers()
        {
#if DEBUG
            Plugin.log.Info("Waiting for game controllers...");
#endif
            yield return new WaitUntil(delegate () { return FindObjectOfType<ScoreController>() != null; });
#if DEBUG
            Plugin.log.Info("Game controllers found!");
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
                    Plugin.log.Critical(e);
                }
            }
#if DEBUG
            Plugin.log.Info("Disabled pause button!");
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
            Plugin.log.Info("Found score controller");
#endif

            _energyController = FindObjectOfType<GameEnergyCounter>();

            if (_energyController != null)
            {
                _energyController.gameEnergyDidChangeEvent += EnergyDidChangeEvent;
            }
#if DEBUG
            Plugin.log.Info("Found energy controller");
#endif

            audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault(x => !(x is OnlineAudioTimeController));

            _pauseMenuManager = FindObjectsOfType<PauseMenuManager>().First();
            
            if (_pauseMenuManager != null)
            {
                _pauseMenuManager.GetPrivateField<Button>("_restartButton").interactable = false;
            }

#if DEBUG
            Plugin.log.Info("Found pause manager");
#endif

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
            Client.Instance.playerInfo.playerEnergy = energy * 100;
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

        private void ScoreChanged(int rawScore, int score)
        {
            Client.Instance.playerInfo.playerScore = (uint)score;
        }
    }

}
