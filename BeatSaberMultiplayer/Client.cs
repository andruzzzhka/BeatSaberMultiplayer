using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    class Client : MonoBehaviour, IDisposable
    {
        public static event Action ClientCreated;
        public static event Action ClientDestroyed;
        public static Client instance;

        public event Action ConnectedToServerHub;
        public event Action<Exception> ServerHubException;

        public event Action<BasePacket> PacketReceived;

        Thread receiverThread;

        public bool Connected;

        TcpClient tcpClient;

        public string ip;
        public int port;

        public PlayerInfo playerInfo;
        
        public bool isHost;

        Queue<BasePacket> _packetsQueue = new Queue<BasePacket>();

        DateTime _lastPacketTime;
        List<float> _averagePacketTimes = new List<float>();

        public float Tickrate;

        public static void CreateClient()
        {
            if(instance != null)
            {
                Log.Error("Client Instance already exists!");
                return;
            }

            new GameObject("MultiplayerClient").AddComponent<Client>();
        }

        public void Awake()
        {
            if (instance != this)
            {
                instance = this;
                DontDestroyOnLoad(this);
                playerInfo = new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID());
                ClientCreated?.Invoke();
            }
        }

        public void Disconnect(bool dontDestroy = false)
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                try
                {
                    tcpClient.SendData(new BasePacket(CommandType.Disconnect, new byte[0]));
                    Thread.Sleep(150);
                    tcpClient.Close();
                }
                catch (Exception)
                {

                }
            }

            if (!dontDestroy)
            {
                instance = null;
                ClientDestroyed?.Invoke();
                Destroy(gameObject);
            }
        }

        public void Connect(string IP, int Port)
        {
            ip = IP;
            port = Port;

            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            tcpClient.BeginConnect(ip, port, new AsyncCallback(ConnectedCallback), null);
        }

        private void ConnectedCallback(IAsyncResult ar)
        {
            tcpClient.EndConnect(ar);
            _lastPacketTime = DateTime.Now;

            List<byte> buffer = new List<byte>();
            buffer.AddRange(BitConverter.GetBytes(Plugin.pluginVersion));
            buffer.AddRange(playerInfo.ToBytes(false));
            tcpClient.SendData(new BasePacket(CommandType.Connect, buffer.ToArray()));
            BasePacket response = tcpClient.ReceiveData();

            if (response.commandType != CommandType.Connect)
            {
                if (response.commandType == CommandType.Disconnect)
                {
                    if (response.additionalData.Length > 0)
                    {
                        string reason = Encoding.UTF8.GetString(response.additionalData, 4, BitConverter.ToInt32(response.additionalData, 0));

                        ServerHubException?.Invoke(new Exception($"ServerHub refused connection! Reason: {reason}"));
                        return;
                    }
                    else
                    {
                        ServerHubException?.Invoke(new Exception("ServerHub refused connection!"));
                        return;
                    }
                }
                else
                {
                    ServerHubException?.Invoke(new Exception("Unexpected response from ServerHub!"));
                    return;
                }
            }

            playerInfo.playerState = PlayerState.Lobby;
            Connected = true;
            ConnectedToServerHub?.Invoke();
            
            receiverThread = new Thread(ReceiveData) { IsBackground = true };
            receiverThread.Start();
        }

        public void RemovePacketsFromQueue(CommandType type)
        {
            _packetsQueue = new Queue<BasePacket>(_packetsQueue.ToList().RemoveAll(x => x.commandType == type));
        }

        public void Update()
        {
            while (_packetsQueue.Count > 0)
            {
                BasePacket packet = _packetsQueue.Dequeue();

                PacketReceived?.Invoke(packet);

                if (packet.commandType == CommandType.Disconnect)
                {
#if DEBUG
                    Log.Info("Disconnecting...");
#endif
                    Disconnect();
                }
            }
        }

        public void ReceiveData()
        {
            while (tcpClient.Connected && receiverThread.IsAlive)
            {
                BasePacket packet = tcpClient.ReceiveData();
                if (_averagePacketTimes.Count > 119)
                    _averagePacketTimes.RemoveAt(0);
                _averagePacketTimes.Add((float)DateTime.Now.Subtract(_lastPacketTime).TotalMilliseconds);
                _lastPacketTime = DateTime.Now;

                Tickrate = 1000f / (_averagePacketTimes.Sum() / _averagePacketTimes.Count);
#if DEBUG
                if (packet.commandType != CommandType.UpdatePlayerInfo)
                    Log.Info($"Received Packet: CommandType={packet.commandType}, DataLength={packet.additionalData.Length}");
#endif
                _packetsQueue.Enqueue(packet);
            }
        }

        public void JoinRoom(uint roomId)
        {
            if (Connected && tcpClient.Connected)
            {
#if DEBUG
                Log.Info("Joining room " + roomId);
#endif
                tcpClient.SendData(new BasePacket(CommandType.JoinRoom, BitConverter.GetBytes(roomId)));
            }
        }

        public void JoinRoom(uint roomId, string password)
        {
            if (Connected && tcpClient.Connected)
            {
#if DEBUG
                Log.Info("Joining room " + roomId+" with password "+password);
#endif

                List<byte> buffer = new List<byte>();
                buffer.AddRange(BitConverter.GetBytes(roomId));
                byte[] passBytes = Encoding.UTF8.GetBytes(password);
                buffer.AddRange(BitConverter.GetBytes(passBytes.Length));
                buffer.AddRange(passBytes);

                tcpClient.SendData(new BasePacket(CommandType.JoinRoom, buffer.ToArray()));
            }
        }

        public void CreateRoom(RoomSettings settings)
        {
            if(Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.CreateRoom, settings.ToBytes(false)));
