using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.VOIP;
using BS_Utils.Gameplay;
using Lidgren.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer
{

    public enum CommandType : byte { Connect, Disconnect, GetRooms, CreateRoom, JoinRoom, GetRoomInfo, LeaveRoom, DestroyRoom, TransferHost, SetSelectedSong, StartLevel, UpdatePlayerInfo, PlayerReady, SetGameState, DisplayMessage, SendEventMessage, GetChannelInfo, JoinChannel, LeaveChannel, GetSongDuration, UpdateVoIPData, GetRandomSongInfo, SetLevelOptions }

    public class Client : MonoBehaviour
    {
#if DEBUG
        public static FileStream packetWriter;
        public static Queue<byte[]> packetsBuffer = new Queue<byte[]>();
        private static int packetsWrittenToCurrentDump;
        public static bool startNewDump;

        public static async void WritePackets()
        {

            byte[] data;
            while (true)
            {
                try
                {
                    if (startNewDump && packetWriter != null && packetsWrittenToCurrentDump > 0)
                    {
                        Plugin.log.Info("Closing old dump...");
                        await packetWriter.FlushAsync().ConfigureAwait(false);
                        packetWriter.Close();

                        Plugin.log.Info("Compressing old dump...");
                        using (FileStream zipStream = new FileStream(packetWriter.Name.Substring(0, packetWriter.Name.LastIndexOf('.')) + ".zip", FileMode.Create))
                        using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                        {
                            archive.CreateEntryFromFile(packetWriter.Name, Path.GetFileName(packetWriter.Name));
                        }
                        File.Delete(packetWriter.Name);
                        Plugin.log.Info("Compressed!");

                        packetWriter = null;
                        packetsWrittenToCurrentDump = 0;
                    }
                    startNewDump = false;

                    if (packetWriter == null)
                    {
                        Plugin.log.Info("Starting new dump...");
                        if (!Directory.Exists("MPDumps"))
                        {
                            Directory.CreateDirectory("MPDumps");
                        }

                        int dumpsCount = GetFiles(Path.Combine(Environment.CurrentDirectory, "MPDumps"), "*.mpdmp|*.zip", SearchOption.TopDirectoryOnly).Length;
                        string newDumpPath;

                        if (File.Exists($"MPDumps\\packetDump{dumpsCount - 1}.mpdmp") && new FileInfo($"MPDumps\\packetDump{dumpsCount - 1}.mpdmp").Length == 0)
                            newDumpPath = $"MPDumps\\packetDump{dumpsCount - 1}.mpdmp";
                        else
                            newDumpPath = $"MPDumps\\packetDump{dumpsCount}.mpdmp";

                        packetWriter = File.Open(newDumpPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    }

                    if (packetsBuffer.Count > 0 && !startNewDump)
                    {
                        int packets = packetsBuffer.Count;
                        while (packetsBuffer.Count > 0 && !startNewDump)
                        {
                            data = packetsBuffer.Dequeue();

                            await packetWriter.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                        }

                        await packetWriter.FlushAsync().ConfigureAwait(false);
                        packetsWrittenToCurrentDump += packets;
                        Plugin.log.Info(packets + " update packets are written to disk!");
                    }
                }catch(Exception e)
                {
                    Plugin.log.Error("Unable to write update packets to packet dump! Exception: "+e);
                }
                await Task.Delay(TimeSpan.FromSeconds(2.5)).ConfigureAwait(false);
            }
        }

        private static string[] GetFiles(string sourceFolder, string filters, System.IO.SearchOption searchOption)
        {
            return filters.Split('|').SelectMany(filter => System.IO.Directory.GetFiles(sourceFolder, filter, searchOption)).ToArray();
        }
#endif

        public static event Action<string, string> EventMessageReceived;
        public static event Action ClientJoinedRoom;
        public static event Action ClientLevelStarted;
        public static event Action ClientLeftRoom;
        public static bool disableScoreSubmission;
        
        public event Action ConnectedToServerHub;

        public event Action<NetIncomingMessage> MessageReceived;
        public event Action<NetIncomingMessage> PlayerInfoUpdateReceived;

        public bool Connected;
        public bool InRoom;
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

#if DEBUG
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            WritePackets();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#endif
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

            Plugin.log.Info($"Creating message...");
            NetOutgoingMessage outMsg = NetworkClient.CreateMessage();

            Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            byte[] version = new byte[4] { (byte)assemblyVersion.Major, (byte)assemblyVersion.Minor, (byte)assemblyVersion.Build, (byte)assemblyVersion.Revision };

            outMsg.Write(version);
            playerInfo = new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID());
            playerInfo.AddToMessage(outMsg);

            Plugin.log.Info($"Connecting to {ip}:{port} with player name \"{playerInfo.playerName}\" and ID {playerInfo.playerId}");

            InRadioMode = false;
            InRoom = false;

            NetworkClient.Connect(ip, port, outMsg);
        }

        public void Update()
        {
            if (NetworkClient.ReadMessages(_receivedMessages) > 0)
            {
                try
                {
                    if (_receivedMessages.Any(x => x.PeekByte() != (byte)CommandType.UpdateVoIPData))
                    {
                        float packetTime = (float)DateTime.UtcNow.Subtract(_lastPacketTime).TotalMilliseconds;
                        if (packetTime > 2f)
                        {
                            _averagePacketTimes.Add(packetTime);

                            if (_averagePacketTimes.Count > 150)
                                _averagePacketTimes.RemoveAt(0);

                            Tickrate = (float)Math.Round(1000f / _averagePacketTimes.Where(x => x > 2f).Average(), 2);
                        }
                        _lastPacketTime = DateTime.UtcNow;
                    }
                    
                    NetIncomingMessage lastUpdate = _receivedMessages.LastOrDefault(x => x.MessageType == NetIncomingMessageType.Data && x.PeekByte() == (byte)CommandType.UpdatePlayerInfo);
                    
                    if (lastUpdate != null)
                    {
                        foreach(NetIncomingMessage msg in _receivedMessages.Where(x => x.MessageType == NetIncomingMessageType.Data && x.PeekByte() == (byte)CommandType.UpdatePlayerInfo))
                        {
                            foreach (Action<NetIncomingMessage> nextDel in PlayerInfoUpdateReceived.GetInvocationList())
                            {
                                try
                                {
                                    msg.Position = 0;
                                    nextDel.Invoke(msg);
                                }
                                catch (Exception e)
                                {
                                    Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on update received event: {e}");
                                }
                            }
                        }

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
                                Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on message received event: {e}");
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
                                    else if(status == NetConnectionStatus.Disconnected && Connected)
                                    {

#if DEBUG
                                        Plugin.log.Info("Disconnecting...");
#endif
                                        Disconnect();

                                        foreach (Action<NetIncomingMessage> nextDel in MessageReceived.GetInvocationList())
                                        {
                                            try
                                            {
                                                nextDel.Invoke(null);
                                            }
                                            catch (Exception e)
                                            {
                                                Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on message received event: {e}");
                                            }
                                        }
                                    }
                                    else if(status == NetConnectionStatus.Disconnected && !Connected)
                                    {
                                        Plugin.log.Error("ServerHub refused connection! Reason: " + msg.ReadString());
                                    }
                                }
                                break;
                            case NetIncomingMessageType.Data:
                                {
                                    CommandType commandType = (CommandType)msg.PeekByte();

                                    if (commandType == CommandType.Disconnect)
                                    {
#if DEBUG
                                        Plugin.log.Info("Disconnecting...");
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
                                                Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on message received event: {e}");
                                            }
                                        }

                                        return;
                                    }
                                    else if (commandType == CommandType.SendEventMessage)
                                    {
                                        string header = msg.ReadString();
                                        string data = msg.ReadString();

#if DEBUG
                                        Plugin.log.Info($"Received event message! Header=\"{header}\", Data=\"{data}\"");
#endif
                                        foreach (Action<string, string> nextDel in EventMessageReceived.GetInvocationList())
                                        {
                                            try
                                            {
                                                nextDel.Invoke(header, data);
                                            }
                                            catch (Exception e)
                                            {
                                                Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on event message received event: {e}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (MessageReceived != null)
                                            foreach (Action<NetIncomingMessage> nextDel in MessageReceived.GetInvocationList())
                                            {
                                                try
                                                {
                                                    msg.Position = 0;
                                                    nextDel.Invoke(msg);
                                                }
                                                catch (Exception e)
                                                {
                                                    Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on message received event: {e}");
                                                }
                                            }
                                    }


                                    if (commandType == CommandType.JoinRoom)
                                    {
                                        msg.Position = 8;
                                        if (msg.PeekByte() == 0 && ClientJoinedRoom != null)
                                        {
                                            foreach (Action nextDel in ClientJoinedRoom.GetInvocationList())
                                            {
                                                try
                                                {
                                                    nextDel.Invoke();
                                                }
                                                catch (Exception e)
                                                {
                                                    Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on client joined room event: {e}");
                                                }
                                            }
                                        }
                                    }
                                    else if (commandType == CommandType.StartLevel)
                                    {
#if DEBUG
                                        startNewDump = true;
                                        packetsBuffer.Clear();
                                        msg.Position = 8;
                                        LevelOptionsInfo levelInfo = new LevelOptionsInfo(msg);
                                        SongInfo songInfo = new SongInfo(msg);
                                        List<byte> buffer = new List<byte>();
                                        buffer.AddRange(levelInfo.ToBytes());
                                        buffer.AddRange(HexConverter.ConvertHexToBytesX(songInfo.levelId));

                                        Plugin.log.Info("LevelID: " + songInfo.levelId + ", Bytes: " + BitConverter.ToString(buffer.ToArray()));

                                        packetsBuffer.Enqueue(buffer.ToArray());
                                        msg.Position = 0;                                        
#endif
                                        if (playerInfo.playerState == PlayerState.Room)
                                            foreach (Action nextDel in ClientLevelStarted.GetInvocationList())
                                            {
                                                try
                                                {
                                                    nextDel.Invoke();
                                                }
                                                catch (Exception e)
                                                {
                                                    Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on client level started event: {e}");
                                                }
                                            }
                                    }
                                };
                                break;

                            case NetIncomingMessageType.WarningMessage:
                                Plugin.log.Warn(msg.ReadString());
                                break;
                            case NetIncomingMessageType.ErrorMessage:
                                Plugin.log.Error(msg.ReadString());
                                break;
#if DEBUG
                            case NetIncomingMessageType.VerboseDebugMessage:
                            case NetIncomingMessageType.DebugMessage:
                                Plugin.log.Info(msg.ReadString());
                                break;
                            default:
                                Plugin.log.Info("Unhandled message type: " + msg.MessageType);
                                break;
#endif
                        }
                        NetworkClient.Recycle(msg);
                    }
                }catch(Exception e)
                {
#if DEBUG
                    Plugin.log.Critical($"Exception on message received event: {e}");
#endif
                }
                _receivedMessages.Clear();
            }

            if(Connected && NetworkClient.ConnectionsCount == 0)
            {

#if DEBUG
                Plugin.log.Info("Connection lost! Disconnecting...");
#endif
                Disconnect();

                foreach (Action<NetIncomingMessage> nextDel in MessageReceived.GetInvocationList())
                {
                    try
                    {
                        nextDel.Invoke(null);
                    }
                    catch (Exception e)
                    {
                        Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on message received event: {e}");
                    }
                }
            }
        }

        public void LateUpdate()
        {
            if(NetworkClient != null)
            {
                NetworkClient.FlushSendQueue();
            }
        }

        public void ClearMessageQueue()
        {
            while (NetworkClient.ReadMessages(_receivedMessages) > 0)
            {
                foreach (var message in _receivedMessages)
                {
                    NetworkClient.Recycle(message);
                }
                _receivedMessages.Clear();
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
                Plugin.log.Info("Joining room " + roomId);
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
                Plugin.log.Info("Joining room " + roomId + " with password \"" + password + "\"");
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
                Plugin.log.Info("Joining radio channel!");
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
                Plugin.log.Info("Creating room...");
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
                Plugin.log.Info("Leaving room...");
#endif
                HMMainThreadDispatcher.instance.Enqueue(() => { ClientLeftRoom?.Invoke(); });
                
            }
            isHost = false;
            InRoom = false;
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
                Plugin.log.Info("Destroying room...");
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
                Plugin.log.Info("Requested RoomInfo...");
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
                Plugin.log.Info("Requested ChannelInfo...");
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
                Plugin.log.Info("Sending SongInfo for selected song...");
#endif
            }
        }

        public void SetLevelOptions(LevelOptionsInfo info)
        {
            if (Connected && NetworkClient != null)
            {
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.SetLevelOptions);
                info.AddToMessage(outMsg);

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Plugin.log.Info("Updating level options...");
                Plugin.log.Info("Updating level options: Selected modifiers: " + string.Join(", ", Resources.FindObjectsOfTypeAll<GameplayModifiersModelSO>().First().GetModifierParams(info.modifiers).Select(x => x.modifierName)));

#endif
            }
        }

        public void StartLevel(BeatmapLevelSO song, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers)
        {
            if (Connected && NetworkClient != null)
            {
#if DEBUG
                Plugin.log.Info("Starting level...");
#endif
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write((byte)CommandType.StartLevel);

                new LevelOptionsInfo(difficulty, modifiers, characteristic.serializedName).AddToMessage(outMsg);
                SongInfo selectedSong = new SongInfo(song);
                selectedSong.songDuration = selectedSong.songDuration / modifiers.songSpeedMul;
                selectedSong.AddToMessage(outMsg);

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


#if DEBUG
                if (playerInfo.playerState == PlayerState.Game)
                {
                    List<byte> buffer = new List<byte>();

                    buffer.Add((byte)playerInfo.playerState);
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.playerScore));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.playerCutBlocks));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.playerComboBlocks));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.playerTotalBlocks));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.playerEnergy));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.playerProgress));


                    buffer.AddRange(BitConverter.GetBytes(playerInfo.rightHandPos.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.rightHandPos.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.rightHandPos.z));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.leftHandPos.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.leftHandPos.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.leftHandPos.z));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.headPos.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.headPos.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.headPos.z));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.rightHandRot.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.rightHandRot.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.rightHandRot.z));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.rightHandRot.w));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.leftHandRot.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.leftHandRot.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.leftHandRot.z));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.leftHandRot.w));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.headRot.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.headRot.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.headRot.z));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.headRot.w));

                    buffer.Add((byte)playerInfo.hitsLastUpdate.Count);

                    for (int i = 0; i < (byte)playerInfo.hitsLastUpdate.Count; i++)
                    {
                        byte[] hitData = new byte[5];

                        Buffer.BlockCopy(BitConverter.GetBytes(playerInfo.hitsLastUpdate[i].objectTime), 0, hitData, 0, 4);

                        BitArray bits = new BitArray(8);
                        bits[0] = playerInfo.hitsLastUpdate[i].noteWasCut;
                        bits[0] = playerInfo.hitsLastUpdate[i].isSaberA;
                        bits[0] = playerInfo.hitsLastUpdate[i].speedOK;
                        bits[0] = playerInfo.hitsLastUpdate[i].directionOK;
                        bits[0] = playerInfo.hitsLastUpdate[i].saberTypeOK;
                        bits[0] = playerInfo.hitsLastUpdate[i].wasCutTooSoon;
                        bits[0] = playerInfo.hitsLastUpdate[i].reserved1;
                        bits[0] = playerInfo.hitsLastUpdate[i].reserved2;

                        bits.CopyTo(hitData, 4);

                        buffer.AddRange(hitData);
                    }

                    buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

                    packetsBuffer.Enqueue(buffer.ToArray());
                }
#endif

                NetworkClient.SendMessage(outMsg, NetDeliveryMethod.UnreliableSequenced, 1);
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
                Plugin.log.Info("Requested ChannelInfo...");
#endif
            }
        }
    }
}
