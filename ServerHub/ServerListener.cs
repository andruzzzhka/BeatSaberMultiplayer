using System.Net;
using System.Net.Sockets;
using ServerHub.Misc;

namespace ServerHub {
    public class ServerListener {
        private TcpListener Listener { get; set; } = new TcpListener(IPAddress.Any, Settings.Instance.SettingsIP.Port);

        public void Start() {
            Listener.Start();
        }

        public void Stop() {
            Listener.Stop();
        }
    }
}