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
using UnityEngine.Scripting;

namespace BeatSaberMultiplayer
{

    public enum CommandType : byte { Connect, Disconnect, GetRooms, CreateRoom, JoinRoom, GetRoomInfo, LeaveRoom, DestroyRoom, TransferHost, SetSelectedSong, StartLevel, UpdatePlayerInfo, PlayerReady, SetGameState, DisplayMessage, SendEventMessage, GetChannelInfo, JoinChannel, LeaveChannel, GetSongDuration, UpdateVoIPData, GetRandomSongInfo, SetLevelOptions, GetPlayerUpdates }

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

        public bool connected;
        public bool inRoom;
        public bool inRadioMode;

        public NetClient networkClient;
        
        public string ip;
        public int port;

        public PlayerInfo playerInfo;

        public bool isHost;

        public float tickrate;

        private float _lastPacketTime;
        private float _averagePacketTime;
        private int _packets;
        
        private List<NetIncomingMessage> _receivedMessages = new List<NetIncomingMessage>();
        
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
            networkClient = new NetClient(Config);

#if DEBUG
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            WritePackets();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#endif
        }

        public void Disconnect()
        {
            if (networkClient != null && networkClient.Status == NetPeerStatus.Running)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.Disconnect);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                networkClient.FlushSendQueue();

                networkClient.Shutdown("");
            }
            connected = false;
        }

        public void Connect(string IP, int Port)
        {
            ip = IP;
            port = Port;

            networkClient.Start();

            Plugin.log.Info($"Creating message...");
            NetOutgoingMessage outMsg = networkClient.CreateMessage();

            Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            byte[] version = new byte[4] { (byte)assemblyVersion.Major, (byte)assemblyVersion.Minor, (byte)assemblyVersion.Build, (byte)assemblyVersion.Revision };

            outMsg.Write(version);
            playerInfo = new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID());
            playerInfo.AddToMessage(outMsg);

            Plugin.log.Info($"Connecting to {ip}:{port} with player name \"{playerInfo.playerName}\" and ID {playerInfo.playerId}");

            inRadioMode = false;
            inRoom = false;

            networkClient.Connect(ip, port, outMsg);
        }

        public void Update()
        {
            if (_receivedMessages.Count > 0)
            {
                networkClient.Recycle(_receivedMessages);
                _receivedMessages.Clear();
            }

            if (networkClient.ReadMessages(_receivedMessages) > 0)
            {
                for (int i = _receivedMessages.Count - 1; i >= 0; i--)
                {
                    if (_receivedMessages[i].MessageType == NetIncomingMessageType.Data && _receivedMessages[i].PeekByte() == (byte)CommandType.UpdatePlayerInfo)
                    {
                        if (_packets > 150)
                        {
                            _averagePacketTime = 0f;
                            _packets = 0;
                        }

                        if (Time.realtimeSinceStartup - _lastPacketTime < 0.2f)
                        {
                            _averagePacketTime += Time.realtimeSinceStartup - _lastPacketTime;
                            _packets++;

                            if (_averagePacketTime != 0f && _packets != 0)
                                tickrate = 1f / (_averagePacketTime / _packets);
                        }
                        else
                        {
                            _averagePacketTime = 0f;
                            _packets = 0;
                        }
                        _lastPacketTime = Time.realtimeSinceStartup;

                        MessageReceived?.Invoke(_receivedMessages[i]);
                        _receivedMessages[i].Position = 0;

                        break;
                    }
                }

                if (Config.Instance.SpectatorMode)
                {
                    for (int i = 0; i < _receivedMessages.Count; i++)
                    {
                        _receivedMessages[i].Position = 0;
                        if (_receivedMessages[i].MessageType == NetIncomingMessageType.Data && _receivedMessages[i].PeekByte() == (byte)CommandType.GetPlayerUpdates)
                        {
                            PlayerInfoUpdateReceived?.Invoke(_receivedMessages[i]);
                            _receivedMessages[i].Position = 0;
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

                                if (status == NetConnectionStatus.Connected && !connected)
                                {
                                    connected = true;
                                    ConnectedToServerHub?.Invoke();
                                }
                                else if(status == NetConnectionStatus.Disconnected && connected)
                                {

#if DEBUG
                                    Plugin.log.Info("Disconnecting...");
#endif
                                    Disconnect();

                                    MessageReceived?.Invoke(null);
                                }
                                else if(status == NetConnectionStatus.Disconnected && !connected)
                                {
                                    Plugin.log.Error("ServerHub refused connection! Reason: " + msg.ReadString());
                                }
                            }
                            break;
                        case NetIncomingMessageType.Data:
                            {
                                CommandType commandType = (CommandType)msg.PeekByte();

                                switch (commandType)
                                {
                                    case CommandType.Disconnect:
                                        {
#if DEBUG
                                            Plugin.log.Info("Disconnecting...");
#endif
                                            Disconnect();

                                            MessageReceived?.Invoke(msg);
                                        }
                                        break;
                                    case CommandType.SendEventMessage:
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
                                                    nextDel?.Invoke(header, data);
                                                }
                                                catch (Exception e)
                                                {
                                                    if (nextDel != null)
                                                        Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on event message received event: {e}");
                                                    else
                                                        Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on event message received event: {e}");
                                                }
                                            }
                                        }
                                        break;
                                    case CommandType.JoinRoom:
                                        {
                                            msg.Position = 8;
                                            if (msg.PeekByte() == 0 && ClientJoinedRoom != null)
                                            {
                                                foreach (Action nextDel in ClientJoinedRoom.GetInvocationList())
                                                {
                                                    try
                                                    {
                                                        nextDel?.Invoke();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        if (nextDel != null)
                                                            Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on client joined room event: {e}");
                                                        else
                                                            Plugin.log.Error($"Exception on client joined room event: {e}");
                                                    }
                                                }
                                            }

                                            MessageReceived?.Invoke(msg);
                                        }
                                        break;
                                    case CommandType.StartLevel:
                                        {
#if DEBUG
                                            startNewDump = true;
                                            packetsBuffer.Clear();
                                            msg.Position = 8;
                                            LevelOptionsInfo levelInfo = new LevelOptionsInfo(msg);
                                            SongInfo songInfo = new SongInfo(msg);
                                            List<byte> buffer = new List<byte>();
                                            buffer.AddRange(levelInfo.ToBytes());
                                            buffer.AddRange(HexConverter.ConvertHexToBytesX(songInfo.hash));

                                            Plugin.log.Info("LevelID: " + songInfo.levelId + ", Bytes: " + BitConverter.ToString(buffer.ToArray()));

                                            packetsBuffer.Enqueue(buffer.ToArray());
                                            msg.Position = 0;                                        
#endif
                                            if (playerInfo != null && playerInfo.updateInfo.playerState == PlayerState.Room && ClientLevelStarted != null)
                                            {
                                                foreach (Action nextDel in ClientLevelStarted.GetInvocationList())
                                                {
                                                    try
                                                    {
                                                        nextDel?.Invoke();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        if (nextDel != null)
                                                            Plugin.log.Error($"Exception in {nextDel.Target.GetType()}.{nextDel.Method.Name} on client level started event: {e}");
                                                        else
                                                            Plugin.log.Error($"Exception on client level started event: {e}");
                                                    }
                                                }
                                            }

                                            MessageReceived?.Invoke(msg);
                                        }
                                        break;
                                    case CommandType.UpdatePlayerInfo:
                                            break;

                                    default:
                                        {
                                            MessageReceived?.Invoke(msg);
                                        }
                                        break;
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
                }

                networkClient.Recycle(_receivedMessages);
                _receivedMessages.Clear();
            }

            if(connected && networkClient.ConnectionsCount == 0)
            {

#if DEBUG
                Plugin.log.Info("Connection lost! Disconnecting...");
#endif
                Disconnect();

                MessageReceived?.Invoke(null);
            }

            if((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F1))
            {
                GarbageCollector.GCMode = GarbageCollector.GCMode == GarbageCollector.Mode.Enabled ? GarbageCollector.Mode.Disabled : GarbageCollector.Mode.Enabled;
                Plugin.log.Notice($"Garbage collector {GarbageCollector.GCMode.ToString()}!");
            }
        }

        public void LateUpdate()
        {
            if(networkClient != null)
            {
                networkClient.FlushSendQueue();
            }
        }

        public void ClearMessageQueue()
        {
            while (networkClient.ReadMessages(_receivedMessages) > 0)
            {
                foreach (var message in _receivedMessages)
                {
                    networkClient.Recycle(message);
                }
                _receivedMessages.Clear();
            }
        }

        public void GetRooms()
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetRooms);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void JoinRoom(uint roomId)
        {
            if (connected && networkClient != null)
            {
#if DEBUG
                Plugin.log.Info("Joining room " + roomId);
#endif

                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.JoinRoom);
                outMsg.Write(roomId);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void JoinRoom(uint roomId, string password)
        {
            if (connected && networkClient != null)
            {
#if DEBUG
                Plugin.log.Info("Joining room " + roomId + " with password \"" + password + "\"");
#endif

                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.JoinRoom);
                outMsg.Write(roomId);
                outMsg.Write(password);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void JoinRadioChannel(int channelId)
        {
            if (connected && networkClient != null)
            {
#if DEBUG
                Plugin.log.Info("Joining radio channel!");
#endif

                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.JoinChannel);
                outMsg.Write(channelId);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void CreateRoom(RoomSettings settings)
        {
            if(connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.CreateRoom);
                settings.AddToMessage(outMsg);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                
#if DEBUG
                Plugin.log.Info("Creating room...");
#endif
            }
        }

        public void LeaveRoom()
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.LeaveRoom);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);

