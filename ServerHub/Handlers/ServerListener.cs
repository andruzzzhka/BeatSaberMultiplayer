using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ServerHub.Misc;

namespace ServerHub.Handlers {
    public class ServerListener {
        private TcpListener Listener { get; set; } = new TcpListener(IPAddress.Any, Settings.Instance.IP.Port);

        private bool Listen { get; set; }

        private List<ClientData> ConnectedClients { get; set; } = new List<ClientData>();
        private Thread ClientWatcher { get; set; }

        struct ClientData {
            public int ID { get; }
            public TcpClient TcpClient { get; set; }
            public string Name { get; set; }
            public string IPv4 { get; set; }
            public int Port { get; set; }

            public ClientData(int id) {
                ID = id;
                TcpClient = null;
                Name = "";
                IPv4 = "";
                Port = 0;
            }
        }
        
        public ServerListener() {
            ClientWatcher = new Thread(WatchClients) {
                IsBackground = true
            };
        }

        private void WatchClients() {
            while (Listen) {
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

        void BeginListening() { //TODO: Client sending data to main server. Use DataPacket class
            while (Listen) {
                Logger.Instance.Log("Waiting for a connection");
                var client = new ClientData(ConnectedClients.Count) {TcpClient = Listener.AcceptTcpClient()};
                ConnectedClients.Add(client);
                Logger.Instance.Log("Connected");
                byte[] bytes = new byte[DataPacket.MAX_BYTE_LENGTH];
                DataPacket packet = null;
                while (client.TcpClient.GetStream().Read(bytes, 0, bytes.Length) != 0) {
                    packet = DataPacket.ToPacket(bytes);
                }

                client.IPv4 = packet?.IPv4;
                client.Name = packet?.Name;
                if (packet?.Port != null) client.Port = packet.Port;

                Thread.Sleep(16);
            }
        }

        public void Stop() {
            Listen = false;
            Listener.Stop();
            ClientWatcher.Join();
        }
    }
}