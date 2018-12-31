using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using CustomAvatar;
using CustomUI.BeatSaber;
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
    public class InGameOnlineController : MonoBehaviour
    {

        public static Quaternion oculusTouchRotOffset = Quaternion.Euler(-40f, 0f, 0f);
        public static Vector3 oculusTouchPosOffset = new Vector3(0f, 0f, 0.055f);
        public static Quaternion openVrRotOffset = Quaternion.Euler(-4.3f, 0f, 0f);
        public static Vector3 openVrPosOffset = new Vector3(0f, -0.008f, 0f);
        public static InGameOnlineController Instance;

        private StandardLevelGameplayManager _gameManager;
        private ScoreController _scoreController;
        private GameEnergyCounter _energyController;
        private PauseMenuManager _pauseMenuManager;
        public AudioTimeSyncController audioTimeSync;

        private List<AvatarController> _avatars = new List<AvatarController>();
        private List<PlayerInfoDisplay> _scoreDisplays = new List<PlayerInfoDisplay>();
        private GameObject _scoreScreen;

        private TextMeshPro _messageDisplayText;
        private float _messageDisplayTime;

        private string _currentScene;

        private bool loaded;

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
                        if (Client.instance != null && Client.instance.Connected)
                        {
                            StartCoroutine(WaitForControllers());
                        }
                    }
                    else if (_currentScene == "Menu")
                    {
                        loaded = false;
                        DestroyAvatars();
                        if (Client.instance != null && Client.instance.Connected)
                        {
                            PluginUI.instance.roomFlowCoordinator.ReturnToRoom();
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
            Client.instance.PacketReceived -= PacketReceived;
            Client.instance.PacketReceived += PacketReceived;
        }

        private void PacketReceived(BasePacket packet)
        {
            switch (packet.commandType)
            {
                case CommandType.UpdatePlayerInfo:
                    {
                        int playersCount = BitConverter.ToInt32(packet.additionalData, 8);

                        Stream byteStream = new MemoryStream(packet.additionalData, 12, packet.additionalData.Length - 12);

                        List<PlayerInfo> playerInfos = new List<PlayerInfo>();
                        for (int j = 0; j < playersCount; j++)
                        {
                            byte[] sizeBytes = new byte[4];
                            byteStream.Read(sizeBytes, 0, 4);

                            int playerInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                            byte[] playerInfoBytes = new byte[playerInfoSize];
                            byteStream.Read(playerInfoBytes, 0, playerInfoSize);

                            try
                            {
                                playerInfos.Add(new PlayerInfo(playerInfoBytes));
                            }
                            catch (Exception e)
                            {
#if DEBUG
                                Misc.Logger.Exception($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                            }
                        }

                        playerInfos = playerInfos.Where(x => (x.playerState == PlayerState.Game && _currentScene == "GameCore") || (x.playerState == PlayerState.Room && _currentScene == "Menu") || (x.playerState == PlayerState.DownloadingSongs && _currentScene == "Menu")).OrderByDescending(x => x.playerScore).ToList();

                        int localPlayerIndex = playerInfos.FindIndexInList(Client.instance.playerInfo);

                        if ((ShowAvatarsInGame() && !Config.Instance.SpectatorMode && loaded) || ShowAvatarsInRoom())
                        {
                            try
                            {
                                if (_avatars.Count > playerInfos.Count)
                                {
                                    for (int i = playerInfos.Count; i < _avatars.Count; i++)
                                    {
                                        if (_avatars[i] != null && _avatars[i].gameObject != null)
                                            Destroy(_avatars[i].gameObject);
                                    }
                                    _avatars.RemoveAll(x => x == null || x.gameObject == null);
                                }
                                else if (_avatars.Count < playerInfos.Count)
                                {
                                    for (int i = 0; i < (playerInfos.Count - _avatars.Count); i++)
                                    {
                                        _avatars.Add(new GameObject("Avatar").AddComponent<AvatarController>());
                                    }
                                }

                                List<PlayerInfo> _playerInfosByID = playerInfos.OrderBy(x => x.playerId).ToList();

                                for (int i = 0; i < playerInfos.Count; i++)
                                {
                                    if (_currentScene == "GameCore")
                                    {
                                        _avatars[i].SetPlayerInfo(_playerInfosByID[i], (i - _playerInfosByID.FindIndexInList(Client.instance.playerInfo)) * 3f, Client.instance.playerInfo.Equals(_playerInfosByID[i]));
                                    }
                                    else
                                    {
                                        _avatars[i].SetPlayerInfo(_playerInfosByID[i], 0f, Client.instance.playerInfo.Equals(_playerInfosByID[i]));
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"AVATARS EXCEPTION: {e}");
                            }
                        }

                        if (_currentScene == "GameCore" && loaded)
                        {
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

                        Client.instance.playerInfo.headPos = GetXRNodeWorldPosRot(XRNode.Head).Position;
                        Client.instance.playerInfo.headRot = GetXRNodeWorldPosRot(XRNode.Head).Rotation;
                        Client.instance.playerInfo.leftHandPos = GetXRNodeWorldPosRot(XRNode.LeftHand).Position;
                        Client.instance.playerInfo.leftHandRot = GetXRNodeWorldPosRot(XRNode.LeftHand).Rotation;

                        Client.instance.playerInfo.rightHandPos = GetXRNodeWorldPosRot(XRNode.RightHand).Position;
                        Client.instance.playerInfo.rightHandRot = GetXRNodeWorldPosRot(XRNode.RightHand).Rotation;

                        if (PersistentSingleton<VRPlatformHelper>.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
                        {
                            Client.instance.playerInfo.leftHandRot *= oculusTouchRotOffset;
                            Client.instance.playerInfo.leftHandPos += oculusTouchPosOffset;
                        }
                        else if (PersistentSingleton<VRPlatformHelper>.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
                        {
                            Client.instance.playerInfo.leftHandRot *= openVrRotOffset;
                            Client.instance.playerInfo.leftHandPos += openVrPosOffset;
                        }

                        if (_currentScene == "GameCore" && loaded)
                        {
                            Client.instance.playerInfo.playerProgress = audioTimeSync.songTime;
                        }
                        else
                        {
                            Client.instance.playerInfo.playerProgress = 0;
                        }

                        if (Config.Instance.SpectatorMode)
                        {
                            Client.instance.playerInfo.playerScore = 0;
                            Client.instance.playerInfo.playerEnergy = 0f;
                            Client.instance.playerInfo.playerCutBlocks = 0;
                            Client.instance.playerInfo.playerComboBlocks = 0;
                        }

                        Client.instance.SendPlayerInfo();
                    }; break;
                case CommandType.SetGameState:
                    {
                        if (_currentScene == "GameCore" && loaded)
                        {
                            PropertyInfo property = typeof(StandardLevelGameplayManager).GetProperty("gameState");
                            property.DeclaringType.GetProperty("gameState");
                            property.GetSetMethod(true).Invoke(_gameManager, new object[] { (StandardLevelGameplayManager.GameState)packet.additionalData[0] });
                        }
                    }
                    break;
                case CommandType.DisplayMessage:
                    {
                        _messageDisplayTime = BitConverter.ToSingle(packet.additionalData, 0);
                        _messageDisplayText.fontSize = BitConverter.ToSingle(packet.additionalData, 4);

                        int messageSize = BitConverter.ToInt32(packet.additionalData, 8);

                        _messageDisplayText.text = Encoding.UTF8.GetString(packet.additionalData, 12, messageSize);
                    };break;
            }
        }

        public void Update()
        {
            if(_messageDisplayTime > 0f)
            {
                _messageDisplayTime -= Time.deltaTime;
                if(_messageDisplayTime <= 0f)
                {
                    _messageDisplayTime = 0f;
                    _messageDisplayText.text = "";
                }
            }
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
                Misc.Logger.Info("Destroying avatars");
                for (int i = 0; i < _avatars.Count; i++)
                {
                    if (_avatars[i] != null)
                        Destroy(_avatars[i].gameObject);
                }
                _avatars.Clear();
            }catch(Exception e)
            {
                Misc.Logger.Exception($"Unable to destroy avatars! Exception: {e}");
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

        public void SongFinished(StandardLevelSceneSetupDataSO sender, LevelCompletionResults levelCompletionResults, IDifficultyBeatmap difficultyBeatmap, GameplayModifiers gameplayModifiers)
        {
            if (Config.Instance.SpectatorMode)
                return;

            Misc.Logger.Info("PlayerDataModels: "+ Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().Count());
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
            Client.instance.playerInfo.playerEnergy = (int)Math.Round(energy * 100);
        }

        private void ComboDidChangeEvent(int obj)
        {
            Client.instance.playerInfo.playerComboBlocks = (uint)obj;
        }

        private void NoteWasCutEvent(NoteData arg1, NoteCutInfo arg2, int score)
        {
            if (arg2.allIsOK)
            {
                Client.instance.playerInfo.playerCutBlocks++;
                Client.instance.playerInfo.playerTotalBlocks++;
            }
            else
            {
                Client.instance.playerInfo.playerTotalBlocks++;
            }
        }

        private void NoteWasMissedEvent(NoteData arg1, int arg2)
        {
            Client.instance.playerInfo.playerTotalBlocks++;
        }

        private void ScoreChanged(int score)
        {
            Client.instance.playerInfo.playerScore = (uint)score;
        }
    }

}