#if DEBUG
                Plugin.log.Info("Leaving room...");
#endif
                HMMainThreadDispatcher.instance.Enqueue(() => { ClientLeftRoom?.Invoke(); });
                
            }
            isHost = false;
            inRoom = false;
            playerInfo.updateInfo.playerState = PlayerState.Lobby;
        }


        public void DestroyRoom()
        {
            if (connected && networkClient != null)
            {

                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.DestroyRoom);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Plugin.log.Info("Destroying room...");
#endif
            }
            isHost = false;
            playerInfo.updateInfo.playerState = PlayerState.Lobby;
        }

        public void RequestRoomInfo()
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetRoomInfo);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Plugin.log.Info("Requested RoomInfo...");
#endif
            }
        }

        public void RequestChannelInfo(int channelId)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetChannelInfo);
                outMsg.Write(channelId);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Plugin.log.Info("Requested ChannelInfo...");
#endif
            }
        }

        public void TransferHost(PlayerInfo newHost)
        {
            if (connected && networkClient != null && isHost)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.TransferHost);
                newHost.AddToMessage(outMsg);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Plugin.log.Info("Transferred host to "+newHost.playerName);
#endif
            }
        }

        public void SetSelectedSong(SongInfo song)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                if (song == null)
                {
                    outMsg.Write((byte)CommandType.SetSelectedSong);
                    outMsg.Write((byte)0);

                    networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                }
                else
                {
                    outMsg.Write((byte)CommandType.SetSelectedSong);
                    song.AddToMessage(outMsg);

                    networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                }
