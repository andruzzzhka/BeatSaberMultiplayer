using ServerHub.Data;
using ServerHub.Misc;
using ServerHub.Room;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        }

        public void InitializeClient()
        {
            BasePacket packet = tcpClient.ReceiveData(true);

            if (packet.commandType == CommandType.Connect)
            {
                uint version = BitConverter.ToUInt32(packet.additionalData, 0);

                uint serverVersion = ((uint)Assembly.GetEntryAssembly().GetName().Version.Major).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Minor).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Revision).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Build);

                if (version != serverVersion)
                {
                    KickClient("Plugin version mismatch:\nServer: " + serverVersion + "\nClient: " + version);
                }

                playerInfo = new PlayerInfo(packet.additionalData.Skip(4).ToArray());
                playerInfo.playerState = PlayerState.Lobby;
                tcpClient.SendData(new BasePacket(CommandType.Connect, new byte[0]));

                timeoutTimer = new Stopwatch();
                HighResolutionTimer.LoopTimer.Elapsed += ClientLoop;
            }
            else
            {
                throw new Exception("Wrong command received!");
            }
        }

        void ClientLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {

            if ((playerInfo.playerState == PlayerState.Room || playerInfo.playerState == PlayerState.Game) && !timeoutTimer.IsRunning)
                timeoutTimer.Start();

            if (timeoutTimer.ElapsedMilliseconds > 3000)
                KickClient();
            
            BasePacket packet = tcpClient.ReceiveData(true);

            if(packet == null)
            {
                DestroyClient();
                return;
            }

            switch (packet.commandType)
            {
                case CommandType.Disconnect:
                    {
                        DestroyClient();
                    }
                    break;
                case CommandType.UpdateServerInfo:
                    {
                        playerInfo = new PlayerInfo(packet.additionalData);
                        timeoutTimer.Restart();
                    }
                    break;
                case CommandType.JoinRoom:
                    {
                        uint roomId = BitConverter.ToUInt32(packet.additionalData, 0);
                        if (RoomsController.GetRoomsList().Any(x => x.roomId == roomId && x.usePassword))
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
                        tcpClient.SendData(new BasePacket(CommandType.GetRooms, RoomsController.GetRoomsListInBytes()));
                    }
                    break;
                case CommandType.CreateRoom:
                    {
                        uint roomId = RoomsController.CreateRoom(new RoomSettings(packet.additionalData), playerInfo);
                        tcpClient.SendData(new BasePacket(CommandType.CreateRoom, BitConverter.GetBytes(roomId)));
                    }
                    break;
            }

        }

        public void DestroyClient()
        {
            Logger.Instance.Warning("Client disconnected!");
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
                    tcpClient.SendData(new BasePacket(CommandType.Disconnect, new byte[0]));
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
                    tcpClient.SendData(new BasePacket(CommandType.Disconnect, buffer.ToArray()));
                }
                catch (Exception)
                {
                }
            }
            DestroyClient();
        }
    }
}