#if DEBUG
                Log.Info("Creating room...");
#endif
            }
        }

        public void LeaveRoom()
        {
            if (Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.LeaveRoom, new byte[0]));
#if DEBUG
                Log.Info("Leaving room...");
#endif
            }
            isHost = false;
            playerInfo.playerState = PlayerState.Lobby;
        }


        public void DestroyRoom()
        {
            if (Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.DestroyRoom, new byte[0]));
#if DEBUG
                Log.Info("Destroying room...");
#endif
            }
            isHost = false;
            playerInfo.playerState = PlayerState.Lobby;
        }

        public void RequestRoomInfo()
        {
            if (Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.GetRoomInfo, new byte[0]));
#if DEBUG
                Log.Info("Requested RoomInfo...");
#endif
            }
        }

        public void SetSelectedSong(SongInfo song)
        {
            if (Connected && tcpClient.Connected)
            {
                if (song == null)
                {
                    tcpClient.SendData(new BasePacket(CommandType.SetSelectedSong, new byte[0]));
                }
                else
                {
                    tcpClient.SendData(new BasePacket(CommandType.SetSelectedSong, song.ToBytes(false)));
                }
#if DEBUG
                Log.Info("Sending SongInfo for selected song...");
#endif
            }
        }

        public void StartLevel(IStandardLevel song, LevelDifficulty difficulty)
        {
            if (Connected && tcpClient.Connected)
            {
#if DEBUG
                Log.Info("Starting level...");
#endif
                List<byte> buffer = new List<byte>();
                buffer.Add((byte)difficulty);
                buffer.AddRange(new SongInfo() { songName = song.songName +" "+song.songSubName, levelId = song.levelID.Substring(0, Math.Min(32, song.levelID.Length)), songDuration = song.audioClip.length}.ToBytes(false));
                tcpClient.SendData(new BasePacket(CommandType.StartLevel, buffer.ToArray()));
            }
        }

        public void SendPlayerInfo()
        {
            if (Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.UpdatePlayerInfo, playerInfo.ToBytes(false)));
            }
        }

        public void SendPlayerReady(bool ready)
        {
            if (Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.PlayerReady, new byte[1] { (byte)(ready ? 1 : 0) }));
            }
        }

        public void Dispose()
        {
            receiverThread.Abort();
            tcpClient.Dispose();
            _packetsQueue.Clear();
        }
    }
}
