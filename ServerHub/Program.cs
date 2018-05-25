using System;
using System.Linq;
using System.Net;
using ServerHub.Misc;

namespace ServerHub {
    class Program {
        private static string IP { get; set; }

        static void Main(string[] args) => new Program().Start(args);

        private ServerListener Listener { get; set; } = new ServerListener();
        
        void Start(string[] args) {
            IP = GetPublicIPv4();
            Logger.Instance.Log($"Current IP: {IP}");
            Listener.Start();
            Listener.Stop();
        }

        string GetPublicIPv4() {
            using (var client = new WebClient()) {
                return client.DownloadString("https://api.ipify.org");
            }
        }
    }
}