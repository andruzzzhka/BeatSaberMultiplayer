using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
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
        private VRCenterAdjust _roomAdjust;

        public Vector3 roomPositionOffset;
        public Quaternion roomRotationOffset;

        private List<AvatarController> _avatars = new List<AvatarController>();
        private List<PlayerInfoDisplay> _scoreDisplays = new List<PlayerInfoDisplay>();

        private Scene _currentScene;

        public static void OnLoad()
        {
            if (Instance != null)
                return;
            new GameObject("InGameOnlineController").AddComponent<InGameOnlineController>();
        }

        public void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Client.ClientCreated += ClientCreated;
            _currentScene = SceneManager.GetActiveScene();
        }

        private void ClientCreated()
        {
            Client.instance.PacketReceived += PacketReceived;
        }

        private void PacketReceived(BasePacket packet)
        {
#if DEBUG
            Log.Info("Packet received");
#endif
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

                Log.Info(playerInfos.Count+" players in the room");
                
                int localPlayerIndex = playerInfos.FindIndexInList(Client.instance.playerInfo);

                
                if ((Config.Instance.ShowAvatarsInGame && _currentScene.name == "StandardLevel") || (Config.Instance.ShowAvatarsInRoom && _currentScene.name == "Menu"))
                {
                    try
                    {
                        if (_avatars.Count > playerInfos.Count)
                        {
                            List<AvatarController> avatarsToRemove = new List<AvatarController>();
                            for (int i = playerInfos.Count; i < _avatars.Count; i++)
                            {
                                avatarsToRemove.Add(_avatars[i]);
                            }
                            foreach (AvatarController avatar in avatarsToRemove)
                            {
                                _avatars.Remove(avatar);
                                Destroy(avatar.gameObject);
                            }

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
                        int displaysToCreate = 5 - _scoreDisplays.Count;
                        for(int i = 0; i < displaysToCreate; i++)
                        {
                            _scoreDisplays.Add(new GameObject("ScoreDisplay").AddComponent<PlayerInfoDisplay>());
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

                Client.instance.playerInfo.headPos = InputTracking.GetLocalPosition(XRNode.Head);
                Client.instance.playerInfo.headRot = InputTracking.GetLocalRotation(XRNode.Head);
                Client.instance.playerInfo.leftHandPos = InputTracking.GetLocalPosition(XRNode.LeftHand);
                Client.instance.playerInfo.leftHandRot = InputTracking.GetLocalRotation(XRNode.LeftHand);
                Client.instance.playerInfo.rightHandPos = InputTracking.GetLocalPosition(XRNode.RightHand);
                Client.instance.playerInfo.rightHandRot = InputTracking.GetLocalRotation(XRNode.RightHand);

                Client.instance.SendPlayerInfo();
            }
        }

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            if(next.name == "StandardLevel")
            {
                if (Client.instance.Connected)
                {
                    StartCoroutine(FindControllers());
                }
            }
            else if(next.name == "Menu")
            {
                if (Client.instance.Connected)
                {
                    StartCoroutine(ReturnToRoom());
                }
            }
            _currentScene = next;
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
            _roomAdjust = FindObjectOfType<VRCenterAdjust>();

            if (_roomAdjust != null)
            {
                roomPositionOffset = _roomAdjust.transform.position;
                roomRotationOffset = _roomAdjust.transform.rotation;
            }
            

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
