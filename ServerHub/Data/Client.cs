using ServerHub.Data;
using ServerHub.Hub;
using ServerHub.Misc;
using ServerHub.Rooms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;

namespace ServerHub.Data
{
    class Client
    {
        Stopwatch timeoutTimer;

        public event Action<Client> clientDisconnected;
        public event Action<Client, uint, string> clientJoinedRoom;
        public event Action<Client> clientLeftRoom;

        public TcpClient tcpClient;
        public PlayerInfo playerInfo;

        public uint joinedRoomID;

        public Client(TcpClient tcp)
        {
            tcpClient = tcp;
            tcpClient.NoDelay = true;
        }

        public bool InitializeClient()
        {
            try
            {
                

                BasePacket packet = this.ReceiveData(true);

                if (packet.commandType == CommandType.Connect && packet.additionalData != null && packet.additionalData.Length > 0)
                {
                    uint version = BitConverter.ToUInt32(packet.additionalData, 0);

                    uint serverVersion = ((uint)Assembly.GetEntryAssembly().GetName().Version.Major).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Minor).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Build).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Revision);

                    if (version != serverVersion)
                    {
                        KickClient("Plugin version mismatch:\nServer: " + serverVersion + "\nClient: " + version);
                    }

                    playerInfo = new PlayerInfo(packet.additionalData.Skip(4).ToArray());

                    if (Settings.Instance.Access.WhitelistEnabled)
                    {
                        if (!IsWhitelisted())
                        {
                            List<byte> buffer = new List<byte>();

                            byte[] reasonText = Encoding.UTF8.GetBytes("You are not whitelisted on this ServerHub!");
                            buffer.AddRange(BitConverter.GetBytes(reasonText.Length));
                            buffer.AddRange(reasonText);

                            this.SendData(new BasePacket(CommandType.Disconnect, buffer.ToArray()));
                            Logger.Instance.Warning($"Client {playerInfo.playerName}({playerInfo.playerId})@{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address} is not whitelisted!");
                            return false;
                        }
                    }

                    if (IsBlacklisted())
                    {
                        List<byte> buffer = new List<byte>();

                        byte[] reasonText = Encoding.UTF8.GetBytes("You are banned on this ServerHub!");
                        buffer.AddRange(BitConverter.GetBytes(reasonText.Length));
                        buffer.AddRange(reasonText);

                        this.SendData(new BasePacket(CommandType.Disconnect, buffer.ToArray()));
                        Logger.Instance.Warning($"Client {playerInfo.playerName}({playerInfo.playerId})@{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address} is blacklisted!");
                        return false;
                    }

                    playerInfo.playerState = PlayerState.Lobby;
                    this.SendData(new BasePacket(CommandType.Connect, new byte[0]));

                    Logger.Instance.Log($"{playerInfo.playerName} connected!");

                    timeoutTimer = new Stopwatch();
                    HighResolutionTimer.LoopTimer.Elapsed += ClientLoop;

