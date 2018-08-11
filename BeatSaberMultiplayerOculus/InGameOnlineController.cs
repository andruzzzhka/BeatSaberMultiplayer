using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using CustomAvatar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using VRUI;

namespace BeatSaberMultiplayer
{
    class InGameOnlineController : MonoBehaviour
    {
        public static InGameOnlineController Instance;

        private GameplayManager _gameManager;
        private ScoreController _scoreController;
        private GameEnergyCounter _energyController;

        private List<AvatarController> _avatars = new List<AvatarController>();
        private List<PlayerInfoDisplay> _scoreDisplays = new List<PlayerInfoDisplay>();
        private GameObject _scoreScreen;

        private Scene _currentScene;

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
                DontDestroyOnLoad(this);

                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                Client.ClientCreated += ClientCreated;
                _currentScene = SceneManager.GetActiveScene();
            }
        }

        private void ClientCreated()
        {
            Client.instance.PacketReceived += PacketReceived;
        }

        private void PacketReceived(BasePacket packet)
        {
            if (packet.commandType == CommandType.UpdatePlayerInfo)
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

                    playerInfos.Add(new PlayerInfo(playerInfoBytes));
                }

                playerInfos = playerInfos.Where(x => (x.playerState == PlayerState.Game && _currentScene.name == "StandardLevel") || (x.playerState == PlayerState.Room && _currentScene.name == "Menu") || (x.playerState == PlayerState.DownloadingSongs && _currentScene.name == "Menu")).OrderByDescending(x => x.playerScore).ToList();

                int localPlayerIndex = playerInfos.FindIndexInList(Client.instance.playerInfo);

                if ((Config.Instance.ShowAvatarsInGame && _currentScene.name == "StandardLevel") || (Config.Instance.ShowAvatarsInRoom && _currentScene.name == "Menu"))
                {
                    try
                    {
                        if (_avatars.Count > playerInfos.Count)
                        {
                            for (int i = playerInfos.Count; i < _avatars.Count; i++)
                            {
                                if(_avatars[i] != null && _avatars[i].gameObject != null)
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
                            if (_currentScene.name == "StandardLevel")
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

                if (_currentScene.name == "StandardLevel")
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

                Client.instance.SendPlayerInfo();
            }
        }

        private static PosRot GetXRNodeWorldPosRot(XRNode node)
        {
            var pos = InputTracking.GetLocalPosition(node);
            var rot = InputTracking.GetLocalRotation(node);

            var roomCenter = BeatSaberUtil.GetRoomCenter();
            var roomRotation = BeatSaberUtil.GetRoomRotation();
            pos += roomCenter;
            pos = roomRotation * pos;
            rot = roomRotation * rot;
            return new PosRot(pos, rot);
        }

        public void DestroyAvatars()
        {
            try
            {
                for (int i = 0; i < _avatars.Count; i++)
                {
                    if (_avatars[i] != null)
                        Destroy(_avatars[i].gameObject);
                }
                _avatars.Clear();
            }catch(Exception e)
            {
                Log.Exception($"Can't destroy avatars! Exception: {e}");
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
                Log.Exception($"Can't destroy score screens! Exception: {e}");
            }
        }

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            try
            {
                _currentScene = next;
                if (_currentScene.name == "StandardLevel")
                {
                    DestroyAvatars();
                    DestroyScoreScreens();
                    if (Client.instance != null && Client.instance.Connected)
                    {
                        StartCoroutine(FindControllers());
                    }
                }
                else if (_currentScene.name == "Menu")
                {
                    DestroyAvatars();
                    if (Client.instance != null && Client.instance.Connected)
                    {
                        StartCoroutine(ReturnToRoom());
                    }
                }
            }catch(Exception e)
            {
                Log.Exception($"Exception on {_currentScene.name} scene load! {e}");
            }
        }

        public void SongFinished(MainGameSceneSetupData sender, LevelCompletionResults result)
        {
            sender.didFinishEvent -= SongFinished;
            Resources.FindObjectsOfTypeAll<MenuSceneSetupData>().First().TransitionToScene((result == null) ? 0.35f : 1.3f);
        }

        IEnumerator ReturnToRoom()
        {
            yield return new WaitUntil(delegate () { return Resources.FindObjectsOfTypeAll<VRUIScreenSystem>().Any(); });
            VRUIScreenSystem screenSystem = Resources.FindObjectsOfTypeAll<VRUIScreenSystem>().First();

            yield return new WaitWhile(delegate () { return screenSystem.mainScreen == null; });
            yield return new WaitWhile(delegate () { return screenSystem.mainScreen.rootViewController == null; });

            try
            {

                VRUIViewController root = screenSystem.mainScreen.rootViewController;

                List<VRUIViewController> children = new List<VRUIViewController>();

                children.Add(root);

                while (children.Last().childViewController != null)
                {
                    children.Add(children.Last().childViewController);
                }

                children.Reverse();
                children.Remove(root);
                children.ForEach(x => {
#if DEBUG
                    Log.Info($"Dismissing {x.name}...");
#endif
                    x.DismissModalViewController(null, true); });

                PluginUI.instance.serverHubFlowCoordinator.ReturnToRoom();
            }
            catch (Exception e)
            {
                Log.Exception($"MENU EXCEPTION: {e}");
            }

        }

        IEnumerator FindControllers()
        {
#if DEBUG
            Log.Info("Waiting for game controllers...");
#endif
            yield return new WaitUntil(delegate () { return FindObjectOfType<ScoreController>() != null; });
#if DEBUG
            Log.Info("Controllers found!");
#endif
            _gameManager = Resources.FindObjectsOfTypeAll<GameplayManager>().First();

            if (_gameManager != null)
            {
                try
                {
                    if (ReflectionUtil.GetPrivateField<IPauseTrigger>(_gameManager, "_pauseTrigger") != null)
                    {
                        ReflectionUtil.GetPrivateField<IPauseTrigger>(_gameManager, "_pauseTrigger").SetCallback(delegate () { });
                    }

                    if (ReflectionUtil.GetPrivateField<VRPlatformHelper>(_gameManager, "_vrPlatformHelper") != null)
                    {
                        ReflectionUtil.GetPrivateField<VRPlatformHelper>(_gameManager, "_vrPlatformHelper").inputFocusWasCapturedEvent -= _gameManager.HandleInputFocusWasCaptured;
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e.ToString());
                }
            }
#if DEBUG
            Log.Info("Disabled pause button!");
#endif
            _scoreController = FindObjectOfType<ScoreController>();

            if (_scoreController != null)
            {
                _scoreController.scoreDidChangeEvent += ScoreChanged;
                _scoreController.noteWasCutEvent += noteWasCutEvent;
                _scoreController.comboDidChangeEvent += comboDidChangeEvent;
            }
#if DEBUG
            Log.Info("Found score controller");
#endif

            _energyController = FindObjectOfType<GameEnergyCounter>();

            if (_energyController != null)
            {
                _energyController.gameEnergyDidChangeEvent += EnergyDidChangeEvent;
            }
#if DEBUG
            Log.Info("Found energy controller");
#endif
        }

        private void EnergyDidChangeEvent(float energy)
        {
            Client.instance.playerInfo.playerEnergy = (int)Math.Round(energy * 100);
        }

        private void comboDidChangeEvent(int obj)
        {
            Client.instance.playerInfo.playerComboBlocks = (uint)obj;
        }

        private void noteWasCutEvent(NoteData arg1, NoteCutInfo arg2, int score)
        {
            if (arg2.allIsOK)
                Client.instance.playerInfo.playerCutBlocks++;
        }

        private void ScoreChanged(int score)
        {
            Client.instance.playerInfo.playerScore = (uint)score;
        }
    }

}
