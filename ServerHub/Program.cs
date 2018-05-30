using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using BeatSaberMultiplayerServer.Misc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServerCommons.Misc;
using ServerHub.Handlers;
using Settings = ServerHub.Misc.Settings;

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
            //listenerThread.Abort();
        }

        void Start(string[] args) {
            Logger.Instance.Log($"Current IP: {IP}:{Settings.Instance.Server.Port}");
            
            listenerThread = new Thread(() => Listener.Start()) {
                IsBackground = true
            };
            listenerThread.Start();
            Logger.Instance.Warning($"Use [Help] to display commands");
            Logger.Instance.Warning($"Use [Quit] to exit");
            while (listenerThread.IsAlive) {
                var x = Console.ReadLine();
                if (x == string.Empty) continue;
                var comParts = x?.Split(' ');
                var comName = comParts[0];
                var comArgs = comParts.Skip(1).ToArray();
                string s = string.Empty;
                switch (comName.ToLower()) {
                    case "help":
                        foreach (var com in new [] {"Help", "Quit", "Clients"}) {
                            s += $"{Environment.NewLine}> {com}";
                        }
                        Logger.Instance.Log($"Commands:{s}");
                        break;
                    case "quit":
                        return;
                    case "clients":
                        foreach (var t in Listener.ConnectedClients.Where(o => o.Data.ID!=-1)) {
                            var client = t.Data;
                            s += $"{Environment.NewLine}[{client.ID}] {client.Name} @ {client.IPv4}:{client.Port}";
                        }
                        Logger.Instance.Log($"Connected Clients:{s}");
                        break;
                }
            }
        }

        string GetPublicIPv4() {
            using (var client = new WebClient()) {
                return client.DownloadString("https://api.ipify.org");
            }
        }
    }
}