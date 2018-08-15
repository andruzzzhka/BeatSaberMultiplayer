using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using ServerHub.Misc;
using ServerHub.Rooms;
using System.IO;
using System.Collections.Generic;
using NetTools;
using ServerHub.Data;
#if DEBUG
using System.IO.Pipes;
using System.Diagnostics;
#endif

namespace ServerHub.Hub
{
    class Program {
        private static string IP { get; set; }

        public static List<IPAddressRange> blacklistedIPs;
        public static List<ulong> blacklistedIDs;
        public static List<string> blacklistedNames;
        
        public static List<IPAddressRange> whitelistedIPs;
        public static List<ulong> whitelistedIDs;
        public static List<string> whitelistedNames;

        static void Main(string[] args) => new Program().Start(args);

        private Thread listenerThread { get; set; }

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Instance.Exception(e.ExceptionObject.ToString());
            Environment.FailFast("UnhadledException", e.ExceptionObject as Exception);
        }

        private void OnShutdown(ShutdownEventArgs obj) {
            HubListener.Stop();
        }

        void Start(string[] args) {

            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            UpdateLists();

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

            Settings.Instance.Server.Tickrate = Misc.Math.Clamp(Settings.Instance.Server.Tickrate, 5, 150);
            HighResolutionTimer.LoopTimer.Interval = 1000/Settings.Instance.Server.Tickrate;
            HighResolutionTimer.LoopTimer.Start();
#if DEBUG
            InitializePipe();
            HighResolutionTimer.LoopTimer.Elapsed += LoopTimer_Elapsed;
#endif

            IP = GetPublicIPv4();

            Logger.Instance.Log($"Beat Saber Multiplayer ServerHub v{Assembly.GetEntryAssembly().GetName().Version}");

            VersionChecker.CheckForUpdates();

            Logger.Instance.Log($"Hosting ServerHub @ {IP}:{Settings.Instance.Server.Port}");

            HubListener.Start();
            Logger.Instance.Warning($"Use [Help] to display commands");
            Logger.Instance.Warning($"Use [Quit] to exit");
            
            while (HubListener.Listen)
            {
                var x = Console.ReadLine();
                if (x == string.Empty) continue;
                var comParts = x?.Split(' ');
                string comName = comParts[0];
                string[] comArgs = comParts.Skip(1).ToArray();

                ProcessCommand(comName, comArgs, false);

            }
            
        }
#if DEBUG
        DateTime lastTick;

        NamedPipeServerStream pipeServer;
        StreamWriter pipeWriter;

        private async void InitializePipe()
        {
            if (pipeServer != null)
            {
                pipeServer.Close();
            }
            pipeServer = new NamedPipeServerStream("ServerHubLoopPipe", PipeDirection.Out);
            await pipeServer.WaitForConnectionAsync();

            pipeWriter = new StreamWriter(pipeServer);
            pipeWriter.AutoFlush = true;
            lastTick = DateTime.Now;

        }

        private void LoopTimer_Elapsed(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if (pipeServer.IsConnected)
            {
                try
                {
                    float milliseconds = (DateTime.Now.Subtract(lastTick).Ticks) / TimeSpan.TicksPerMillisecond;
                    lastTick = DateTime.Now;
                    List<byte> buffer = new List<byte>();
                    buffer.AddRange(BitConverter.GetBytes(milliseconds));
                    buffer.AddRange(BitConverter.GetBytes((float)e.Delay));
                    pipeWriter.BaseStream.Write(buffer.ToArray(), 0, buffer.Count);
                }catch(Exception)
                {
                    pipeServer.Dispose();

                    InitializePipe();
                }
            }
        }
#endif
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
                        s += "Clients in Lobby:";
                        foreach (var client in HubListener.GetClientsInLobby())
                        {
                            IPEndPoint remote = (IPEndPoint)client.tcpClient.Client.RemoteEndPoint;
                            s += $"{Environment.NewLine}[{client.playerInfo.playerState}] {client.playerInfo.playerName}({client.playerInfo.playerId}) @ {remote.Address}:{remote.Port}";
                        }

                        if (RoomsController.GetRoomsList().Count > 0)
                        {
                            s += $"{Environment.NewLine}Clients in Rooms:";
                            foreach (var room in RoomsController.GetRoomsList())
                            {
                                s += $"{Environment.NewLine}Room {room.roomId}:";
                                if (room.roomClients.Count == 0)
                                {
                                    s += $"{Environment.NewLine}No Clients";
                                }
                                else
                                {
                                    foreach (var client in room.roomClients)
                                    {
                                        IPEndPoint remote = (IPEndPoint)client.tcpClient.Client.RemoteEndPoint;
                                        s += $"{Environment.NewLine}[{client.playerInfo.playerState}] {client.playerInfo.playerName}({client.playerInfo.playerId}) @ {remote.Address}:{remote.Port}";
                                    }
                                }
                            }
                        }


