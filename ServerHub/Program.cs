using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using ServerHub.Handlers;
using ServerCommons.Misc;

namespace ServerHub {
    class Program {
        private static string IP { get; set; }

        static void Main(string[] args) => new Program().Start(args);

        private ServerListener Listener { get; set; } = new ServerListener();

        private Thread listenerThread { get; set; }

        Program() {
            ShutdownEventCatcher.Shutdown += OnShutdown;
            IP = GetPublicIPv4();
        }

        private void OnShutdown(ShutdownEventArgs obj) {
            Listener.Stop();
            listenerThread.Join();
        }

        void Start(string[] args) {
            Logger.Instance.Log($"Current IP: {IP}");
            
            listenerThread = new Thread(() => Listener.Start()) {
                IsBackground = true
            };
            listenerThread.Start();
            while (listenerThread.IsAlive) {
                Console.Read();
            }
        }

        string GetPublicIPv4() {
            using (var client = new WebClient()) {
                return client.DownloadString("https://api.ipify.org");
            }
        }
    }
}