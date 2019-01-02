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

namespace BeatSaberMultiplayer
{

    public class StateObject
    {
        public Socket workSocket = null;
        public BasePacket packet;
        public int offset;
        public byte[] buffer;

        public StateObject(int BufferSize)
        {
            buffer = new byte[BufferSize];
        }
    }

    public class Client : MonoBehaviour
    {
#if DEBUG
        public static FileStream packetWriter = File.Open("packetDump.dmp", FileMode.Append);
#endif
        public static event Action<string, string> EventMessageReceived;
        public static event Action ClientCreated;
        public static event Action ClientJoinedRoom;
        public static event Action ClientLevelStarted;
        public static event Action ClientLeftRoom;
        public static event Action ClientDestroyed;
        public static Client instance;

        public event Action ConnectedToServerHub;
        public event Action<Exception> ServerHubException;

        public event Action<BasePacket> PacketReceived;

        public bool Connected;

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
                Misc.Logger.Error("Client Instance already exists!");
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
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close(5);
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
        }

        public void RemovePacketsFromQueue(CommandType type)
        {
            try
            {
                _packetsQueue = new Queue<BasePacket>(_packetsQueue.ToList().RemoveAll(x => x.commandType == type));
            }
            catch(Exception e)
            {
                Misc.Logger.Warning("Unable to remove "+type.ToString()+" packets from queue! Exception: "+e);
            }
        }

        public void Update()
        {
            while (_packetsQueue.Count > 0)
            {
                BasePacket packet = _packetsQueue.Dequeue();
                
                if (packet.commandType == CommandType.Disconnect)
                {
#if DEBUG
                    Misc.Logger.Info("Disconnecting...");
#endif
                    Disconnect();
                    PacketReceived?.Invoke(packet);
                }
                else if (packet.commandType == CommandType.SendEventMessage)
                {
                    int headerLength = BitConverter.ToInt32(packet.additionalData, 0);
                    string header = Encoding.UTF8.GetString(packet.additionalData.Skip(4).Take(headerLength).ToArray());
                    string data = Encoding.UTF8.GetString(packet.additionalData.Skip(4+headerLength).ToArray());

#if DEBUG
                    Misc.Logger.Info($"Received event message! Header=\"{header}\", Data=\"{data}\"");
#endif
                    EventMessageReceived?.Invoke(header, data);
                }
                else
                {
                    PacketReceived?.Invoke(packet);
                }

                if(packet.commandType == CommandType.JoinRoom)
                {
                    if(packet.additionalData[0] == 0)
                        ClientJoinedRoom?.Invoke();
                }else if(packet.commandType == CommandType.StartLevel)
                {
                    if (playerInfo.playerState == PlayerState.Room)
                        ClientLevelStarted?.Invoke();
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
            
            if (bytesRead == recState.buffer.Length-recState.offset)
            {
                try
                {
                    BasePacket packet = new BasePacket(recState.buffer);

                    if (_averagePacketTimes.Count > 119)
                        _averagePacketTimes.RemoveAt(0);
                    _averagePacketTimes.Add((float)DateTime.Now.Subtract(_lastPacketTime).TotalMilliseconds);
                    _lastPacketTime = DateTime.Now;

                    Tickrate = 1000f / (_averagePacketTimes.Sum() / _averagePacketTimes.Count);
#if DEBUG
                    if (packet.commandType != CommandType.UpdatePlayerInfo)
                        Misc.Logger.Info($"Received Packet: CommandType={packet.commandType}, DataLength={packet.additionalData.Length}");
#endif
                    _packetsQueue.Enqueue(packet);
                }catch(Exception e)
                {
                    Misc.Logger.Warning("Unable to parse packet from ServerHub! Exception: "+ e);
                }

                if (client.Connected)
                {
                    StateObject state = new StateObject(4) { workSocket = client };
                    client.BeginReceive(state.buffer, 0, 4, SocketFlags.None, new AsyncCallback(ReceiveHeader), state);
                }
            }
            else if (bytesRead < recState.buffer.Length - recState.offset)
            {
                recState.offset += bytesRead;
                client.BeginReceive(recState.buffer, recState.offset, recState.buffer.Length-recState.offset, SocketFlags.None, new AsyncCallback(ReceiveCallback), recState);
            }
        }

        public void JoinRoom(uint roomId)
        {
            if (Connected && socket.Connected)
            {
#if DEBUG
                Misc.Logger.Info("Joining room " + roomId);
#endif
                socket.SendData(new BasePacket(CommandType.JoinRoom, BitConverter.GetBytes(roomId)));
            }
        }

        public void JoinRoom(uint roomId, string password)
        {
            if (Connected && socket.Connected)
            {
#if DEBUG
                Misc.Logger.Info("Joining room " + roomId+" with password "+password);
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
                Misc.Logger.Info("Creating room...");
#endif
            }
        }

        public void LeaveRoom()
        {
            if (Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.LeaveRoom, new byte[0]));
#if DEBUG
                Misc.Logger.Info("Leaving room...");
#endif
                HMMainThreadDispatcher.instance.Enqueue(() => { ClientLeftRoom?.Invoke(); });
                
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
                Misc.Logger.Info("Destroying room...");
#endif
            }
            isHost = false;
            playerInfo.playerState = PlayerState.Lobby;
        }

        public void RequestRoomInfo(bool requestSongList = true)
        {
            if (Connected && socket.Connected)
            {
                socket.SendData(new BasePacket(CommandType.GetRoomInfo, new byte[1] { (byte)(requestSongList ? 1 : 0) }));
#if DEBUG
                Misc.Logger.Info("Requested RoomInfo...");
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
                Misc.Logger.Info("Sending SongInfo for selected song...");
#endif
            }
        }
        
        public void StartLevel(LevelSO song, BeatmapDifficulty difficulty)
        {
            if (Connected && socket.Connected)
            {
#if DEBUG
                Misc.Logger.Info("Starting level...");
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

        public void SendEventMessage(string header, string data)
        {
            if (Connected && socket.Connected)
            {
                List<byte> buffer = new List<byte>();

                byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);

                buffer.AddRange(BitConverter.GetBytes(headerBytes.Length));
                buffer.AddRange(headerBytes);
                buffer.AddRange(dataBytes);

                socket.SendData(new BasePacket(CommandType.SendEventMessage, buffer.ToArray()));
            }
        }

        public void OnDestroy()
        {
            socket.Dispose();
            _packetsQueue.Clear();
        }
    }
}
