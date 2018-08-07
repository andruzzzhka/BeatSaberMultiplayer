using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    class Client : MonoBehaviour
    {
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
            instance = this;
            DontDestroyOnLoad(this);
            playerInfo = new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID());
        }

        public void Disconnect(bool dontDestroy = false)
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                try
                {
                    tcpClient.SendData(new BasePacket(CommandType.Disconnect, new byte[0]));
                    tcpClient.Close();
                }
                catch (Exception)
                {

                }
            }
            if (!dontDestroy)
            {
                instance = null;
                Destroy(gameObject);
            }
        }

        public void Connect(string IP, int Port)
        {
            ip = IP;
            port = Port;

            tcpClient = new TcpClient();
            tcpClient.BeginConnect(ip, port, new AsyncCallback(ConnectedCallback), null);
        }

        private void ConnectedCallback(IAsyncResult ar)
        {
            tcpClient.EndConnect(ar);

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

       

        public void ReceiveData()
        {
            while (tcpClient.Connected && receiverThread.IsAlive)
            {
                Log.Info("Waiting for packet...");
                BasePacket packet = tcpClient.ReceiveData();
                Log.Info($"Received Packet: CommandType={packet.commandType}, DataLength={packet.additionalData.Length}");
                PacketReceived?.Invoke(packet);

                if(packet.commandType == CommandType.Disconnect)
                {
                    Disconnect();
                }
            }
        }

        public void JoinRoom(uint roomId)
        {
            if (Connected && tcpClient.Connected)
            {
                Log.Info("Joining room " + roomId);
                tcpClient.SendData(new BasePacket(CommandType.JoinRoom, BitConverter.GetBytes(roomId)));
            }
        }

        public void JoinRoom(uint roomId, string password)
        {
            if (Connected && tcpClient.Connected)
            {
                Log.Info("Joining room " + roomId+" with password "+password);

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
            }
        }

        public void LeaveRoom()
        {
            if (Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.LeaveRoom, new byte[0]));
            }

            playerInfo.playerState = PlayerState.Lobby;
        }

        public void RequestRoomInfo()
        {
            if (Connected && tcpClient.Connected)
            {
                tcpClient.SendData(new BasePacket(CommandType.GetRoomInfo, new byte[0]));
                Log.Info("Requested RoomInfo...");
            }
        }
    }
}
