using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ServerHub.Misc;
using ServerCommons.Data;
using ServerCommons.Misc;
using Settings = ServerHub.Misc.Settings;
using Math = ServerCommons.Misc.Math;

namespace ServerHub.Handlers {
    public class ServerListener {
        private TcpListener Listener { get; set; } = new TcpListener(IPAddress.Any, Settings.Instance.Server.Port);

        private bool Listen { get; set; }

        private List<ClientObject> ConnectedClients { get; set; } = new List<ClientObject>();
        private Thread ClientWatcher { get; set; }

        public class ClientObject {
            public Thread DataListener { get; set; }
            public Data Data { get; set; }
        }
        
        public ServerListener() {
            ClientWatcher = new Thread(WatchClients) {
                IsBackground = true
            };
        }

        private void WatchClients() {
            while (Listen) {
                Thread.Sleep(new TimeSpan(0, 1, 0)); //check the servers every 1 minute
                ConnectedClients.AsParallel().ForAll(cO => {
                    var o = cO.Data;
                    try {
                        if (o.TcpClient?.Client != null && o.TcpClient.Client.Connected) {
                            /* pear to the documentation on Poll:
                             * When passing SelectMode.SelectRead as a parameter to the Poll method it will return 
                             * -either- true if Socket.Listen(Int32) has been called and a connection is pending;
                             * -or- true if data is available for reading; 
                             * -or- true if the connection has been closed, reset, or terminated; 
                             * otherwise, returns false
                             */

                            // Detect if client disconnected
                            if (o.TcpClient.Client.Poll(0, SelectMode.SelectRead)) {
                                byte[] buff = new byte[1];
                                if (o.TcpClient.Client.Receive(buff, SocketFlags.Peek) == 0) {
                                    // Client disconnected
                                    RemoveClient(cO);
                                }
                            }
                        }
                        else {
                            RemoveClient(cO);
                        }
                    }
                    catch {
                        RemoveClient(cO);
                    }
                });

                Thread.Sleep(16);
            }
        }

        public void RemoveClient(ClientObject cO) {
            Logger.Instance.Log($"Server {cO.Data.ID} @ [{cO.Data.IPv4}:{cO.Data.Port}] Removed from collection");
            ConnectedClients.Remove(cO);
            cO.Data.TcpClient.Close();
        }

        public void Start() {
            Listen = true;
            ClientWatcher.Start();
            Listener.Start();
            BeginListening();
        }

        void BeginListening() {
            Thread t = null;
            while (Listen) {
                var data = AcceptClient();
                t = new Thread(o => {
                    var clientObject = new ClientObject {DataListener = t, Data = data};
                    ConnectedClients.Add(clientObject);
                    while (t.IsAlive) {
                        PacketHandler(ListenForPackets(ref clientObject), ref clientObject);
                    }
                }){IsBackground = true};
                t.Start();
            }
        }

        Data AcceptClient() {
            Logger.Instance.Log("Waiting for a connection");
            var client = Listener.AcceptTcpClient();
            Logger.Instance.Log("Connected");
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
            if (client.GetStream().Read(bytes, 0, bytes.Length) != 0) {
                packet = Packet.ToPacket(bytes);
            }
            
            if (packet is ServerDataPacket) {
                var sPacket = (ServerDataPacket) packet;
                if (sPacket.FirstConnect)
                {
                    cO.Data = new Data(ConnectedClients.Count(x => x.Data.ID != -1) == 0 ? 0 : ConnectedClients.Last(x => x.Data.ID != -1).Data.ID + 1) { TcpClient = client, FirstConnect = sPacket.FirstConnect, IPv4 = sPacket.IPv4, Name = sPacket.Name, Port = sPacket.Port };
                    Logger.Instance.Log($"Server {cO.Data.ID} @ {cO.Data.IPv4}:{cO.Data.Port} added to collection");
                }
                else
                {

                    Logger.Instance.Log("Server already in collection");
                }
            }

            return packet;
        }

        void PacketHandler(IDataPacket dataPacket, ref ClientObject cO) {
            switch (dataPacket.ConnectionType) {
                case ConnectionType.Client:
                    Logger.Instance.Log("ConnectionType is [Client]");
                    var clientData = (ClientDataPacket)dataPacket;
                    Logger.Instance.Log(clientData.ToString());
                    clientData.Servers = GetServers(clientData.Offset);
                    cO.Data.TcpClient.GetStream().Write(clientData.ToBytes(), 0, clientData.ToBytes().Length);
                    break;
                case ConnectionType.Server:
                    Logger.Instance.Log("ConnectionType is [Server]");
                    var serverData = (ServerDataPacket)dataPacket;
                    Logger.Instance.Log(serverData.ToString());
                    if (serverData.RemoveFromCollection) {
                        var temp = cO.Data;
                        Logger.Instance.Log($"Removing Server {serverData.ID} from Collection");
                        RemoveClient(ConnectedClients.First(o => o.Data.ID == temp.ID));
                        Logger.Instance.Log($"Joining Thread of Server {serverData.ID}");
                        cO.DataListener.Join();
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

        List<Data> GetServers(int Offset, int count=10) {
            var index = (count - 1) * Offset;
            return ConnectedClients.Select(o => o.Data).Where(o => o.ID != -1).ToList().GetRange(index, count + index >= ConnectedClients.Count ? Math.Clamp(ConnectedClients.Count - index, 0, 10) : count + index);
        }

        public void Stop() {
            Listen = false;
            Listener.Stop();
            ClientWatcher.Join();
        }
    }
}