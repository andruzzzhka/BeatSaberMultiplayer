using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer
{
    class BSMultiplayerMain : MonoBehaviour
    {
        BSMultiplayerUI ui;

        private TcpClient _connection;
        private NetworkStream _connectionStream;

        public static BSMultiplayerMain _instance;
        public MainGameSceneSetupData _mainGameSceneSetupData;
        GameplayManager _gameManager;

        ScoreController _scoreController;

        PlayerInfo playerInfo;

        GameObject scoreScreen;

        List<PlayerInfoDisplay> scoreDisplays = new List<PlayerInfoDisplay>();

        float _sendRate = 1f;
        float _sendTimer = 0;

        static int _loadedlevel;

        public event Action<string[]> DataReceived;


        int lastLocalPlayerIndex = -1;
        int localPlayerIndex = -1;
        public List<PlayerInfo> _playerInfos = new List<PlayerInfo>();

        public static void OnLoad(int level)
        {
            _loadedlevel = level;

            if (_instance == null)
            {
                new GameObject("BeatSaberMultiplayer").AddComponent<BSMultiplayerMain>();
                return;
            }
            else
            {
                _instance.OnLevelChange();
            }


            

        }


        public bool ConnectToServer()
        {
            try
            {
                if(_connection != null && _connection.Connected)
                {
                     return true;
                }
                
                _connection = new TcpClient(Config.Instance.IP, Config.Instance.Port);
                _connectionStream = _connection.GetStream();
                return true;
                
            }catch(Exception e)
            {
                return false;
            }
        }

        public bool DisconnectFromServer()
        {
            try
            {
                _connection.Close();
                _connectionStream = null;
                return true;
            }
            catch (Exception e)
            {
                
                return false;

            }
        }


        public void OnLevelChange()
        {
            if(_loadedlevel > 2 && _connection.Connected)
            {
                StartCoroutine(WaitForControllers());

                

                try
                {

                   
                    playerInfo = new PlayerInfo(SteamFriends.GetPersonaName(), SteamUser.GetSteamID().ToString());

                    SendPlayerInfo();

                    DataReceived += ReceivedFromServer;
                    StartCoroutine(ReceiveFromServerCoroutine());

                    try
                    {
                        scoreScreen = new GameObject("ScoreScreen");
                        scoreScreen.transform.position = new Vector3(0f, 4f, 12f);
                        scoreScreen.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

                        scoreDisplays.Clear();

                        for (int i = 0; i < 5; i++)
                        {
                            PlayerInfoDisplay buffer = new GameObject("ScoreDisplay").AddComponent<PlayerInfoDisplay>();
                            buffer.transform.SetParent(scoreScreen.transform);
                            buffer.transform.localPosition = new Vector3(0f,2.5f-i,0);
                            
                            scoreDisplays.Add(buffer);
                        }


                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("SCREEN EXCEPTION: "+e);
                    }
                   
                }
                catch (Exception e)
                {
                    Console.WriteLine("Can't connect to the server! Exception: " + e);
                }
            }
            else
            {
                if(_loadedlevel <= 1)
                {
                    DataReceived -= ReceivedFromServer;
                    DisconnectFromServer();
                }
            }

        }

        private void ReceivedFromServer(string[] _data)
        {
            foreach (string data in _data)
            {
                try
                {
                    ServerCommand command = JsonUtility.FromJson<ServerCommand>(data);

                    if (command.commandType == ServerCommandType.SetPlayerInfos)
                    {
                        _playerInfos.Clear();
                        foreach (string playerStr in command.playerInfos)
                        {
                            PlayerInfo player = JsonUtility.FromJson<PlayerInfo>(playerStr);
                            _playerInfos.Add(player);
                        }


                        lastLocalPlayerIndex = localPlayerIndex;
                        localPlayerIndex = FindIndexInList(playerInfo);

                        if (_playerInfos.Count <= 5)
                        {
                            for (int i = 0; i < _playerInfos.Count; i++)
                            {
                                scoreDisplays[i].UpdatePlayerInfo(_playerInfos[i], FindIndexInList(_playerInfos[i]));
                            }
                        }
                        else
                        {
                            

                            if(localPlayerIndex < 3)
                            {
                                for (int i = 0; i < 5; i++)
                                {
                                    scoreDisplays[i].UpdatePlayerInfo(_playerInfos[i], FindIndexInList(_playerInfos[i]));
                                }
                            }else if(localPlayerIndex > _playerInfos.Count - 3)
                            {
                                for (int i = _playerInfos.Count - 5; i < _playerInfos.Count; i++)
                                {
                                    scoreDisplays[i-(_playerInfos.Count - 5)].UpdatePlayerInfo(_playerInfos[i], FindIndexInList(_playerInfos[i]));
                                }
                            }
                            else
                            {
                                for (int i = localPlayerIndex - 2; i < localPlayerIndex + 3; i++)
                                {
                                    scoreDisplays[i - (localPlayerIndex - 2)].UpdatePlayerInfo(_playerInfos[i], FindIndexInList(_playerInfos[i]));
                                }
                            }

                        }

                        Console.WriteLine(lastLocalPlayerIndex + " " + localPlayerIndex);
                        if (lastLocalPlayerIndex != 0 && localPlayerIndex == 0)
                        {
                            TextMeshPro player1stPlaceText = ui.CreateWorldText(transform, "You are number one!");
                            player1stPlaceText.transform.position = new Vector3(0f, 1f, 12f);
                            player1stPlaceText.fontSize = 10f;
                            Destroy(player1stPlaceText.gameObject,2f);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("EXCEPTION ON RECEIVED: " + e);
                }
            }

            StartCoroutine(ReceiveFromServerCoroutine());
        }

        private int FindIndexInList(PlayerInfo _player)
        {
            return _playerInfos.FindIndex(x => (x.playerId == _player.playerId) && (x.playerName == _player.playerName));
        }

        private void Awake()
        {
            _instance = this;

            DontDestroyOnLoad(this);

            StartCoroutine(WaitForGameData());

            ui = BSMultiplayerUI._instance;
        }

        IEnumerator WaitForGameData()
        {
            Console.ForegroundColor = ConsoleColor.Gray;

            yield return new WaitUntil(delegate() { return Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().Count() > 0; });
            
            _mainGameSceneSetupData = Resources.FindObjectsOfTypeAll<MainGameSceneSetupData>().First();
            
            
        }

        public IEnumerator ReceiveFromServerCoroutine()
        {
            if (_connection.Available == 0)
            {
                yield return new WaitUntil(delegate() { return _connection.Available > 0; });
            }

            NetworkStream stream = _connection.GetStream();

            string recievedJson;
            byte[] buffer = new byte[_connection.ReceiveBufferSize];
            int length;

            length = stream.Read(buffer, 0, buffer.Length);

            recievedJson = Encoding.Unicode.GetString(buffer);

            string[] strBuffer = recievedJson.Trim('\0').Replace("}{", "}#{").Split('#');

            DataReceived.Invoke(strBuffer);
        }

        public string[] ReceiveFromServer()
        {
            if (_connection.Available == 0)
            {
                return null;
            }

            NetworkStream stream = _connection.GetStream();

            string receivedJson;
            byte[] buffer = new byte[_connection.ReceiveBufferSize];
            int length;

            length = stream.Read(buffer, 0, buffer.Length);

            receivedJson = Encoding.Unicode.GetString(buffer);

            string[] strBuffer = receivedJson.Trim('\0').Replace("}{", "}#{").Split('#');


            return strBuffer;

        }

        bool SendPlayerInfo()
        {

            return SendString(JsonUtility.ToJson(new ClientCommand(ClientCommandType.SetPlayerInfo, JsonUtility.ToJson(playerInfo))));
        }

        public bool SendString(string send)
        {
            if (_connection == null ||  !_connection.Connected || _connectionStream == null || !_connectionStream.CanWrite)
            {
                return false;
            }
            try
            {
                byte[] buffer = Encoding.Unicode.GetBytes(send);
                _connectionStream.Write(buffer,0,buffer.Length);
                //Console.WriteLine("Sending: "+send);
                return true;
            }catch(Exception e)
            {
                Console.WriteLine("Can't send data to server! Exception: "+e);
                return false;
            }
            
        }

        public void Update()
        {
            if (_loadedlevel > 1 && _scoreController != null)
            {
                _sendTimer += Time.deltaTime;
                if (_sendTimer > _sendRate)
                {
                    _sendTimer = 0;
                    SendPlayerInfo();
                }
            }
        }

        IEnumerator WaitForControllers()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Waiting for game controllers...");
            yield return new WaitUntil(delegate () { return FindObjectOfType<ScoreController>() != null; });

            Console.WriteLine("Controllers found!");

            _gameManager = Resources.FindObjectsOfTypeAll<GameplayManager>().First();

            if (_gameManager != null)
            {
                try
                {
                    if(ReflectionUtil.GetPrivateField<IPauseTrigger>(_gameManager, "_pauseTrigger") != null)
                    {
                        ReflectionUtil.GetPrivateField<IPauseTrigger>(_gameManager, "_pauseTrigger").SetCallback(delegate() { });
                    }

                    if (ReflectionUtil.GetPrivateField<VRPlatformHelper>(_gameManager, "_vrPlatformHelper") != null)
                    {
                    
                        ReflectionUtil.GetPrivateField<VRPlatformHelper>(_gameManager, "_vrPlatformHelper").inputFocusWasCapturedEvent -= _gameManager.HandleInputFocusWasCaptured;
                    
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            Console.WriteLine("Disabled pause button");

            _scoreController = FindObjectOfType<ScoreController>();

            if (_scoreController != null)
            {
                _scoreController.scoreDidChangeEvent += ScoreChanged;
                _scoreController.comboDidChangeEvent += ComboChanged;
            }


            
        }

        private void ComboChanged(int combo)
        {
            playerInfo.playerCombo = combo;
            if(combo > playerInfo.playerMaxCombo)
            {
                playerInfo.playerMaxCombo = combo;
            }
        }

        private void ScoreChanged(int score)
        {
            playerInfo.playerScore = score;
        }
    }
}
