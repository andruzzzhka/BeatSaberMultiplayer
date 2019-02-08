using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.VOIP;
using BS_Utils.Gameplay;
using Lidgren.Network;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer
{

    public enum CommandType : byte { Connect, Disconnect, GetRooms, CreateRoom, JoinRoom, GetRoomInfo, LeaveRoom, DestroyRoom, TransferHost, SetSelectedSong, StartLevel, UpdatePlayerInfo, PlayerReady, SetGameState, DisplayMessage, SendEventMessage, GetChannelInfo, JoinChannel, LeaveChannel, GetSongDuration, UpdateVoIPData, GetRandomSongInfo }

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
        public static bool disableScoreSubmission;
        
        public event Action ConnectedToServerHub;

        public event Action<NetIncomingMessage> MessageReceived;

        public bool Connected;
        public bool InRadioMode;

        public NetClient NetworkClient;
        
        public string ip;
        public int port;

        public PlayerInfo playerInfo;
        
        public bool isHost;

        DateTime _lastPacketTime;
        List<float> _averagePacketTimes = new List<float>();
        
        private List<NetIncomingMessage> _receivedMessages = new List<NetIncomingMessage>();

        public float Tickrate;

        public static Client Instance {
            get
            {
                if (instance == null)
                   instance = new GameObject("MultiplayerClient").AddComponent<Client>();
                return instance;
            }
        }
        
        private static Client instance;
        
        public void Awake()
        {
            instance = this;
            DontDestroyOnLoad(this);
            GetUserInfo.UpdateUserInfo();
            playerInfo = new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID());
            NetPeerConfiguration Config = new NetPeerConfiguration("BeatSaberMultiplayer") { MaximumHandshakeAttempts = 2, AutoFlushSendQueue = false };
            NetworkClient = new NetClient(Config);
            ClientCreated?.Invoke();
        }

        public void Disconnect()
        {
            if (NetworkClient != null && NetworkClient.Status == NetPeerStatus.Running)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.Disconnect);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                NetworkClient.FlushSendQueue();

                NetworkClient.Shutdown("");
            }
            Connected = false;
        }

        public void Connect(string IP, int Port)
        {
            ip = IP;
            port = Port;

            NetworkClient.Start();

            Misc.Logger.Info($"Creating message...");
            NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
            outMsg.Write(Plugin.pluginVersion);
            playerInfo = new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID());
            playerInfo.AddToMessage(outMsg);

            Misc.Logger.Info($"Connecting to {ip}:{port} with player name \"{playerInfo.playerName}\" and ID {playerInfo.playerId}");

            NetworkClient.Connect(ip, port, outMsg);
        }

        public void Update()
        {
            if (NetworkClient.ReadMessages(_receivedMessages) > 0)
            {
                try
                {
                    float packetTime = (float)DateTime.UtcNow.Subtract(_lastPacketTime).TotalMilliseconds;
                    if (packetTime > 2f)
                    {
                        _averagePacketTimes.Add(packetTime);

                        if (_averagePacketTimes.Count > 300)
                            _averagePacketTimes.RemoveAt(0);

                        Tickrate = (float)Math.Round(1000f / _averagePacketTimes.Average(), 2);
                    }
                    _lastPacketTime = DateTime.UtcNow;

                    NetIncomingMessage lastUpdate = _receivedMessages.LastOrDefault(x => x.MessageType == NetIncomingMessageType.Data && x.PeekByte() == (byte)CommandType.UpdatePlayerInfo);

                    if (lastUpdate != null)
                    {
                        _receivedMessages.RemoveAll(x => x.MessageType == NetIncomingMessageType.Data && x.PeekByte() == (byte)CommandType.UpdatePlayerInfo);

                        foreach (Action<NetIncomingMessage> nextDel in MessageReceived.GetInvocationList())
                        {
                            try
                            {
                                lastUpdate.Position = 0;
                                nextDel.Invoke(lastUpdate);
                            }
                            catch (Exception e)
                            {
                                Misc.Logger.Error($"Exception in {nextDel.Method.Name} on message received event: {e}");
                            }
                        }
                    }

                    foreach (NetIncomingMessage msg in _receivedMessages)
                    {
                        switch (msg.MessageType)
                        {
                            case NetIncomingMessageType.StatusChanged:
                                {
                                    NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();

                                    if (status == NetConnectionStatus.Connected && !Connected)
                                    {
                                        Connected = true;
                                        ConnectedToServerHub?.Invoke();
                                    }
                                    else
                                    {
                                        Misc.Logger.Info("New connection state: " + status);
                                    }
                                }
                                break;
                            case NetIncomingMessageType.Data:
                                {
                                    CommandType commandType = (CommandType)msg.PeekByte();

                                    if (commandType == CommandType.Disconnect)
                                    {
#if DEBUG
                                        Misc.Logger.Info("Disconnecting...");
#endif
                                        Disconnect();

                                        foreach (Action<NetIncomingMessage> nextDel in MessageReceived.GetInvocationList())
                                        {
                                            try
                                            {
                                                msg.Position = 0;
                                                nextDel.Invoke(msg);
                                            }
                                            catch (Exception e)
                                            {
                                                Misc.Logger.Error($"Exception in {nextDel.Method.Name} on message received event: {e}");
                                            }
                                        }

                                        return;
                                    }
                                    else if (commandType == CommandType.SendEventMessage)
                                    {
                                        string header = msg.ReadString();
                                        string data = msg.ReadString();

#if DEBUG
                                        Misc.Logger.Info($"Received event message! Header=\"{header}\", Data=\"{data}\"");
#endif
                                        EventMessageReceived?.Invoke(header, data);
                                    }
                                    else
                                    {
                                        foreach (Action<NetIncomingMessage> nextDel in MessageReceived.GetInvocationList())
                                        {
                                            try
                                            {
                                                msg.Position = 0;
                                                nextDel.Invoke(msg);
                                            }
                                            catch (Exception e)
                                            {
                                                Misc.Logger.Error($"Exception in {nextDel.Method.Name} on message received event: {e}");
                                            }
                                        }
                                    }


                                    if (commandType == CommandType.JoinRoom)
                                    {
                                        msg.Position = 8;
                                        if (msg.PeekByte() == 0)
                                            ClientJoinedRoom?.Invoke();
                                    }
                                    else if (commandType == CommandType.StartLevel)
                                    {
                                        if (playerInfo.playerState == PlayerState.Room)
                                            ClientLevelStarted?.Invoke();
                                    }
                                };
                                break;

                            case NetIncomingMessageType.WarningMessage:
                                Misc.Logger.Warning(msg.ReadString());
                                break;
                            case NetIncomingMessageType.ErrorMessage:
                                Misc.Logger.Error(msg.ReadString());
                                break;
#if DEBUG
                            case NetIncomingMessageType.VerboseDebugMessage:
                            case NetIncomingMessageType.DebugMessage:
                                Misc.Logger.Info(msg.ReadString());
                                break;
                            default:
                                Misc.Logger.Info("Unhandled message type: " + msg.MessageType);
                                break;
#endif
                        }
                        NetworkClient.Recycle(msg);
                    }
                }catch(Exception e)
                {
#if DEBUG
                    Misc.Logger.Exception($"Exception on message received event: {e}");
#endif
                }
                _receivedMessages.Clear();
            }
        }

        public void LateUpdate()
        {
            if(NetworkClient != null)
            {
                NetworkClient.FlushSendQueue();
            }
        }

        public void GetRooms()
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetRooms);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void JoinRoom(uint roomId)
        {
            if (Connected && NetworkClient != null)
            {
#if DEBUG
                Misc.Logger.Info("Joining room " + roomId);
#endif

                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.JoinRoom);
                outMsg.Write(roomId);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void JoinRoom(uint roomId, string password)
        {
            if (Connected && NetworkClient != null)
            {
#if DEBUG
                Misc.Logger.Info("Joining room " + roomId + " with password \"" + password + "\"");
#endif

                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.JoinRoom);
                outMsg.Write(roomId);
                outMsg.Write(password);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void JoinRadioChannel(int channelId)
        {
            if (Connected && NetworkClient != null)
            {
#if DEBUG
                Misc.Logger.Info("Joining radio channel!");
#endif

                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.JoinChannel);
                outMsg.Write(channelId);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void CreateRoom(RoomSettings settings)
        {
            if(Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.CreateRoom);
                settings.AddToMessage(outMsg);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                
#if DEBUG
                Misc.Logger.Info("Creating room...");
#endif
            }
        }

        public void LeaveRoom()
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.LeaveRoom);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);

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
            if (Connected && NetworkClient != null)
            {

                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.DestroyRoom);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Misc.Logger.Info("Destroying room...");
