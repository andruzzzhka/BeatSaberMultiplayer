using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class ServerHubFlowCoordinator : FlowCoordinator
    {
        MainFlowCoordinator _mainFlowCoordinator;
        ServerHubNavigationController _serverHubNavigationController;

        RoomListViewController _roomListViewController;

        List<ServerHubClient> _serverHubClients = new List<ServerHubClient>();

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (_mainFlowCoordinator == null)
                _mainFlowCoordinator = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();

            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                AvatarController.LoadAvatar();

                title = "Online Multiplayer";

                _serverHubNavigationController = BeatSaberUI.CreateViewController<ServerHubNavigationController>();
                _serverHubNavigationController.didFinishEvent += () => {
                    MainFlowCoordinator mainFlow = Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First();
                    mainFlow.InvokeMethod("DismissFlowCoordinator", this, null, false);
                };

                _roomListViewController = BeatSaberUI.CreateViewController<RoomListViewController>();

                _roomListViewController.createRoomButtonPressed += CreateRoomPressed;
                _roomListViewController.selectedRoom += RoomSelected;
            }

            SetViewControllerToNavigationConctroller(_serverHubNavigationController, _roomListViewController);
            ProvideInitialViewControllers(_serverHubNavigationController, null, null);

            StartCoroutine(UpdateRoomsListCoroutine());
        }

        private void CreateRoomPressed()
        {
            PresentFlowCoordinator(PluginUI.instance.roomCreationFlowCoordinator, null, false, false);
            PluginUI.instance.roomCreationFlowCoordinator.SetServerHubsList(_serverHubClients.Where(x => x.serverHubAvailable).ToList());

            PluginUI.instance.roomCreationFlowCoordinator.didFinishEvent += RoomCreationFlowCoordinator_didFinishEvent;
        }

        private void RoomCreationFlowCoordinator_didFinishEvent(bool immediately)
        {
            try
            {
                DismissFlowCoordinator(PluginUI.instance.roomCreationFlowCoordinator, null, immediately);
            }catch(Exception e)
            {
                Misc.Logger.Warning("Unable to dismiss flow coordinator! Exception: "+e);
            }
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
            PresentFlowCoordinator(PluginUI.instance.roomFlowCoordinator, null, false, false);
            PluginUI.instance.roomFlowCoordinator.JoinRoom(ip, port, roomId, usePassword, pass);
            PluginUI.instance.roomFlowCoordinator.didFinishEvent -= RoomFlowCoordinator_didFinishEvent;
            PluginUI.instance.roomFlowCoordinator.didFinishEvent += RoomFlowCoordinator_didFinishEvent;
        }
        
        public void ReturnToRoom()
        {

            PresentFlowCoordinator(PluginUI.instance.roomFlowCoordinator, null, false, false);
            PluginUI.instance.roomFlowCoordinator.ReturnToRoom();
            PluginUI.instance.roomFlowCoordinator.didFinishEvent -= RoomFlowCoordinator_didFinishEvent;
            PluginUI.instance.roomFlowCoordinator.didFinishEvent += RoomFlowCoordinator_didFinishEvent;
        }

        private void RoomFlowCoordinator_didFinishEvent()
        {
            DismissFlowCoordinator(PluginUI.instance.roomFlowCoordinator, null, false);
        }

        public IEnumerator UpdateRoomsListCoroutine()
        {
            yield return null;
            UpdateRoomsList();
        }

        public void UpdateRoomsList()
        {
            Misc.Logger.Info("Updating rooms list...");
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

            _roomListViewController.SetRooms(null);
            _serverHubNavigationController.SetLoadingState(true);
            _serverHubClients.ForEach(x => x.GetRooms());

            Misc.Logger.Info("Requested rooms lists from ServerHubs...");
        }

        private void ReceivedRoomsList(ServerHubClient sender, List<RoomInfo> rooms)
        {
            HMMainThreadDispatcher.instance.Enqueue(delegate ()
            {
                Misc.Logger.Info($"Received {rooms.Count} rooms from {sender.ip}:{sender.port}");
                _roomListViewController.SetRooms(_serverHubClients);
                _serverHubNavigationController.SetLoadingState(false);
            });
        }

        public void ServerHubException(ServerHubClient sender, Exception e)
        {
            Misc.Logger.Error($"ServerHub exception ({sender.ip}:{sender.port}): {e}");
        }
    }



    class ServerHubClient
    {
        private Socket socket;
        
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
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        public void GetRooms()
        {
            try
            {
                socket.BeginConnect(ip, port, new AsyncCallback(ConnectedToServerHub), null);
            }catch(Exception e)
            {
                ServerHubException?.Invoke(this, e);
            }
        }

        private void ConnectedToServerHub(IAsyncResult ar)
        {
            socket.EndConnect(ar);

            try
            {
                List<byte> buffer = new List<byte>();
                buffer.AddRange(BitConverter.GetBytes(Plugin.pluginVersion));
                buffer.AddRange(new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID()).ToBytes(false));
                socket.SendData(new BasePacket(CommandType.Connect, buffer.ToArray()));
                BasePacket response = socket.ReceiveData();

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

                socket.SendData(new BasePacket(CommandType.GetRooms, new byte[0]));
                response = socket.ReceiveData();

                socket.SendData(new BasePacket(CommandType.Disconnect, new byte[0]));

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
            if (socket != null)
            {
                socket.Close();
            }
            availableRooms.Clear();
        }
    }
}
