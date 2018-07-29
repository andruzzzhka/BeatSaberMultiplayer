using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ServerHub.Data
{
    enum ClientState { Disconnected, Lobby, Room, Game }

    class Client
    {
        Thread clientLoopThread;

        public event Action<Client> clientDisconnected;
        public event Action<Client, int> clientJoinedRoom;
        public event Action<Client> clientJoinedLobby;

        public TcpClient tcpClient;

        public ClientState state;
        public PlayerInfo playerInfo;

        public Client(TcpClient tcp)
        {
            tcpClient = tcp;
            BasePacket packet = tcpClient.ReceiveData(true);

            if(packet.commandType == CommandType.Connect)
            {
                uint version = BitConverter.ToUInt32(packet.additionalData,0);

                uint serverVersion = ((uint)Assembly.GetEntryAssembly().GetName().Version.Major).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Minor).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Revision).ConcatUInts((uint)Assembly.GetEntryAssembly().GetName().Version.Build);

                if (version != serverVersion)
                {
                    KickClient("Plugin version mismatch:\nServer: " + serverVersion + "\nClient: " + version);
                }

                playerInfo = new PlayerInfo(packet.additionalData.Skip(4).ToArray());
                state = ClientState.Lobby;
                tcpClient.SendData(new BasePacket(CommandType.Connect, new byte[0]));

                clientLoopThread = new Thread(ClientLoop);
                clientLoopThread.Start();
            }
            else
            {
                throw new Exception("Wrong command received!");
            }

        }

        void ClientLoop()
        {
            Stopwatch timeoutTimer = new Stopwatch();

            while (clientLoopThread.IsAlive)
            {
                if ((state == ClientState.Room || state == ClientState.Game) && !timeoutTimer.IsRunning)
                    timeoutTimer.Start();

                if(timeoutTimer.ElapsedMilliseconds > 3000)
                    KickClient();


                BasePacket packet = tcpClient.ReceiveData(true);

                switch (packet.commandType)
                {
                    case CommandType.Disconnect:
                        {
                            DestroyClient();
                        }break;
                    case CommandType.UpdatePlayerInfo:
                        {
                            playerInfo = new PlayerInfo(packet.additionalData);
                            timeoutTimer.Restart();
                        }
                        break;
                    case CommandType.JoinRoom:
                        {
                            clientJoinedRoom.Invoke(this, BitConverter.ToInt32(packet.additionalData, 0));
                            state = ClientState.Room;
                        }break;
                    case CommandType.LeaveRoom:
                        {
                            clientJoinedLobby.Invoke(this);
                            state = ClientState.Lobby;
                        }break;
                }


            }

        }

        public void DestroyClient()
        {
            if (tcpClient != null)
            {
                tcpClient.Close();
            }
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
