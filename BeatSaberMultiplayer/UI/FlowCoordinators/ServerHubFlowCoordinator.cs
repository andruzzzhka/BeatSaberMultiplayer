using BeatSaberMultiplayer.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Lidgren.Network;
using BS_Utils.Gameplay;
using System.Reflection;
using BeatSaberMultiplayer.UI.ViewControllers.ServerHubScreen;
using HMUI;
using BeatSaberMarkupLanguage;
using BS_Utils.Utilities;
using UnityEngine.Networking;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{

    public struct ServerHubRoom
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
        RoomListViewController _roomListViewController;

        List<ServerHubClient> _serverHubClients = new List<ServerHubClient>();

        List<ServerHubRoom> _roomsList = new List<ServerHubRoom>();

        public bool doNotUpdate = false;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        { 
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                title = "Online Multiplayer";
                
                _roomListViewController = BeatSaberUI.CreateViewController<RoomListViewController>();

                _roomListViewController.createRoomButtonPressed += CreateRoomPressed;
                _roomListViewController.selectedRoom += RoomSelected;
                _roomListViewController.refreshPressed += RefreshPresed;
            }

            showBackButton = true;

            ProvideInitialViewControllers(_roomListViewController, null, null);

            StartCoroutine(GetServersFromRepositories());
            StartCoroutine(UpdateRoomsListCoroutine());
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if(topViewController == _roomListViewController)
            {
                PluginUI.instance.modeSelectionFlowCoordinator.InvokeMethod("DismissFlowCoordinator", this, null, false);
            }
        }

        private void RefreshPresed()
        {
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
            PluginUI.instance.roomCreationFlowCoordinator.didFinishEvent -= RoomCreationFlowCoordinator_didFinishEvent;
            try
            {
                DismissFlowCoordinator(PluginUI.instance.roomCreationFlowCoordinator, null, immediately);
            }catch(Exception e)
            {
                Plugin.log.Warn("Unable to dismiss flow coordinator! Exception: "+e);
            }
        }

        private void RoomSelected(ServerHubRoom selectedRoom, string password)
        {
            JoinRoom(selectedRoom.ip, selectedRoom.port, selectedRoom.roomInfo.roomId, selectedRoom.roomInfo.usePassword, password);
        }

        public void JoinRoom(string ip, int port, uint roomId, bool usePassword, string pass = "")
        {
            pass = pass?.ToUpper();
            PresentFlowCoordinator(PluginUI.instance.roomFlowCoordinator, null, false, false);
            PluginUI.instance.roomFlowCoordinator.JoinRoom(ip, port, roomId, usePassword, pass);
            Client.Instance.inRadioMode = false;
            PluginUI.instance.roomFlowCoordinator.didFinishEvent -= RoomFlowCoordinator_didFinishEvent;
            PluginUI.instance.roomFlowCoordinator.didFinishEvent += RoomFlowCoordinator_didFinishEvent;
        }

        private void RoomFlowCoordinator_didFinishEvent()
        {
            PluginUI.instance.roomCreationFlowCoordinator.didFinishEvent -= RoomCreationFlowCoordinator_didFinishEvent;
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

        protected IEnumerator GetServersFromRepositories()
        {
            Plugin.log.Warn("Starting GetServersFromRepositories");
            if (Config.Instance?.ServerRepositories == null)
                yield break;
            List<RepositoryServer> repoServers = new List<RepositoryServer>();
            int repositoriesUsed = 0;
            int serversAdded = 0;
            foreach (string serverRepoPath in Config.Instance.ServerRepositories)
            {
                Uri repoUri = null;
                try
                {
                    repoUri = new Uri(serverRepoPath, UriKind.Absolute);
                }
                catch (Exception ex)
                {
                    Plugin.log.Warn($"Invalid server repository URL: {serverRepoPath}");
                    Plugin.log.Debug(ex);
                    continue;
                }
                UnityWebRequest www = UnityWebRequest.Get(repoUri);
                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    Plugin.log.Warn($"Error getting Server Repository: {serverRepoPath}");
                    Plugin.log.Debug(www.error);
                }
                else
                {
                    string serverRepoJsonStr = www.downloadHandler.text;
                    try
                    {
                        ServerRepository repo = ServerRepository.FromJson(serverRepoJsonStr);
                        bool repositoryUsed = false;
                        foreach (var server in repo.Servers)
                        {
                            Plugin.log.Critical($"Server: {server.ToString()}");
                            if (server.IsValid)
                            {
                                repoServers.Add(server);
                                serversAdded++;
                                repositoryUsed = true;
                            }
                            else
                                Plugin.log.Warn($"Invalid server ({server.ToString()}) in repository {repo.RepositoryName}");
                        }
                        if (repositoryUsed)
                            repositoriesUsed++;
                    }
                    catch (Exception ex)
                    {
                        Plugin.log.Warn($"Error parsing ServerRepository from {serverRepoPath}");
                    }
                }
            }
            if (serversAdded > 0)
            {
                RepositoryServers = repoServers.ToArray();
                Plugin.log.Debug($"Finished getting {(serversAdded == 1 ? $"{serversAdded} server" : $"{serversAdded} servers")} from {(repositoriesUsed == 1 ? $"{repositoriesUsed} server" : $"{repositoriesUsed} servers")}.");
            }
            else
                Plugin.log.Debug("Did not get any servers from server repositories.");
            yield return UpdateRoomsListCoroutine();
        }

        public RepositoryServer[] RepositoryServers { get; private set; }

        public void UpdateRoomsList()
        {
            Plugin.log.Info("Updating rooms list...");
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

            // Store server addresses so duplicates aren't added.
            HashSet<string> serverAddresses = new HashSet<string>();
            if ((RepositoryServers?.Length ?? 0) > 0)
            {
                for (int i = 0; i < RepositoryServers.Length; i++)
                {
                    RepositoryServer repositoryServer = RepositoryServers[i];
                    string fullAddress = repositoryServer.ServerAddress + ":" + repositoryServer.ServerPort.ToString();
                    if (repositoryServer == null || !repositoryServer.IsValid)
                        continue;
                    if (serverAddresses.Contains(repositoryServer.ServerAddress))
                        continue;
                    serverAddresses.Add(fullAddress);
                    ServerHubClient client = repositoryServer.ToServerHubClient();
                    if (client != null)
                    {
                        client.ReceivedRoomsList += ReceivedRoomsList;
                        client.ServerHubException += ServerHubException;
                        _serverHubClients.Add(client);
                    }
                }
            }

            for (int i = 0; i < Config.Instance.ServerHubIPs.Length ; i++)
            {
                string ip = Config.Instance.ServerHubIPs[i];
                int port = 3700;
                if (Config.Instance.ServerHubPorts.Length > i)
                {
                    port = Config.Instance.ServerHubPorts[i];
                }
                string fullAddress = ip + ":" + port.ToString();
                if (serverAddresses.Contains(fullAddress) || port < 1 || port > 65535)
                    continue;
                serverAddresses.Add(fullAddress);
                ServerHubClient client = new GameObject("ServerHubClient").AddComponent<ServerHubClient>();
                client.ip = ip;
                client.port = port;
                client.ReceivedRoomsList += ReceivedRoomsList;
                client.ServerHubException += ServerHubException;
                _serverHubClients.Add(client);
            }

            _roomListViewController.SetRooms(null);
            _roomListViewController.SetRefreshButtonState(false);
            _serverHubClients.ForEach(x => x.GetRooms());

            Plugin.log.Info("Requested rooms lists from ServerHubs...");
        }

        private void ReceivedRoomsList(ServerHubClient sender, List<RoomInfo> rooms)
        {
            _roomsList.AddRange(rooms.Select(x => new ServerHubRoom(sender.ip, sender.port, x)));
            int roomsCount = rooms.Count;
            HMMainThreadDispatcher.instance.Enqueue(delegate ()
            {
                if (!string.IsNullOrEmpty(sender.serverHubName))
                    Plugin.log.Info($"Received {roomsCount} rooms from \"{sender.serverHubName}\" ({sender.ip}:{sender.port})! Total rooms count: {_roomsList.Count}");
                else
                    Plugin.log.Info($"Received {roomsCount} rooms from {sender.ip}:{sender.port}! Total rooms count: {_roomsList.Count}");
                _roomListViewController.SetRooms(_roomsList);
                _roomListViewController.SetRefreshButtonState(true);
            });
        }

        public void ServerHubException(ServerHubClient sender, Exception e)
        {
            if (!string.IsNullOrEmpty(sender.serverHubName))
                Plugin.log.Error($"ServerHub exception \"{sender.serverHubName}\" ({sender.ip}:{sender.port}): {e}");
            else
                Plugin.log.Error($"ServerHub exception ({sender.ip}:{sender.port}): {e}");
        }
    }

    public static class ServerHubExtensions
    {
        /// <summary>
        /// Converts a <see cref="RepositoryServer"/> to a <see cref="ServerHubClient"/>. Returns null if the <see cref="RepositoryServer"/> has invalid values.
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public static ServerHubClient ToServerHubClient(this RepositoryServer server)
        {
            if (string.IsNullOrEmpty(server?.ServerAddress) || server.ServerPort < 1 || server.ServerPort > 65535)
                return null;
            ServerHubClient client = new GameObject($"ServerHubClient.{server.ServerName}").AddComponent<ServerHubClient>();
            client.ip = server.ServerAddress;
            client.port = server.ServerPort;
            client.serverHubName = server.ServerName;
            return client;
        }
    }

    public class ServerHubClient : MonoBehaviour
    {
        private NetClient NetworkClient;

        public string serverHubName = "";
        public string ip;
        public int port;

        public bool serverHubAvailable;
        public bool serverHubCompatible;
        public int availableRoomsCount;
        public int playersCount;

        public float ping;

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
                Task.Run(() =>
                {
                    NetworkClient.Start();

                    Plugin.log.Debug($"Creating message...");
                    NetOutgoingMessage outMsg = NetworkClient.CreateMessage();

                    Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    byte[] version = new byte[4] { (byte)assemblyVersion.Major, (byte)assemblyVersion.Minor, (byte)assemblyVersion.Build, (byte)assemblyVersion.Revision };

                    outMsg.Write(version);
                    new PlayerInfo(GetUserInfo.GetUserName(), GetUserInfo.GetUserID()).AddToMessage(outMsg);

                    Plugin.log.Debug($"Connecting to {ip}:{port}...");

                    NetworkClient.Connect(ip, port, outMsg);
                }).ConfigureAwait(false);
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
                    if (NetworkClient.Connections.FirstOrDefault() != null) {
                        ping = Math.Abs(NetworkClient.Connections.First().AverageRoundtripTime);
                    }
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.StatusChanged:
                            {
                                NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();

                                if (status == NetConnectionStatus.Connected)
                                {
                                    if (NetworkClient.ServerConnection != null && NetworkClient.ServerConnection.RemoteHailMessage != null && NetworkClient.ServerConnection.RemoteHailMessage.LengthBytes > 4)
                                    {
                                        var hailMsg = NetworkClient.ServerConnection.RemoteHailMessage;

                                        try
                                        {
                                            byte[] serverVer = hailMsg.ReadBytes(4);
                                            serverHubName = hailMsg.ReadString();
                                        }
                                        catch (Exception e)
                                        {
                                            Plugin.log.Warn("Unable to read additional ServerHub info! Exception: " + e);
                                        }
                                    }

                                    serverHubCompatible = true;
                                    serverHubAvailable = true;
                                    NetOutgoingMessage outMsg = NetworkClient.CreateMessage();
                                    outMsg.Write((byte)CommandType.GetRooms);

                                    NetworkClient.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                }
                                else if(status == NetConnectionStatus.Disconnected)
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
                            Plugin.log.Warn(msg.ReadString());
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            Plugin.log.Error(msg.ReadString());
                            break;
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            Plugin.log.Debug(msg.ReadString());
                            break;
                        default:
                            Plugin.log.Debug("Unhandled type: " + msg.MessageType);
                            break;
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