                    return true;
                }
                else
                {
                    throw new Exception("Wrong command received!");
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Warning($"Can't initialize client! Exception: {e}");
                return false;
            }
        }

        public bool IsBlacklisted()
        {
            return  Program.blacklistedIPs.Any(x => x.Contains(((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address)) ||
                    Program.blacklistedIDs.Contains(playerInfo.playerId) ||
                    Program.blacklistedNames.Contains(playerInfo.playerName);
        }

        public bool IsWhitelisted()
        {
            return  Program.whitelistedIPs.Any(x => x.Contains(((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address)) ||
                    Program.whitelistedIDs.Contains(playerInfo.playerId) ||
                    Program.whitelistedNames.Contains(playerInfo.playerName);
        }

        void ClientLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {

            if(tcpClient == null || !tcpClient.Connected)
            {
                DestroyClient();
            }

            if ((playerInfo.playerState == PlayerState.Room || playerInfo.playerState == PlayerState.Game))
            {
                if(!timeoutTimer.IsRunning)
                    timeoutTimer.Start();
            }
            else
            {
                timeoutTimer.Reset();
            }

            if (timeoutTimer.ElapsedMilliseconds > 6000)
            {
                Logger.Instance.Log($"Kicked {playerInfo.playerName} for inactivity");
                KickClient();
            }

            BasePacket packet = this.ReceiveData(false);
            
            while (packet != null)
            {
#if DEBUG
                if (packet.commandType != CommandType.UpdatePlayerInfo)
                    Logger.Instance.Log($"Received packet from client: type={packet.commandType}");
#endif
                switch (packet.commandType)
                {
                    case CommandType.Connect:
                        {
                            if (packet.additionalData == null || packet.additionalData.Length == 0 || !packet.additionalData.Any(x => x != 0))
                            {
                                DestroyClient();
                            }
                        }
                        break;
                    case CommandType.Disconnect:
                        {
                            DestroyClient();
                        }
                        break;
                    case CommandType.UpdatePlayerInfo:
                        {
                            playerInfo = new PlayerInfo(packet.additionalData);
                            timeoutTimer.Restart();
                        }
                        break;
                    case CommandType.JoinRoom:
                        {
                            uint roomId = BitConverter.ToUInt32(packet.additionalData, 0);
                            if (RoomsController.GetRoomInfosList().Any(x => x.roomId == roomId && x.usePassword))
                            {
                                clientJoinedRoom?.Invoke(this, roomId, Encoding.UTF8.GetString(packet.additionalData, 8, BitConverter.ToInt32(packet.additionalData, 4)));
                            }
                            else
                            {
                                clientJoinedRoom?.Invoke(this, roomId, "");
                            }
                        }
                        break;
                    case CommandType.LeaveRoom:
                        {
                            clientLeftRoom?.Invoke(this);
                            playerInfo.playerState = PlayerState.Lobby;
                        }
                        break;
                    case CommandType.GetRooms:
                        {
                            this.SendData(new BasePacket(CommandType.GetRooms, RoomsController.GetRoomsListInBytes()));
                        }
                        break;
                    case CommandType.CreateRoom:
                        {
                            uint roomId = RoomsController.CreateRoom(new RoomSettings(packet.additionalData), playerInfo);
                            this.SendData(new BasePacket(CommandType.CreateRoom, BitConverter.GetBytes(roomId)));
                        }
                        break;
                    case CommandType.GetRoomInfo:
                        {
#if DEBUG
                            Logger.Instance.Log("Client room: " + joinedRoomID);
#endif
                            if (joinedRoomID != 0)
                            {
                                Room joinedRoom = RoomsController.GetRoomsList().First(x => x.roomId == joinedRoomID);
                                List<byte> buffer = new List<byte>();
                                buffer.Add(1);
                                buffer.AddRange(joinedRoom.GetSongInfos());
                                buffer.AddRange(joinedRoom.GetRoomInfo().ToBytes());
                                this.SendData(new BasePacket(CommandType.GetRoomInfo, buffer.ToArray()));
                            }

                        }
                        break;
                    case CommandType.SetSelectedSong:
                        {
                            if (joinedRoomID != 0)
                            {
                                Room joinedRoom = RoomsController.GetRoomsList().First(x => x.roomId == joinedRoomID);
                                if (packet.additionalData == null || packet.additionalData.Length == 0)
                                {
                                    joinedRoom.SetSelectedSong(playerInfo, null);
                                }
                                else
                                {
                                    joinedRoom.SetSelectedSong(playerInfo, new SongInfo(packet.additionalData));
                                }
                            }
                        }
                        break;
                    case CommandType.StartLevel:
                        {
                            if (joinedRoomID != 0)
                            {
                                Room joinedRoom = RoomsController.GetRoomsList().First(x => x.roomId == joinedRoomID);
                                byte difficulty = packet.additionalData[0];
                                SongInfo song = new SongInfo(packet.additionalData.Skip(1).ToArray());
                                song.songDuration += 4f;
                                joinedRoom.StartLevel(playerInfo, difficulty, song);
                            }
                        }
                        break;
                    case CommandType.DestroyRoom:
                        {
                            if (joinedRoomID != 0)
                            {
                                RoomsController.DestroyRoom(playerInfo, joinedRoomID);
                            }
                        }
                        break;
                    case CommandType.TransferHost:
                        {
                            if (joinedRoomID != 0)
                            {
                                Room joinedRoom = RoomsController.GetRoomsList().First(x => x.roomId == joinedRoomID);
                                joinedRoom.TransferHost(playerInfo, new PlayerInfo(packet.additionalData));
                            }
                        }
                        break;
                    case CommandType.PlayerReady:
                        {
                            if (joinedRoomID != 0)
                            {
                                Room joinedRoom = RoomsController.GetRoomsList().First(x => x.roomId == joinedRoomID);
                                joinedRoom.ReadyStateChanged(playerInfo, (packet.additionalData[0] == 0) ? false : true);
                            }
                        }
                        break;
                }
                
                packet = this.ReceiveData(false);
            }

        }

        public void DestroyClient()
        {
            if (playerInfo != null)
            {
                Logger.Instance.Log($"{playerInfo.playerName} disconnected!");
            }
            else
            {
                Logger.Instance.Log($"Client disconnected!");
            }
            if (tcpClient != null)
            {
                tcpClient.Close();
            }
            HighResolutionTimer.LoopTimer.Elapsed -= ClientLoop;
            clientDisconnected.Invoke(this);
        }

        public void KickClient()
        {
            if (tcpClient != null)
            {
                try
                {
                    this.SendData(new BasePacket(CommandType.Disconnect, new byte[0]));
                }
                catch (Exception)
                {
                }
            }
            DestroyClient();
        }

        public void KickClient(string reason)
        {
            if (tcpClient != null)
            {
                try
                {
                    List<byte> buffer = new List<byte>();
                    byte[] stringBuf = Encoding.UTF8.GetBytes(reason);
                    buffer.AddRange(BitConverter.GetBytes(stringBuf.Length));
                    buffer.AddRange(stringBuf);
                    this.SendData(new BasePacket(CommandType.Disconnect, buffer.ToArray()));
                }
                catch (Exception)
                {
                }
            }
            DestroyClient();
        }
    }
}
