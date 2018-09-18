using Open.Nat;
using ServerHub.Data;
using ServerHub.Misc;
using ServerHub.Rooms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerHub.Hub
{
    static class HubListener
    {
        private static List<float> _ticksLength = new List<float>();
        private static DateTime _lastTick;

        public static float Tickrate {
            get {
                return (1000 / (_ticksLength.Sum() / _ticksLength.Count));
            }
        }

        public static Timer pingTimer;
        
        static Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
        
        public static ManualResetEvent clientAccepted = new ManualResetEvent(false);

        static public bool Listen;

        public static List<Client> hubClients = new List<Client>();

        public static void Start()
        {
            if (Settings.Instance.Server.TryUPnP)
            {
                OpenPort();
            }

            if (Settings.Instance.Server.EnableWebSocketServer)
            {
                WebSocketListener.Start();
            }

            pingTimer = new Timer(PingTimerCallback, null, 7500, 5000);
            HighResolutionTimer.LoopTimer.Elapsed += HubLoop;
            _lastTick = DateTime.Now;
            
            listener.Bind(new IPEndPoint(IPAddress.Any, Settings.Instance.Server.Port));
            listener.Listen(25);
            ClientHelper.LostConnection += ClientHelper_LostConnection;
            BeginListening();
        }

        private static void ClientHelper_LostConnection(Client obj)
        {
            ClientDisconnected(obj);
        }

        public static void Stop()
        {
            listener.Close(2);
            Listen = false;
            WebSocketListener.Stop();
        }

        private static void HubLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if(_ticksLength.Count > 15)
            {
                _ticksLength.RemoveAt(0);
            }
            _ticksLength.Add(DateTime.Now.Subtract(_lastTick).Ticks/TimeSpan.TicksPerMillisecond);
            _lastTick = DateTime.Now;
            List<RoomInfo> roomsList = RoomsController.GetRoomInfosList();
            Console.Title = $"ServerHub v{Assembly.GetEntryAssembly().GetName().Version}: {roomsList.Count} rooms, {hubClients.Count} clients in lobby, {roomsList.Select(x => x.players).Sum() + hubClients.Count} clients total, {Tickrate.ToString("0.0")} tickrate";

        }
        
        private static void PingTimerCallback(object state)
        {
            try
            {
                foreach (Client client in hubClients)
                {
                    if (!client.socket.IsSocketConnected())
                    {
                        client.DestroyClient();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static bool IsSocketConnected(this Socket s)
        {
            return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) || !s.Connected);
        }

        async static void OpenPort()
        {
            Logger.Instance.Log($"Trying to open port {Settings.Instance.Server.Port} using UPnP...");
            try
            {
                NatDiscoverer discoverer = new NatDiscoverer();
                CancellationTokenSource cts = new CancellationTokenSource(3000);
                NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, Settings.Instance.Server.Port, Settings.Instance.Server.Port, "BeatSaber Multiplayer ServerHub"));

                Logger.Instance.Log($"Port {Settings.Instance.Server.Port} is open!");
            }
            catch (Exception)
            {
                Logger.Instance.Warning($"Unable to open port {Settings.Instance.Server.Port} using UPnP!");
            }
        }

        static void BeginListening()
        {
            while (Listen)
            {
                clientAccepted.Reset();

                listener.BeginAccept(new AsyncCallback(AcceptClient), listener);

                clientAccepted.WaitOne();
            }
        }

        private static void ClientJoinedRoom(Client sender, uint room, string password)
        {
            if(RoomsController.ClientJoined(sender, room, password))
            {
                if (hubClients.Contains(sender))
                    hubClients.Remove(sender);
            }
        }

        private static void ClientDisconnected(Client sender)
        {
            RoomsController.ClientLeftRoom(sender);
            if(hubClients.Contains(sender))
                hubClients.Remove(sender);
            sender.clientDisconnected -= ClientDisconnected;
            sender.clientJoinedRoom -= ClientJoinedRoom;
            sender.clientLeftRoom -= RoomsController.ClientLeftRoom;
        }

        static void AcceptClient(IAsyncResult ar)
        {
#if DEBUG
            Logger.Instance.Log("Waiting for a connection...");
#endif
            try
            {
                Client client = new Client(((Socket)ar.AsyncState).EndAccept(ar));

                if (client.InitializeClient())
                {
                    hubClients.Add(client);
                    client.clientDisconnected += ClientDisconnected;
                    client.clientJoinedRoom += ClientJoinedRoom;
                    client.clientLeftRoom += RoomsController.ClientLeftRoom;
                    client.ClientAccepted();
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Warning($"Unable to accept client! Exception: {e}");
            }

            clientAccepted.Set();
        }

    }
}
