using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
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

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Instance.Exception(e.ExceptionObject.ToString());
            Environment.FailFast("UnhadledException", e.ExceptionObject as Exception);
        }

        private void OnShutdown(ShutdownEventArgs obj) {
            Listener.Stop();
        }

        void Start(string[] args) {

            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            if (args.Length > 0)
            {
                if (args[0].StartsWith("--"))
                {
                    ProcessCommand(args[0].TrimStart('-'), args.Skip(1).ToArray(), true);
                }
                else
                {

                }
            }

            ShutdownEventCatcher.Shutdown += OnShutdown;
            IP = GetPublicIPv4();

            Logger.Instance.Log($"Beat Saber Multiplayer ServerHub v{Assembly.GetEntryAssembly().GetName().Version}");

            VersionChecker.CheckForUpdates();

            Logger.Instance.Log($"Hosting ServerHub @ {IP}:{Settings.Instance.Server.Port}");

            Listener.Start();
            Logger.Instance.Warning($"Use [Help] to display commands");
            Logger.Instance.Warning($"Use [Quit] to exit");
            while (Listener.Listen)
            {
                var x = Console.ReadLine();
                if (x == string.Empty) continue;
                var comParts = x?.Split(' ');
                string comName = comParts[0];
                string[] comArgs = comParts.Skip(1).ToArray();

                ProcessCommand(comName, comArgs, false);

            }
        }

        void ProcessCommand(string comName, string[] comArgs, bool exitAfterPrint)
        {
            string s = string.Empty;
            switch (comName.ToLower())
            {
                case "help":
                    foreach (var com in new[] { "help", "version", "quit", "clients" })
                    {
                        s += $"{Environment.NewLine}> {com}";
                    }
                    Logger.Instance.Log($"Commands:{s}", exitAfterPrint);
                    break;
                case "version":
                    Logger.Instance.Log($"{Assembly.GetEntryAssembly().GetName().Version}", exitAfterPrint);
                    break;
                case "quit":
                    Environment.Exit(0);
                    return;
                case "clients":
                    if (!exitAfterPrint)
                    {
                        foreach (var t in Listener.ConnectedClients.Where(o => o.Data.ID != -1))
                        {
                            var client = t.Data;
                            s += $"{Environment.NewLine}[{client.ID}] {client.Name} @ {client.IPv4}:{client.Port}";
                        }
                        Logger.Instance.Log($"Connected Clients:{s}");
                    }
                    break;
                case "crash":
                    throw new Exception("DebugException");
            }

            if (exitAfterPrint)
                Environment.Exit(0);
        }

        string GetPublicIPv4() {
            using (var client = new WebClient()) {
                return client.DownloadString("https://api.ipify.org");
            }
        }
    }
}