                        Logger.Instance.Log(s);
                        
                    }
                    break;
                case "blacklist":
                    {
                        if (comArgs.Length == 2 && !string.IsNullOrEmpty(comArgs[1]))
                        {
                            switch (comArgs[0])
                            {
                                case "add":
                                    {
                                        if (!Settings.Instance.Access.Blacklist.Contains(comArgs[1]))
                                        {
                                            Settings.Instance.Access.Blacklist.Add(comArgs[1]);
                                            Logger.Instance.Log($"Successfully banned {comArgs[1]}", exitAfterPrint);
                                            Settings.Instance.Save();
                                            UpdateLists();
                                        }
                                        else
                                        {
                                            Logger.Instance.Log($"{comArgs[1]} is already blacklisted", exitAfterPrint);
                                        }
                                    }
                                    break;
                                case "remove":
                                    {
                                        if (Settings.Instance.Access.Blacklist.Remove(comArgs[1]))
                                        {
                                            Logger.Instance.Log($"Successfully unbanned {comArgs[1]}", exitAfterPrint);
                                            Settings.Instance.Save();
                                            UpdateLists();
                                        }
                                        else
                                        {
                                            Logger.Instance.Warning($"{comArgs[1]} is not banned", exitAfterPrint);
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        Logger.Instance.Warning($"Command usage: blacklist [add/remove] [playerID/IP]", exitAfterPrint);
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            Logger.Instance.Warning($"Command usage: blacklist [add/remove] [playerID/IP]", exitAfterPrint);
                        }
                    }
                    break;
                case "whitelist":
                    {
                        if (comArgs.Length >= 1)
                        {
                            switch (comArgs[0])
                            {
                                case "enable":
                                    {
                                        Settings.Instance.Access.WhitelistEnabled = true;
                                        Logger.Instance.Log($"Whitelist enabled", exitAfterPrint);
                                        Settings.Instance.Save();
                                        UpdateLists();
                                    }
                                    break;
                                case "disable":
                                    {
                                        Settings.Instance.Access.WhitelistEnabled = false;
                                        Logger.Instance.Log($"Whitelist disabled", exitAfterPrint);
                                        Settings.Instance.Save();
                                        UpdateLists();
                                    }
                                    break;
                                case "add":
                                    {
                                        if (comArgs.Length == 2 && !string.IsNullOrEmpty(comArgs[1]))
                                        {
                                            if (!Settings.Instance.Access.Whitelist.Contains(comArgs[1]))
                                            {
                                                Settings.Instance.Access.Whitelist.Add(comArgs[1]);
                                                Logger.Instance.Log($"Successfully whitelisted {comArgs[1]}", exitAfterPrint);
                                                Settings.Instance.Save();
                                                UpdateLists();
                                            }
                                            else
                                            {
                                                Logger.Instance.Log($"{comArgs[1]} is already whitelisted", exitAfterPrint);
                                            }
                                        }
                                        else
                                        {
                                            Logger.Instance.Warning($"Command usage: whitelist [enable/disable/add/remove] [playerID/IP]", exitAfterPrint);
                                        }
                                    }
                                    break;
                                case "remove":
                                    {
                                        if (comArgs.Length == 2 && !string.IsNullOrEmpty(comArgs[1]))
                                        {
                                            if (Settings.Instance.Access.Whitelist.Remove(comArgs[1]))
                                            {
                                                Logger.Instance.Log($"Successfully removed {comArgs[1]} from whitelist", exitAfterPrint);
                                                Settings.Instance.Save();
                                                UpdateLists();
                                            }
                                            else
                                            {
                                                Logger.Instance.Warning($"{comArgs[1]} is not whitelisted", exitAfterPrint);
                                            }
                                        }
                                        else
                                        {
                                            Logger.Instance.Warning($"Command usage: whitelist [enable/disable/add/remove] [playerID/IP]", exitAfterPrint);
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        Logger.Instance.Warning($"Command usage: whitelist [enable/disable/add/remove] [playerID/IP]", exitAfterPrint);
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            Logger.Instance.Warning($"Command usage: whitelist [enable/disable/add/remove] [playerID/IP]", exitAfterPrint);
                        }


                    }
                    break;
                case "tickrate":
                    {
                        int tickrate = 30;
                        if (int.TryParse(comArgs[0], out tickrate))
                        {
                            tickrate = Misc.Math.Clamp(tickrate, 5, 150);
                            Settings.Instance.Server.Tickrate = tickrate;
                            HighResolutionTimer.LoopTimer.Interval = 1000f / tickrate;
                        }
                    }
                    break;
#if DEBUG
                case "testroom":
                    {
                        RoomsController.CreateRoom(new Data.RoomSettings() { Name = "Debug Server", UsePassword = true, Password = "test", NoFail = true, MaxPlayers = 4, SelectionType = Data.SongSelectionType.Manual, AvailableSongs = new System.Collections.Generic.List<Data.SongInfo>() { new Data.SongInfo() { levelId = "07da4b5bc7795b08b87888b035760db7".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "451ffd065cf0e6adc01b2c3eda375794".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "97b35d13bac139c089a0c9d9306c9d76".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "a0d040d1a4fe833d5f9838d35777d302".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "61e3b11c1a4cd9185db46b1f7bb7ea54".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "e2d35a81fc0c54c326b09892c8d5c038".ToUpper(), songName = "" } } }, new Data.PlayerInfo("andruzzzhka", 76561198047255564));
                    }
                    break;
                case "testroomwopass":
                    {
                        RoomsController.CreateRoom(new Data.RoomSettings() { Name = "Debug Server", UsePassword = false, Password = "test", NoFail = false, MaxPlayers = 4, SelectionType = Data.SongSelectionType.Manual, AvailableSongs = new System.Collections.Generic.List<Data.SongInfo>() { new Data.SongInfo() { levelId = "07da4b5bc7795b08b87888b035760db7".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "451ffd065cf0e6adc01b2c3eda375794".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "97b35d13bac139c089a0c9d9306c9d76".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "a0d040d1a4fe833d5f9838d35777d302".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "61e3b11c1a4cd9185db46b1f7bb7ea54".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "e2d35a81fc0c54c326b09892c8d5c038".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "a8f8f95869b90a288a9ce4bdc260fa17".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "7dce2ba59bc69ec59e6ac455b98f3761".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "fbd77e71ce31329e5ebacde40c7401e0".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "7014f67926d216a6e2df026fa67017b0".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "51d0e56ecea0a98637c0323e7a3af7cf".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "9d1e4315971f6644ac94babdbd20e36a".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "9812c675def22f7405e0bf3422134756".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "1d46797ccb24acb86d0403828533df61".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "6ffccb03d75106c5911dd876dfd5f054".ToUpper(), songName = "" }, new Data.SongInfo() { levelId = "e3a97c826fab2ce5993dc2e71443b9aa".ToUpper(), songName = "" } } }, new Data.PlayerInfo("andruzzzhka", 76561198047255564));
                    }
                    break;
                
#endif
            }

            if (exitAfterPrint)
                Environment.Exit(0);
        }

        private void UpdateLists()
        {
            IPAddressRange tryIp;
            ulong tryId;

            blacklistedIPs = Settings.Instance.Access.Blacklist.Where(x => IPAddressRange.TryParse(x, out tryIp)).Select(y => IPAddressRange.Parse(y)).ToList();
            blacklistedIDs = Settings.Instance.Access.Blacklist.Where(x => ulong.TryParse(x, out tryId)).Select(y => ulong.Parse(y)).ToList();
            blacklistedNames = Settings.Instance.Access.Blacklist.Where(x => !IPAddressRange.TryParse(x, out tryIp) && !ulong.TryParse(x, out tryId)).ToList();

            whitelistedIPs = Settings.Instance.Access.Whitelist.Where(x => IPAddressRange.TryParse(x, out tryIp)).Select(y => IPAddressRange.Parse(y)).ToList();
            whitelistedIDs = Settings.Instance.Access.Whitelist.Where(x => ulong.TryParse(x, out tryId)).Select(y => ulong.Parse(y)).ToList();
            whitelistedNames = Settings.Instance.Access.Whitelist.Where(x => !IPAddressRange.TryParse(x, out tryIp) && !ulong.TryParse(x, out tryId)).ToList();
            
            foreach (var client in HubListener.GetClientsInLobby())
            {
                if (Settings.Instance.Access.WhitelistEnabled && !client.IsWhitelisted())
                {
                    client.KickClient("You are not whitelisted!");
                }
                if (client.IsBlacklisted())
                {
                    client.KickClient("You are banned!");
                }
            }

            foreach (var room in RoomsController.GetRoomsList())
            {
                List<Client> clients = new List<Client>(room.roomClients);
                foreach (var client in clients)
                {
                    if (Settings.Instance.Access.WhitelistEnabled && !client.IsWhitelisted())
                    {
                        client.KickClient("You are not whitelisted!");
                    }
                    if (client.IsBlacklisted())
                    {
                        client.KickClient("You are banned!");
                    }
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