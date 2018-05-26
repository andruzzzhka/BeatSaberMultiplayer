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

namespace ServerHub.Handlers {
    public class ServerListener {
        private TcpListener Listener { get; set; } = new TcpListener(IPAddress.Any, Settings.Instance.Server.Port);

        private bool Listen { get; set; }

        private List<Data> ConnectedClients { get; set; } = new List<Data>();
        private Thread ClientWatcher { get; set; }

        public ServerListener() {
            ClientWatcher = new Thread(WatchClients) {
                IsBackground = true
            };
        }

        private void WatchClients() {
            while (Listen) {
                Thread.Sleep(new TimeSpan(0, 1, 0)); //check the servers every 1 minute
                ConnectedClients.AsParallel().ForAll(o => {
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
                                    ConnectedClients.Remove(o);
                                    o.TcpClient.Close();
                                }
                            }
                        }
                        else {
                            ConnectedClients.Remove(o);
                            o.TcpClient?.Close();
                        }
                    }
                    catch {
                        ConnectedClients.Remove(o);
                        o.TcpClient?.Close();
                    }
                });

                Thread.Sleep(16);
            }
        }

        public void Start() {
            Listen = true;
            ClientWatcher.Start();
            Listener.Start();
            BeginListening();
        }

        void BeginListening() {
            while (Listen) {
                var packet = ListenForPackets(out Data client);
                PacketHandler(packet, client);

                Thread.Sleep(16);
            }
        }

        /// <summary>
        /// Returns the Packet of data sent by a TcpClient
        /// </summary>
        /// <param name="serverData">Returns the ServerData object if the packet was a ServerDataPacket</param>
        /// <returns>The Packet sent</returns>
        IDataPacket ListenForPackets(out Data data) {
            Logger.Instance.Log("Waiting for a connection");
            var client = Listener.AcceptTcpClient();
            Logger.Instance.Log("Connected");
            byte[] bytes = new byte[Packet.MAX_BYTE_LENGTH];
            IDataPacket packet = null;
            if (client.GetStream().Read(bytes, 0, bytes.Length) != 0) {
                packet = Packet.ToPacket(bytes);
            }

            data = new Data {TcpClient = client};
            
            if (packet is ServerDataPacket) {
                data = new Data(ConnectedClients.Count) {TcpClient = client};
                ConnectedClients.Add(data);
                Logger.Instance.Log($"Server @ {data.IPv4} added to collection");
            }

            return packet;
        }

        void PacketHandler(IDataPacket dataPacket, Data data) {
            switch (dataPacket.ConnectionType) {
                case ConnectionType.Client:
                    Logger.Instance.Log("ConnectionType is [Client]");
                    var clientData = (ClientDataPacket)dataPacket;
                    clientData.Servers = GetServers(clientData.Offset);
                    data.TcpClient.GetStream().Write(clientData.ToBytes(), 0, Packet.MAX_BYTE_LENGTH);
                    break;
                case ConnectionType.Server:
                    Logger.Instance.Log("ConnectionType is [Server]");
                    var serverData = (ServerDataPacket)dataPacket;
                    if (serverData.RemoveFromCollection) {
                        ConnectedClients.Remove(data);
                        break;
                    }
                    data.IPv4 = serverData.IPv4;
                    data.Name = serverData.Name;
                    data.Port = serverData.Port;
                    break;
                case ConnectionType.Hub:
                    Logger.Instance.Log("ConnectionType is [Hub]");
                    break;
            }
        }

        List<Data> GetServers(int Offset, int count=10) {
            var index = (count - 1) * Offset;
            return ConnectedClients.GetRange(index, count + index >= ConnectedClients.Count ? Math.Clamp(ConnectedClients.Count - index, 0, 10) : count + index);
        }

        public void Stop() {
            Listen = false;
            Listener.Stop();
            ClientWatcher.Join();
        }
    }
}