#if DEBUG
                Plugin.log.Info("Sending SongInfo for selected song...");
#endif
            }
        }

        public void SetLevelOptions(LevelOptionsInfo info)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.SetLevelOptions);
                info.AddToMessage(outMsg);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Plugin.log.Info("Updating level options: Selected modifiers: " + string.Join(", ", Resources.FindObjectsOfTypeAll<GameplayModifiersModelSO>().First().GetModifierParams(info.modifiers.ToGameplayModifiers()).Select(x => x.localizedModifierName)));

#endif
            }
        }

        public void StartLevel(IPreviewBeatmapLevel song, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers)
        {
            if (connected && networkClient != null)
            {
#if DEBUG
                Plugin.log.Info("Starting level...");
#endif
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.StartLevel);

                new LevelOptionsInfo(difficulty, modifiers, characteristic.serializedName).AddToMessage(outMsg);
                SongInfo selectedSong = new SongInfo(song);
                selectedSong.songDuration = selectedSong.songDuration / modifiers.songSpeedMul;
                selectedSong.AddToMessage(outMsg);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void SendPlayerInfo(bool sendFullUpdate)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.UpdatePlayerInfo);
                outMsg.Write(sendFullUpdate ? (byte)1 : (byte)0);

#if DEBUG
                if (playerInfo.updateInfo.playerState == PlayerState.Game)
                {
                    List<byte> buffer = new List<byte>();

                    buffer.Add((byte)playerInfo.updateInfo.playerState);
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.playerScore));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.playerCutBlocks));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.playerComboBlocks));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.playerTotalBlocks));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.playerEnergy));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.playerProgress));


                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.rightHandPos.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.rightHandPos.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.rightHandPos.z));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.leftHandPos.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.leftHandPos.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.leftHandPos.z));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.headPos.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.headPos.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.headPos.z));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.rightHandRot.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.rightHandRot.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.rightHandRot.z));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.rightHandRot.w));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.leftHandRot.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.leftHandRot.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.leftHandRot.z));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.leftHandRot.w));

                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.headRot.x));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.headRot.y));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.headRot.z));
                    buffer.AddRange(BitConverter.GetBytes(playerInfo.updateInfo.headRot.w));

                    buffer.Add((byte)playerInfo.hitsLastUpdate.Count);

                    for (int i = 0; i < (byte)playerInfo.hitsLastUpdate.Count; i++)
                    {
                        byte[] hitData = new byte[5];

                        Buffer.BlockCopy(BitConverter.GetBytes(playerInfo.hitsLastUpdate[i].objectId), 0, hitData, 0, 4);

                        BitArray bits = new BitArray(8);
                        bits[0] = playerInfo.hitsLastUpdate[i].noteWasCut;
                        bits[1] = playerInfo.hitsLastUpdate[i].isSaberA;
                        bits[2] = playerInfo.hitsLastUpdate[i].speedOK;
                        bits[3] = playerInfo.hitsLastUpdate[i].directionOK;
                        bits[4] = playerInfo.hitsLastUpdate[i].saberTypeOK;
                        bits[5] = playerInfo.hitsLastUpdate[i].wasCutTooSoon;

                        bits.CopyTo(hitData, 4);

                        buffer.AddRange(hitData);
                    }

                    buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

                    packetsBuffer.Enqueue(buffer.ToArray());
                }
