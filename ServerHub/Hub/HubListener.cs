using Open.Nat;
using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerHub.Hub
{
    static class HubListener
    {
        static TcpListener listener = new TcpListener(IPAddress.Any, Settings.Instance.Server.Port);

        static public bool Listen;

        static List<Client> hubClients = new List<Client>();

        public static void Start()
        {
            Listen = true;

            if (Settings.Instance.Server.TryUPnP)
            {
                OpenPort();
            }

            listener.Start();
            BeginListening();
        }

        public static void Stop()
        {

        }

        async static void OpenPort()
        {
            Logger.Instance.Log($"Trying to open port {Settings.Instance.Server.Port} using UPnP...");
            try
            {
                NatDiscoverer discoverer = new NatDiscoverer();
                CancellationTokenSource cts = new CancellationTokenSource(2500);
                NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, Settings.Instance.Server.Port, Settings.Instance.Server.Port, "BeatSaber Multiplayer ServerHub"));

                Logger.Instance.Log($"Port {Settings.Instance.Server.Port} is open!");
            }
            catch (Exception)
            {
                Logger.Instance.Warning($"Can't open port {Settings.Instance.Server.Port} using UPnP!");
            }
        }

        async static void BeginListening()
        {
            while (Listen)
            {
                Client client = await AcceptClient();
                hubClients.Add(client);
                client.clientDisconnected += ClientDisconnected;
            }
        }

        private static void ClientDisconnected(Client sender)
        {
            hubClients.Remove(sender);
        }

        static async Task<Client> AcceptClient()
        {
            Logger.Instance.Log("Waiting for a connection");
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                client = null;
            }
            return new Client(client);
        }

        public static List<Client> GetClientsInLobby()
        {
            return hubClients;
        }

    }
}
