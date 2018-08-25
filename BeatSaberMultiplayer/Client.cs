using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp.Server;

namespace BeatSaberMultiplayer
{
    public class Broadcast : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();
            Misc.Log.Info("WebSocket Client Connected!");
        }
    }

    public class StateObject
    {
        public Socket workSocket = null;
        public byte[] buffer;

        public StateObject(int BufferSize)
        {
            buffer = new byte[BufferSize];
        }
    }

    class Client : MonoBehaviour, IDisposable
    {
#if DEBUG
        public static FileStream packetWriter = File.Open("packetDump.dmp", FileMode.Append);
#endif
        public static event Action ClientCreated;
        public static event Action ClientDestroyed;
        public static Client instance;

        public event Action ConnectedToServerHub;
        public event Action<Exception> ServerHubException;

        public event Action<BasePacket> PacketReceived;

        public bool Connected;

        public WebSocketServer webSocket;
        Socket socket;

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
            if (socket != null && socket.Connected)
            {
                try
                {
                    socket.SendData(new BasePacket(CommandType.Disconnect, new byte[0]));
                    Thread.Sleep(150);
                    socket.Close();
                }
                catch (Exception)
                {

                }
            }

            if (!dontDestroy)
            {
                if (webSocket != null)
                {
                    webSocket.Stop();
                }
                instance = null;
                ClientDestroyed?.Invoke();
                Destroy(gameObject);
            }
        }

        public void Connect(string IP, int Port)
        {
            ip = IP;
            port = Port;

            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
            socket.BeginConnect(ip, port, new AsyncCallback(ConnectedCallback), null);
        }

        private void ConnectedCallback(IAsyncResult ar)
        {
            socket.EndConnect(ar);
            _lastPacketTime = DateTime.Now;

            List<byte> buffer = new List<byte>();
            buffer.AddRange(BitConverter.GetBytes(Plugin.pluginVersion));
            buffer.AddRange(playerInfo.ToBytes(false));
            socket.SendData(new BasePacket(CommandType.Connect, buffer.ToArray()));
            BasePacket response = socket.ReceiveData();

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

            StateObject state = new StateObject(4);
            state.workSocket = socket;

            socket.BeginReceive(state.buffer, 0, 4, SocketFlags.None, new AsyncCallback(ReceiveHeader), state);

            if (Config.Instance.EnableWebSocketServer && webSocket == null)
            {
                webSocket = new WebSocketServer(Config.Instance.WebSocketPort);
                webSocket.AddWebSocketService<Broadcast>("/");
                webSocket.Start();
                Log.Info($"WebSocket Server started at port {Config.Instance.WebSocketPort}");
            }
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

        public void ReceiveHeader(IAsyncResult ar)
        {

            StateObject recState = (StateObject)ar.AsyncState;
            Socket client = recState.workSocket;

            int bytesRead = client.EndReceive(ar);

            if (bytesRead == recState.buffer.Length)
            {
                int bytesToReceive = BitConverter.ToInt32(recState.buffer, 0);

                StateObject state = new StateObject(bytesToReceive) { workSocket = client};
                client.BeginReceive(state.buffer, 0, bytesToReceive, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            StateObject recState = (StateObject)ar.AsyncState;
            Socket client = recState.workSocket;
            
            int bytesRead = client.EndReceive(ar);
            
            if (bytesRead == recState.buffer.Length)
            {
                BasePacket packet = new BasePacket(recState.buffer);

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

                if (webSocket != null)
                {
                    webSocket.WebSocketServices["/"].Sessions.BroadcastAsync(packet.ToBytes(), null);
                }

                if (client.Connected)
                {
                    StateObject state = new StateObject(4) { workSocket = client };
                    client.BeginReceive(state.buffer, 0, 4, SocketFlags.None, new AsyncCallback(ReceiveHeader), state);
                }
            }
        }

        public void JoinRoom(uint roomId)
        {
            if (Connected && socket.Connected)
            {
#if DEBUG
                Log.Info("Joining room " + roomId);
#endif
                socket.SendData(new BasePacket(CommandType.JoinRoom, BitConverter.GetBytes(roomId)));
            }
        }

        public void JoinRoom(uint roomId, string password)
        {
            if (Connected && socket.Connected)
            {
#if DEBUG
                Log.Info("Joining room " + roomId+" with password "+password);
#endif

                List<byte> buffer = new List<byte>();
                buffer.AddRange(BitConverter.GetBytes(roomId));
                byte[] passBytes = Encoding.UTF8.GetBytes(password);
                buffer.AddRange(BitConverter.GetBytes(passBytes.Length));
                buffer.AddRange(passBytes);

                socket.SendData(new BasePacket(CommandType.JoinRoom, buffer.ToArray()));
            }
        }

        public void CreateRoom(RoomSettings settings)
        {
            if(Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.CreateRoom, settings.ToBytes(false)));
#if DEBUG
                Log.Info("Creating room...");
#endif
            }
        }

        public void LeaveRoom()
        {
            if (Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.LeaveRoom, new byte[0]));
#if DEBUG
                Log.Info("Leaving room...");
#endif
            }
            isHost = false;
            playerInfo.playerState = PlayerState.Lobby;
        }


        public void DestroyRoom()
        {
            if (Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.DestroyRoom, new byte[0]));
#if DEBUG
                Log.Info("Destroying room...");
#endif
            }
            isHost = false;
            playerInfo.playerState = PlayerState.Lobby;
        }

        public void RequestRoomInfo()
        {
            if (Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.GetRoomInfo, new byte[0]));
#if DEBUG
                Log.Info("Requested RoomInfo...");
#endif
            }
        }

        public void SetSelectedSong(SongInfo song)
        {
            if (Connected && socket.Connected)
            {
                if (song == null)
                {
                    socket.SendData(new BasePacket(CommandType.SetSelectedSong, new byte[0]));
                }
                else
                {
                    socket.SendData(new BasePacket(CommandType.SetSelectedSong, song.ToBytes(false)));
                }
#if DEBUG
                Log.Info("Sending SongInfo for selected song...");
#endif
            }
        }

        public void StartLevel(IStandardLevel song, LevelDifficulty difficulty)
        {
            if (Connected && socket.Connected)
            {
#if DEBUG
                Log.Info("Starting level...");
#endif
                List<byte> buffer = new List<byte>();
                buffer.Add((byte)difficulty);
                buffer.AddRange(new SongInfo() { songName = song.songName +" "+song.songSubName, levelId = song.levelID.Substring(0, Math.Min(32, song.levelID.Length)), songDuration = song.audioClip.length}.ToBytes(false));
                socket.SendData(new BasePacket(CommandType.StartLevel, buffer.ToArray()));
            }
        }

        public void SendPlayerInfo()
        {
            if (Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.UpdatePlayerInfo, playerInfo.ToBytes(false)));

#if DEBUG
                if (playerInfo.playerState == PlayerState.Game)
                {
                    byte[] packet = new BasePacket(CommandType.UpdatePlayerInfo, playerInfo.ToBytes(false)).ToBytes();
                    packetWriter.Write(packet, 0, packet.Length);
                    packetWriter.Flush();
                }
#endif
            }
        }

        public void SendPlayerReady(bool ready)
        {
            if (Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.PlayerReady, new byte[1] { (byte)(ready ? 1 : 0) }));
            }
        }

        public void Dispose()
        {
            socket.Dispose();
            _packetsQueue.Clear();
        }
    }
}