#endif
                if (sendFullUpdate)
                    playerInfo.AddToMessage(outMsg);
                else
                {
                    playerInfo.updateInfo.AddToMessage(outMsg);
                    outMsg.Write((byte)playerInfo.hitsLastUpdate.Count);

                    foreach(var hit in playerInfo.hitsLastUpdate)
                    {
                        hit.AddToMessage(outMsg);
                    }

                    playerInfo.hitsLastUpdate.Clear();
                }

                networkClient.SendMessage(outMsg, NetDeliveryMethod.UnreliableSequenced, 1);
            }
        }

        public void SendVoIPData(VoipFragment audio)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.UpdateVoIPData);
                audio.AddToMessage(outMsg);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.UnreliableSequenced, 2);
            }
        }

        public void SendPlayerReady(bool ready)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.PlayerReady);
                outMsg.Write(ready);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void SendEventMessage(string header, string data)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.SendEventMessage);
                outMsg.Write(header);
                outMsg.Write(data);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void SendSongDuration(SongInfo info)
        {
            if (connected && networkClient != null)
            {
                NetOutgoingMessage outMsg = networkClient.CreateMessage();
                outMsg.Write((byte)CommandType.GetSongDuration);
                info.AddToMessage(outMsg);

                networkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
#if DEBUG
                Plugin.log.Info("Requested ChannelInfo...");
#endif
            }
        }
    }
}
