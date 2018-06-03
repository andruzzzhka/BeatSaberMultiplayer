using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ServerCommons.Data;
using ServerCommons.Misc;
using Settings = ServerHub.Misc.Settings;
using Math = ServerCommons.Misc.Math;

namespace ServerHub.Handlers {
    public class ServerListener {
        private TcpListener Listener { get; set; } = new TcpListener(IPAddress.Any, Settings.Instance.Server.Port);

        private bool Listen { get; set; }

        public List<ClientObject> ConnectedClients { get; set; } = new List<ClientObject>();

        private string TitleFormat = "ServerHub - {0} Servers Connected";
        
        public class ClientObject {
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Data Data { get; set; }
        }

        public ServerListener() {
            Console.Title = string.Format(TitleFormat, 0);
        }

        public void RemoveClient(ClientObject cO) {
            if(cO.Data.ID!=-1) Logger.Instance.Log($"Unregistered Server [{cO.Data.Name}] @ [{cO.Data.IPv4}:{cO.Data.Port}]");
            ConnectedClients.Remove(cO);
            cO.Data.TcpClient.Close();
            cO.CancellationTokenSource.Cancel();
            
            Console.Title = string.Format(TitleFormat, ConnectedClients.Count(o => o.Data.ID != -1));
        }
        
        public void AddClient(ClientObject cO) {
            ConnectedClients.Add(cO);
            Console.Title = string.Format(TitleFormat, ConnectedClients.Count(o => o.Data.ID != -1));
        }

        public void Start() {
            Listen = true;
            Listener.Start();
            BeginListening();
        }

        void BeginListening() {
                Thread t = null;
                while (Listen) {
                    var data = AcceptClient();
                    CancellationTokenSource cts = new CancellationTokenSource();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(obj => {
                        CancellationTokenSource token = (CancellationTokenSource)obj;
                            var clientObject = new ClientObject {CancellationTokenSource = token, Data = data};
                            while (!token.IsCancellationRequested) {
                                if (!clientObject.Data.TcpClient.Connected) {
                                    RemoveClient(clientObject);
                                    break;
                                }

                                PacketHandler(ListenForPackets(ref clientObject), ref clientObject);
                            }
                        cts.Dispose();
                        }), cts);
                }
        }

        Data AcceptClient() {
            Logger.Instance.Log("Waiting for a connection");
            var client = Listener.AcceptTcpClient();
            //Logger.Instance.Log("Server Connected");
            return new Data {TcpClient = client};
        }

        /// <summary>
        /// Returns the Packet of data sent by a TcpClient
        /// </summary>
        /// <param name="serverData">Returns the ServerData object if the packet was a ServerDataPacket</param>
        /// <returns>The Packet sent</returns>
        IDataPacket ListenForPackets(ref ClientObject cO) {
            var client = cO.Data.TcpClient;
            byte[] bytes = new byte[Packet.MAX_BYTE_LENGTH];
            IDataPacket packet = null;
            try
            {
                if (client.GetStream().Read(bytes, 0, bytes.Length) != 0)
                {
                    packet = Packet.ToPacket(bytes);
                }
            
            
                if (packet is ServerDataPacket) {
                    var sPacket = (ServerDataPacket) packet;
                    if (sPacket.FirstConnect)
                    {
                        cO.Data = new Data(ConnectedClients.Count(x => x.Data.ID != -1) == 0 ? 0 : ConnectedClients.Last(x => x.Data.ID != -1).Data.ID + 1) { TcpClient = client, FirstConnect = sPacket.FirstConnect, IPv4 = sPacket.IPv4, Name = sPacket.Name, Port = sPacket.Port };

                        AddClient(cO);
                        Logger.Instance.Log($"Registered Server [{cO.Data.Name}] @ [{cO.Data.IPv4}:{cO.Data.Port}]");
                        //Logger.Instance.Log($"Server {cO.Data.ID} @ {cO.Data.IPv4}:{cO.Data.Port} added to collection");
                    }
                    else
                    {

                        //Logger.Instance.Log("Server already in collection");
                    }
                }

                return packet;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        void PacketHandler(IDataPacket dataPacket, ref ClientObject cO) {
            if(dataPacket == null)
            {
                return;
            }
            switch (dataPacket.ConnectionType) {
                case ConnectionType.Client:
                    //Logger.Instance.Log("ConnectionType is [Client]");
                    var clientData = (ClientDataPacket)dataPacket;
                    clientData.Servers = GetServers(clientData.Offset);
                    //Logger.Instance.Log($"Packet JSON:{Environment.NewLine}{clientData}");
                    if (cO.Data.TcpClient != null && cO.Data.TcpClient.Connected)
                    {
                        cO.Data.TcpClient.GetStream().Write(clientData.ToBytes(), 0, clientData.ToBytes().Length);
                        cO.Data.TcpClient.Close();
                        cO.CancellationTokenSource.Cancel();
                    }
                    break;
                case ConnectionType.Server:
                    //Logger.Instance.Log("ConnectionType is [Server]");
                    var serverData = (ServerDataPacket)dataPacket;
                    //Logger.Instance.Log($"Packet JSON:{Environment.NewLine}{serverData}");
                    if (serverData.RemoveFromCollection) {
                        var temp = cO.Data;
                        //Logger.Instance.Log($"Removing Server {serverData.ID} from Collection");
                        RemoveClient(ConnectedClients.First(o => o.Data.ID == temp.ID));
                        //Logger.Instance.Log($"Joining Thread of Server {serverData.ID}");
                        cO.CancellationTokenSource.Cancel();
                        break;
                    }
                    cO.Data.IPv4 = serverData.IPv4;
                    cO.Data.Name = serverData.Name;
                    cO.Data.Port = serverData.Port;
                    serverData.ID = cO.Data.ID;
                    cO.Data.TcpClient.GetStream().Write(serverData.ToBytes(), 0, serverData.ToBytes().Length);
                    break;
                case ConnectionType.Hub:
                    Logger.Instance.Log("ConnectionType is [Hub]");
                    break;
            }
        }

        List<Data> GetServers(int Offset, int count=6) {
            var index = count * Offset;
            var connectedClients = ConnectedClients
                .Where(cc => cc.Data.ID != -1)
                .Select(cc => cc.Data);

            return connectedClients.Skip(index).Take(count).ToList();
        }

        public void Stop() {
            Listen = false;
            Listener.Stop();
        }
    }
}