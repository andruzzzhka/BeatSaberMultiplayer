using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class ServerHubFlowCoordinator : FlowCoordinator
    {
        public MainMenuViewController mainMenuViewController;
        ServerHubNavigationController _serverHubNavigationController;

        List<ServerHubClient> _serverHubClients = new List<ServerHubClient>();

        public void OnlineButtonPressed()
        {
            if (mainMenuViewController == null)
                return;
            
            AvatarController.LoadAvatar();
            PresentServerHubUI();

            UpdateRoomsList();

        }

        private void PresentServerHubUI(bool immediately = false)
        {
            if (_serverHubNavigationController == null)
            {
                _serverHubNavigationController = BeatSaberUI.CreateViewController<ServerHubNavigationController>();
            }

            mainMenuViewController.PresentModalViewController(_serverHubNavigationController, null, immediately);
            _serverHubNavigationController.PushViewController(_serverHubNavigationController.roomListViewController, true);
            _serverHubNavigationController.roomListViewController.createRoomButtonPressed -= CreateRoomPressed;
            _serverHubNavigationController.roomListViewController.selectedRoom -= RoomSelected;
            _serverHubNavigationController.roomListViewController.createRoomButtonPressed += CreateRoomPressed;
            _serverHubNavigationController.roomListViewController.selectedRoom += RoomSelected;
        }

        private void CreateRoomPressed()
        {
            PluginUI.instance.roomCreationFlowCoordinator.MainCreateRoomButtonPressed(_serverHubNavigationController, _serverHubClients.Where(x => x.serverHubAvailable).ToList());
        }

        private void RoomSelected(RoomInfo selectedRoom)
        {
            foreach(ServerHubClient client in _serverHubClients.Where(x => x.serverHubAvailable))
            {
                int roomIndex = client.availableRooms.IndexOf(selectedRoom);
                if (roomIndex >= 0)
                {
                    JoinRoom(client.ip, client.port, selectedRoom.roomId, selectedRoom.usePassword);
                    break;
                }
            }
        }

        public void JoinRoom(string ip, int port, uint roomId, bool usePassword, string pass = "")
        {
            PluginUI.instance.roomFlowCoordinator.JoinRoom(_serverHubNavigationController, ip, port, roomId, usePassword, pass);
        }

        public void ReturnToRoom()
        {
            PresentServerHubUI(true);
            PluginUI.instance.roomFlowCoordinator.ReturnToRoom(_serverHubNavigationController);
        }

        public void UpdateRoomsList()
        {
#if DEBUG
            Log.Info("Updating rooms list...");
#endif
            _serverHubClients.ForEach(x =>
            {
                x.Abort();
                x.ReceivedRoomsList -= ReceivedRoomsList;
                x.ServerHubException -= ServerHubException;
            });
            _serverHubClients.Clear();

            for (int i = 0; i < Config.Instance.ServerHubIPs.Length; i++)
            {
                string ip = Config.Instance.ServerHubIPs[i];
                int port = 3700;
                if (Config.Instance.ServerHubPorts.Length > i)
                {
                    port = Config.Instance.ServerHubPorts[i];
                }
                ServerHubClient client = new ServerHubClient(ip, port);
                client.ReceivedRoomsList += ReceivedRoomsList;
                client.ServerHubException += ServerHubException;
                _serverHubClients.Add(client);
            }

            _serverHubNavigationController.roomListViewController.SetRooms(null);
            _serverHubNavigationController.SetLoadingState(true);
            _serverHubClients.ForEach(x => x.GetRooms());
#if DEBUG
            Log.Info("Requested rooms lists from ServerHubs...");
#endif
        }

        private void ReceivedRoomsList(ServerHubClient sender, List<RoomInfo> rooms)
        {
#if DEBUG
            Log.Info($"Received {rooms.Count} rooms from {sender.ip}:{sender.port}");
#endif
            _serverHubNavigationController.roomListViewController.SetRooms(_serverHubClients);
            _serverHubNavigationController.SetLoadingState(false);
        }

        public void ServerHubException(ServerHubClient sender, Exception e)
        {
            Log.Error($"ServerHub exception ({sender.ip}:{sender.port}): {e}");
        }

    }



    class ServerHubClient
    {
        private TcpClient tcpClient;
        
        public string ip;
        public int port;

        public bool serverHubAvailable;

        public List<RoomInfo> availableRooms = new List<RoomInfo>();

        public event Action<ServerHubClient, List<RoomInfo>> ReceivedRoomsList;
        public event Action<ServerHubClient, Exception> ServerHubException;

        public ServerHubClient(string IP, int Port)
        {
            ip = IP;
            port = Port;
            tcpClient = new TcpClient();
        }

        public void GetRooms()
        {
            try
            {
                tcpClient.BeginConnect(ip, port, new AsyncCallback(ConnectedToServerHub), null);
            }catch(Exception e)
            {
                ServerHubException?.Invoke(this, e);
            }
        }

        private void ConnectedToServerHub(IAsyncResult ar)
        {
            tcpClient.EndConnect(ar);

            try
            {
                List<byte> buffer = new List<byte>();
                buffer.AddRange(BitConverter.GetBytes(Plugin.pluginVersion));
                buffer.AddRange(new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID()).ToBytes(false));
                tcpClient.SendData(new BasePacket(CommandType.Connect, buffer.ToArray()));
                BasePacket response = tcpClient.ReceiveData();

                if (response.commandType != CommandType.Connect)
                {
                    if(response.commandType == CommandType.Disconnect)
                    {
                        if (response.additionalData.Length > 0)
                        {
                            string reason = Encoding.UTF8.GetString(response.additionalData, 4, BitConverter.ToInt32(response.additionalData, 0));

                            ServerHubException?.Invoke(this, new Exception($"ServerHub refused connection! Reason: {reason}"));
                            Abort();
                            return;
                        }
                        else
                        {
                            ServerHubException?.Invoke(this, new Exception("ServerHub refused connection!"));
                            Abort();
                            return;
                        }
                    }
                    else
                    {
                        ServerHubException?.Invoke(this, new Exception("Unexpected response from ServerHub!"));
                        Abort();
                        return;
                    }
                }

                serverHubAvailable = true;

                tcpClient.SendData(new BasePacket(CommandType.GetRooms, new byte[0]));
                response = tcpClient.ReceiveData();

                tcpClient.SendData(new BasePacket(CommandType.Disconnect, new byte[0]));
                tcpClient.Close();

                if (response.commandType == CommandType.GetRooms)
                {
                    int roomsCount = BitConverter.ToInt32(response.additionalData, 0);

                    Stream byteStream = new MemoryStream(response.additionalData, 4, response.additionalData.Length - 4);

                    for (int i = 0; i < roomsCount; i++)
                    {
                        byte[] sizeBytes = new byte[4];
                        byteStream.Read(sizeBytes, 0, 4);

                        int roomInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                        byte[] roomInfoBytes = new byte[roomInfoSize];
                        byteStream.Read(roomInfoBytes, 0, roomInfoSize);

                        availableRooms.Add(new RoomInfo(roomInfoBytes));
                    }

                    ReceivedRoomsList?.Invoke(this, availableRooms);
                }
                else
                {
                    ServerHubException?.Invoke(this, new Exception("Unexpected response from ServerHub!"));
                    Abort();
                    return;
                }
            }catch(Exception e)
            {
                ServerHubException?.Invoke(this, e);
                Abort();
                return;
            }
        }

        public void Abort()
        {
            if (tcpClient != null)
            {
                tcpClient.Close();
            }
            availableRooms.Clear();
        }
    }
}
