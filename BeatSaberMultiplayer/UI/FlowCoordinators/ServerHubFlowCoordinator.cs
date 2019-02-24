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
using Lidgren.Network;
using BS_Utils.Gameplay;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{

    struct ServerHubRoom
    {
        public string ip;
        public int port;
        public RoomInfo roomInfo;

        public ServerHubRoom(string ip, int port, RoomInfo roomInfo)
        {
            this.ip = ip;
            this.port = port;
            this.roomInfo = roomInfo;
        }
    }

    class ServerHubFlowCoordinator : FlowCoordinator
    {
        MultiplayerNavigationController _serverHubNavigationController;

        RoomListViewController _roomListViewController;

        List<ServerHubClient> _serverHubClients = new List<ServerHubClient>();

        List<ServerHubRoom> _roomsList = new List<ServerHubRoom>();

        public bool doNotUpdate = false;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        { 
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                title = "Online Multiplayer";

                _serverHubNavigationController = BeatSaberUI.CreateViewController<MultiplayerNavigationController>();
                _serverHubNavigationController.didFinishEvent += () => {
                    PluginUI.instance.modeSelectionFlowCoordinator.InvokeMethod("DismissFlowCoordinator", this, null, false);
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
            PluginUI.instance.roomCreationFlowCoordinator.SetServerHubsList(_serverHubClients);

            PluginUI.instance.roomCreationFlowCoordinator.didFinishEvent -= RoomCreationFlowCoordinator_didFinishEvent;
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

        private void RoomSelected(ServerHubRoom selectedRoom)
        {
            JoinRoom(selectedRoom.ip, selectedRoom.port, selectedRoom.roomInfo.roomId, selectedRoom.roomInfo.usePassword);
        }

        public void JoinRoom(string ip, int port, uint roomId, bool usePassword, string pass = "")
        {
            PresentFlowCoordinator(PluginUI.instance.roomFlowCoordinator, null, false, false);
            PluginUI.instance.roomFlowCoordinator.JoinRoom(ip, port, roomId, usePassword, pass);
            Client.Instance.InRadioMode = false;
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
            if (doNotUpdate)
            {
                doNotUpdate = false;
                yield break;
            }
            UpdateRoomsList();
        }

        public void UpdateRoomsList()
        {
            Misc.Logger.Info("Updating rooms list...");
            _serverHubClients.ForEach(x =>
            {
                if (x != null)
                {
                    x.Abort();
                    x.ReceivedRoomsList -= ReceivedRoomsList;
                    x.ServerHubException -= ServerHubException;
                }
            });
            _serverHubClients.Clear();
            _roomsList.Clear();

            for (int i = 0; i < Config.Instance.ServerHubIPs.Length ; i++)
            {
                string ip = Config.Instance.ServerHubIPs[i];
                int port = 3700;
                if (Config.Instance.ServerHubPorts.Length > i)
                {
                    port = Config.Instance.ServerHubPorts[i];
                }
                ServerHubClient client = new GameObject("ServerHubClient").AddComponent<ServerHubClient>();
                client.ip = ip;
                client.port = port;
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
            _roomsList.AddRange(rooms.Select(x => new ServerHubRoom(sender.ip, sender.port, x)));
            HMMainThreadDispatcher.instance.Enqueue(delegate ()
            {
                Misc.Logger.Info($"Received rooms from {sender.ip}:{sender.port}! Total rooms count: {_roomsList.Count}");
                _roomListViewController.SetRooms(_roomsList);
                _serverHubNavigationController.SetLoadingState(false);
            });
        }

        public void ServerHubException(ServerHubClient sender, Exception e)
        {
            Misc.Logger.Error($"ServerHub exception ({sender.ip}:{sender.port}): {e}");
        }
    }



    class ServerHubClient : MonoBehaviour
    {
        private NetClient NetworkClient;
        
        public string ip;
        public int port;

        public bool serverHubAvailable;
        public bool serverHubCompatible;
        public int availableRoomsCount;
        public int playersCount;

        public List<RoomInfo> availableRooms = new List<RoomInfo>();

        public event Action<ServerHubClient, List<RoomInfo>> ReceivedRoomsList;
        public event Action<ServerHubClient, Exception> ServerHubException;

        public void Awake()
        {
            NetPeerConfiguration Config = new NetPeerConfiguration("BeatSaberMultiplayer");
            NetworkClient = new NetClient(Config);
        }

        public void GetRooms()
        {
            try
            {
                NetworkClient.Start();

                Misc.Logger.Info($"Creating message...");
                NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                outMsg.Write(Plugin.pluginVersion);
                new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID()).AddToMessage(outMsg);

                Misc.Logger.Info($"Connecting to {ip}:{port}...");

                NetworkClient.Connect(ip, port, outMsg);
            }
            catch(Exception e)
            {
                ServerHubException?.Invoke(this, e);
                Abort();
            }
        }

        public void Update()
        {
            if (NetworkClient != null && NetworkClient.Status == NetPeerStatus.Running)
            {
                NetIncomingMessage msg;
                while ((msg = NetworkClient.ReadMessage()) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.StatusChanged:
                            {
                                NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();

                                if (status == NetConnectionStatus.Connected)
                                {
                                    serverHubCompatible = true;
                                    serverHubAvailable = true;
                                    NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                                    outMsg.Write((byte)CommandType.GetRooms);

                                    NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                }else if(status == NetConnectionStatus.Disconnected)
                                {
                                    try
                                    {
                                        string reason = msg.ReadString();
                                        if (reason.Contains("Version mismatch"))
                                        {
                                            serverHubCompatible = false;
                                            serverHubAvailable = true;
                                        }
                                        else
                                        {
                                            serverHubCompatible = false;
                                            serverHubAvailable = false;
                                        }
                                        ServerHubException?.Invoke(this, new Exception("ServerHub refused connection! Reason: " + reason));
                                    }
                                    catch (Exception e)
                                    {
                                        ServerHubException?.Invoke(this, new Exception("ServerHub refused connection! Exception: " + e));
                                    }
                                    Abort();
                                }

                            };
                            break;

                        case NetIncomingMessageType.Data:
                            {
                                if ((CommandType)msg.ReadByte() == CommandType.GetRooms)
                                {
                                    try
                                    {
                                        int roomsCount = msg.ReadInt32();

                                        availableRooms.Clear();

                                        for (int i = 0; i < roomsCount; i++)
                                        {
                                            availableRooms.Add(new RoomInfo(msg));
                                        }

                                        availableRoomsCount = availableRooms.Count;
                                        playersCount = availableRooms.Sum(x => x.players);
                                        ReceivedRoomsList?.Invoke(this, availableRooms);
                                    }catch(Exception e)
                                    {
                                        ServerHubException?.Invoke(this, new Exception("Unable to parse rooms list! Exception: "+e));
                                    }
                                    Abort();
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
                            Console.WriteLine("Unhandled type: " + msg.MessageType);
                            break;
#endif
                    }

                }
            }
        }

        public void Abort()
        {
            NetworkClient.Shutdown("");
            availableRooms.Clear();
            Destroy(gameObject);
        }
    }
}