#endif
            }
            isHost = false;
            playerInfo.playerState = PlayerState.Lobby;
        }

        public void RequestRoomInfo()
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetRoomInfo);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Misc.Logger.Info("Requested RoomInfo...");
#endif
            }
        }

        public void RequestChannelInfo(int channelId)
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetChannelInfo);
                outMsg.Write(channelId);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Misc.Logger.Info("Requested ChannelInfo...");
#endif
            }
        }

        public void SetSelectedSong(SongInfo song)
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                if (song == null)
                {
                    outMsg.Write((byte)CommandType.SetSelectedSong);
                    outMsg.Write((byte)0);

                    NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                }
                else
                {
                    outMsg.Write((byte)CommandType.SetSelectedSong);
                    song.AddToMessage(outMsg);

                    NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                }
#if DEBUG
                Misc.Logger.Info("Sending SongInfo for selected song...");
#endif
            }
        }
        
        public void StartLevel(LevelSO song, BeatmapDifficulty difficulty)
        {
            if (Connected && NetworkClient != null)
            {
#if DEBUG
                Misc.Logger.Info("Starting level...");
#endif
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.StartLevel);
                outMsg.Write((byte)difficulty);
                new SongInfo() { songName = song.songName + " " + song.songSubName, levelId = song.levelID.Substring(0, Math.Min(32, song.levelID.Length)), songDuration = song.audioClip.length }.AddToMessage(outMsg);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void SendPlayerInfo()
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.UpdatePlayerInfo);
                playerInfo.AddToMessage(outMsg);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.UnreliableSequenced, 1);

                /*
                #if DEBUG
                                if (playerInfo.playerState == PlayerState.Game)
                                {
                                    byte[] packet = new BasePacket(CommandType.UpdatePlayerInfo, playerInfo.ToBytes(false)).ToBytes();
                                    packetWriter.Write(packet, 0, packet.Length);
                                    packetWriter.Flush();
                                }
                #endif
                */
            }
        }

        public void SendVoIPData(VoipFragment audio)
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.UpdateVoIPData);
                audio.AddToMessage(outMsg);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.UnreliableSequenced, 2);
            }
        }

        public void SendPlayerReady(bool ready)
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.PlayerReady);
                outMsg.Write(ready);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void SendEventMessage(string header, string data)
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.SendEventMessage);
                outMsg.Write(header);
                outMsg.Write(data);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void SendSongDuration(SongInfo info)
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetSongDuration);
                info.AddToMessage(outMsg);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Misc.Logger.Info("Requested ChannelInfo...");
#endif
            }
        }
    }
}
