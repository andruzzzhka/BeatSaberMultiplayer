using System;
using System.Linq;
using System.Net;
using System.Reflection;
using ServerHub.Misc;
using ServerHub.Rooms;
using System.IO;
using System.Collections.Generic;
using NetTools;
using ServerHub.Data;
using Newtonsoft.Json;
using static ServerHub.Hub.RCONStructs;
using static ServerHub.Misc.Logger;
using System.Diagnostics;
using Lidgren.Network;
using System.Text.RegularExpressions;
using Sentry;

namespace ServerHub.Hub
{
    public static class Program {
        private static string IP { get; set; }

        public static DateTime serverStartTime;

        public static long networkBytesInNow;
        public static long networkBytesOutNow;

        private static long networkBytesInLast;
        private static long networkBytesOutLast;
        
        private static DateTime lastNetworkStatsReset;

        public static List<IPAddressRange> blacklistedIPs;
        public static List<ulong> blacklistedIDs;
        public static List<string> blacklistedNames;

        public static List<IPAddressRange> whitelistedIPs;
        public static List<ulong> whitelistedIDs;
        public static List<string> whitelistedNames;

        public static List<IPlugin> plugins;

        public static List<Command> availableCommands = new List<Command>();
        
        static void Main(string[] args) => Start(args);

#if !DEBUG
        static private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Instance.Exception(e.ExceptionObject.ToString());
            Logger.Instance.Log("Shutting down...");
            List<BaseRoom> rooms = new List<BaseRoom>(RoomsController.GetRoomsList());
            foreach (BaseRoom room in rooms)
            {
                if(room != null)
                    RoomsController.DestroyRoom(room.roomId, "ServerHub exception occured!");
            }
            HubListener.Stop("ServerHub exception occured!");
        }
#endif

        static private SentryEvent CrashReportBeforeSend(SentryEvent e)
        {
            foreach (LogMessage logEntry in Logger.Instance.logHistory.TakeLast(20))
            {
                e.AddBreadcrumb(logEntry.Message, "Logger", "default", null, Sentry.Protocol.BreadcrumbLevel.Info);
            }
            return e;
        }

        static private void OnShutdown(ShutdownEventArgs obj) {
            foreach (IPlugin plugin in plugins)
            {
                try
                {
                    plugin.ServerShutdown();
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"[{plugin.Name}] Exception on ServerShutdown event: {e}");
                }
            }
            HubListener.Stop();
        }

        static void Start(string[] args) {
#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
#endif
            UpdateLists();

            if (!Directory.Exists("Plugins"))
            {
                try
                {
                    Directory.CreateDirectory("Plugins");
                }
                catch (Exception e)
                {
                    Logger.Instance.Exception($"Unable to create Plugins folder! Exception: {e}");
                }
            }

            Logger.Instance.Log($"Beat Saber Multiplayer ServerHub v{Assembly.GetEntryAssembly().GetName().Version}");

            string[] pluginFiles = Directory.GetFiles("Plugins", "*.dll");

            Logger.Instance.Log($"Found {pluginFiles.Length} plugins!");

            plugins = new List<IPlugin>();

            foreach (string path in pluginFiles)
            {
                try
                {
                    Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(path));
                    foreach (Type t in assembly.GetTypes())
                    {
                        if (t.GetInterface("IPlugin") != null)
                        {
                            try
                            {
                                IPlugin pluginInstance = Activator.CreateInstance(t) as IPlugin;

                                plugins.Add(pluginInstance);
                            }
                            catch (Exception e)
                            {
                                Logger.Instance.Error(string.Format("Unable to load plugin {0} in {1}! {2}", t.FullName, Path.GetFileName(path), e));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Instance.Error($"Unable to load assembly {Path.GetFileName(path)}! Exception: {e}");
                }
            }

            foreach(IPlugin plugin in plugins)
            {
                try
                {
                    if (plugin.Init())
                    {
                        Logger.Instance.Log("Initialized plugin: "+plugin.Name+" v"+plugin.Version);
                        HighResolutionTimer.LoopTimer.Elapsed += (object sender, HighResolutionTimerElapsedEventArgs e) =>
                        {
                            try
                            {
                                plugin.Tick(sender, e);
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Warning($"[{plugin.Name}] Exception on Tick event: {ex}");
                            }
                        };
                    }
                    else
                    {
                        plugins.Remove(plugin);
                    }
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"[{plugin.Name}] Exception on Init event: {e}");
                    plugins.Remove(plugin);
                }
            }

            if (args.Length > 0)
            {
                if (args[0].StartsWith("--"))
                {
                    string comName = args[0].ToLower().TrimStart('-');
                    string[] comArgs = args.Skip(1).ToArray();
                    Logger.Instance.Log(ProcessCommand(comName, comArgs), true);
                }
            }

            ShutdownEventCatcher.Shutdown += OnShutdown;

            if (Settings.Instance.Server.SendCrashReports)
            {
                SentrySdk.Init(o =>
                {
                    o.Dsn = new Dsn("https://b197685d885e473cbecbf93d8dc8628a@sentry.io/1380202");
                    o.AttachStacktrace = true;
                    o.BeforeSend = CrashReportBeforeSend;
#if DEBUG
                    o.Debug = true;
#endif
                });
                
            }

            if (!Directory.Exists("RoomPresets"))
            {
                try
                {
                    Directory.CreateDirectory("RoomPresets");
                }
                catch (Exception e)
                {
                    Logger.Instance.Exception($"Unable to create RoomPresets folder! Exception: {e}");
                }
            }
            
            foreach (string blacklistItem in new string[] { "76561201521433077", "IGGGAMES", "76561199437989403", "VALVE" })
            {
                if (!Settings.Instance.Access.Blacklist.Contains(blacklistItem))
                {
                    Settings.Instance.Access.Blacklist.Add(blacklistItem);
                    Settings.Instance.Save();
                }
            }

            Settings.Instance.Server.Tickrate = Misc.Math.Clamp(Settings.Instance.Server.Tickrate, 5, 150);
            HighResolutionTimer.LoopTimer.Interval = 1000f / Settings.Instance.Server.Tickrate;
            HighResolutionTimer.LoopTimer.Elapsed += ProgramLoop;
            HighResolutionTimer.LoopTimer.Start();
            HighResolutionTimer.VoIPTimer.Start();

            VersionChecker.CheckForUpdates();

            if (Settings.Instance.Server.EnableInteractiveShell)
            {
                ReadLine.AutoCompletionHandler = new AutoCompletionHandler();
                ReadLine.HistoryEnabled = true;
                SetupCommands();
            }

            IP = GetPublicIPv4();

            Logger.Instance.Log($"Hosting ServerHub @ {IP}:{Settings.Instance.Server.Port}");
            serverStartTime = DateTime.Now;

            HubListener.Start();
            HubListener.Listen = true;

            foreach (IPlugin plugin in plugins)
            {
                try
                {
                    plugin.ServerStart();
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"[{plugin.Name}] Exception on ServerStart event: {e}");
                }
            }

            if(Settings.Instance.Radio.RadioChannels.Count == 0)
            {
                Settings.Instance.Radio.EnableRadio = false;
                Settings.Instance.Radio.RadioChannels.Add(new Settings.ChannelSettings() { JoinMessages = new List<string>() { "{0} joined us!" } });
            }
            
            if (Settings.Instance.TournamentMode.Enabled)
                CreateTournamentRooms();

            while (HubListener.Listen)
            {
                ServerHubShell();
            }
        }

        static void ServerHubShell()
        {
            if (Settings.Instance.Server.EnableInteractiveShell)
            {
                Logger.Instance.Warning($"Use [Help] to display commands");
                Logger.Instance.Warning($"Use [Quit] to exit");


                while (HubListener.Listen)
                {
                    try
                    {
                        var x = ReadLine.Read(">>> ");
                        if (x == string.Empty) continue;

                        var parsedArgs = ParseLine(x);

                        Logger.Instance.Log(ProcessCommand(parsedArgs[0].ToLower(), parsedArgs.Skip(1).ToArray()));
                    }
                    catch (InvalidOperationException e)
                    {
                        if (e.Message.Contains("Console.Read"))
                        {
                            Logger.Instance.Exception(e.ToString());
                            Logger.Instance.Error("Disabling interactive shell...");
                            Settings.Instance.Server.EnableInteractiveShell = false;
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Exception(e.ToString());
                    }
                }
            }
            else
            {
                Logger.Instance.Warning($"Interactive shell is disabled");

                HighResolutionTimer.LoopTimer.thread.Join();
            }
        }

        static void ProgramLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if(DateTime.Now.Subtract(lastNetworkStatsReset).TotalSeconds > 1f)
            {
                lastNetworkStatsReset = DateTime.Now;
                networkBytesInLast = networkBytesInNow;
                networkBytesInNow = 0;
                networkBytesOutLast = networkBytesOutNow;
                networkBytesOutNow = 0;
            }
        }

        private static void CreateTournamentRooms()
        {
            for (int i = 0; i < Settings.Instance.TournamentMode.Rooms; i++)
            {

                RoomSettings settings = new RoomSettings()
                {
                    Name = string.Format(Settings.Instance.TournamentMode.RoomNameTemplate, i + 1),
                    UsePassword = Settings.Instance.TournamentMode.Password != "",
                    Password = Settings.Instance.TournamentMode.Password,
                    PerPlayerDifficulty = false,
                    MaxPlayers = 0,
                    SelectionType = SongSelectionType.Manual
                };

                uint id = RoomsController.CreateRoom(settings);

                Logger.Instance.Log("Created tournament room with ID " + id);
            }
        }

        public static string ProcessCommand(string comName, string[] comArgs)
        {
            if(comName == "crash")
            {
                throw new Exception("Debug Exception");
            }

            try
            {
                if(availableCommands.Any(x => x.name == comName))
                {
                    return availableCommands.First(x => x.name == comName).function(comArgs);
                }

                IPlugin plugin = plugins.FirstOrDefault(x => x.Commands.Any(y => y.name == comName));
                if (plugin != null)
                {
                    Command comm = plugin.Commands.First(x => x.name == comName);
                    return comm.function(comArgs);
                }

                if (!string.IsNullOrEmpty(comName))
                {
                    return $"{comName}: command not found";
                }
                else
                {
                    return $"command not found";
                }
            }
            catch (Exception e)
            {
                return $"Unable to process command! Exception: {e}";
            }
        }

        public static List<string> ParseLine(string line)
        {
            var args = new List<string>();
            var quote = false;
            for (int i = 0, n = 0; i <= line.Length; ++i)
            {
                if ((i == line.Length || line[i] == ' ') && !quote)
                {
                    if (i - n > 0)
                        args.Add(line.Substring(n, i - n).Trim(' ', '"'));

                    n = i + 1;
                    continue;
                }

                if (line[i] == '"')
                    quote = !quote;
            }

            return args;
        }

        private static void SetupCommands()
        {
            #region Console commands
            availableCommands.Add(new Command()
            {
                name = "help",
                help = "help - prints this text",
                function = (comArgs) => {
                    string commands = $"{Environment.NewLine}Default:";
                    foreach (var com in availableCommands.Where(x => !string.IsNullOrEmpty(x.help)))
                    {
                        commands += $"{Environment.NewLine}> {com.help}";
                    }

                    foreach (IPlugin plugin in plugins)
                    {
                        try
                        {
                            commands += $"{Environment.NewLine}{plugin.Name}:";
                            plugin.Commands.ForEach(x => commands += $"{Environment.NewLine}> {x.help}");
                        }
                        catch
                        {

                        }
                    }

                    return $"Commands:{commands}";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "version",
                help = "version - prints versions of ServerHub and installed plugins",
                function = (comArgs) => {
                    string versions = $"ServerHub v{Assembly.GetEntryAssembly().GetName().Version}";
                    foreach (IPlugin plugin in plugins)
                    {
                        try
                        {
                            versions += $"\n{plugin.Name} v{plugin.Version}";
                        }
                        catch
                        {

                        }
                    }
                    return versions;
                }
            });

            availableCommands.Add(new Command()
            {
                name = "quit",
                help = "quit - stops ServerHub",
                function = (comArgs) => {
                    if (HubListener.Listen)
                    {
                        Logger.Instance.Log("Shutting down...");
                        List<BaseRoom> rooms = new List<BaseRoom>(RoomsController.GetRoomsList());
                        foreach (BaseRoom room in rooms)
                        {
                            if (room != null)
                                RoomsController.DestroyRoom(room.roomId, "ServerHub exception occured!");
                        }
                        HubListener.Stop();
                    }

                    Environment.Exit(0);
                    return "";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "clients",
                help = "clients - prins list of connected clients",
                function = (comArgs) => {
                    string clientsStr = "";
                    if (HubListener.Listen)
                    {
                        clientsStr += $"{Environment.NewLine}┌─Lobby:";
                        if (HubListener.hubClients.Where(x => x.playerConnection != null).Count() == 0)
                        {
                            clientsStr += $"{Environment.NewLine}│ No Clients";
                        }
                        else
                        {
                            List<Client> clients = new List<Client>(HubListener.hubClients.Where(x => x.playerConnection != null));
                            foreach (var client in clients)
                            {
                                IPEndPoint remote = (IPEndPoint)client.playerConnection.RemoteEndPoint;
                                clientsStr += $"{Environment.NewLine}│ [{client.playerConnection.Status} | {client.playerInfo.updateInfo.playerState}] {client.playerInfo.playerName}({client.playerInfo.playerId}) @ {remote.Address}:{remote.Port}";
                            }
                        }

                        if (RadioController.radioStarted)
                        {
                            clientsStr += $"{Environment.NewLine}├─Radio:";
                            if (RadioController.radioChannels.Sum(y => y.radioClients.Where(x => x.playerConnection != null).Count()) == 0)
                            {
                                clientsStr += $"{Environment.NewLine}│ No Clients";
                            }
                            else
                            {
                                List<Client> clients = new List<Client>(RadioController.radioChannels.SelectMany(y => y.radioClients.Where(x => x.playerConnection != null)));
                                foreach (var client in clients)
                                {
                                    IPEndPoint remote = (IPEndPoint)client.playerConnection.RemoteEndPoint;
                                    clientsStr += $"{Environment.NewLine}│ [{client.playerConnection.Status} | {client.playerInfo.updateInfo.playerState}] {client.playerInfo.playerName}({client.playerInfo.playerId}) @ {remote.Address}:{remote.Port}";
                                }
                            }
                        }

                        if (RoomsController.GetRoomsList().Count > 0)
                        {
                            foreach (var room in RoomsController.GetRoomsList())
                            {
                                clientsStr += $"{Environment.NewLine}├─Room {room.roomId} \"{room.roomSettings.Name}\":";
                                if (room.roomClients.Count == 0)
                                {
                                    clientsStr += $"{Environment.NewLine}│ No Clients";
                                }
                                else
                                {
                                    List<Client> clients = new List<Client>(room.roomClients);
                                    foreach (var client in clients)
                                    {
                                        IPEndPoint remote = (IPEndPoint)client.playerConnection.RemoteEndPoint;
                                        clientsStr += $"{Environment.NewLine}│ [{client.playerConnection.Status} | {client.playerInfo.updateInfo.playerState}] {client.playerInfo.playerName}({client.playerInfo.playerId}) @ {remote.Address}:{remote.Port}";
                                    }
                                }
                            }
                        }

                        clientsStr += $"{Environment.NewLine}└─";

                        return clientsStr;

                    }
                    else
                    return "";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "blacklist",
                help = "blacklist [add/remove] [nick/playerID/IP] - bans/unbans players with provided player name/SteamID/OculusID/IP",
                function = (comArgs) => {
                    if (comArgs.Length == 2 && !string.IsNullOrEmpty(comArgs[1]))
                    {
                        switch (comArgs[0])
                        {
                            case "add":
                                {
                                    if (!Settings.Instance.Access.Blacklist.Contains(comArgs[1]))
                                    {
                                        Settings.Instance.Access.Blacklist.Add(comArgs[1]);
                                        Settings.Instance.Save();
                                        UpdateLists();

                                        return $"Successfully banned {comArgs[1]}";
                                    }
                                    else
                                    {
                                        return $"{comArgs[1]} is already blacklisted";
                                    }
                                }
                            case "remove":
                                {
                                    if (Settings.Instance.Access.Blacklist.Remove(comArgs[1]))
                                    {
                                        Settings.Instance.Save();
                                        UpdateLists();
                                        return $"Successfully unbanned {comArgs[1]}";
                                    }
                                    else
                                    {
                                        return $"{comArgs[1]} is not banned";
                                    }
                                }
                            default:
                                {
                                    return $"Command usage: blacklist [add/remove] [nick/playerID/IP]";
                                }
                        }
                    }
                    else
                    {
                        return $"Command usage: blacklist [add/remove] [nick/playerID/IP]";
                    }
                }
            });

            availableCommands.Add(new Command()
            {
                name = "whitelist",
                help = "whitelist [enable/disable/add/remove] [nick/playerID/IP] - enables/disables whitelist and modifies it",
                function = (comArgs) => {
                    if (comArgs.Length >= 1)
                    {
                        switch (comArgs[0])
                        {
                            case "enable":
                                {
                                    Settings.Instance.Access.WhitelistEnabled = true;
                                    Settings.Instance.Save();
                                    UpdateLists();
                                    return $"Whitelist enabled";
                                }
                            case "disable":
                                {
                                    Settings.Instance.Access.WhitelistEnabled = false;
                                    Settings.Instance.Save();
                                    UpdateLists();
                                    return $"Whitelist disabled";
                                }
                            case "add":
                                {
                                    if (comArgs.Length == 2 && !string.IsNullOrEmpty(comArgs[1]))
                                    {
                                        if (!Settings.Instance.Access.Whitelist.Contains(comArgs[1]))
                                        {
                                            Settings.Instance.Access.Whitelist.Add(comArgs[1]);
                                            Settings.Instance.Save();
                                            UpdateLists();
                                            return $"Successfully whitelisted {comArgs[1]}";
                                        }
                                        else
                                        {
                                            return $"{comArgs[1]} is already whitelisted";
                                        }
                                    }
                                    else
                                    {
                                        return $"Command usage: whitelist [enable/disable/add/remove] [nick/playerID/IP]";
                                    }
                                }
                            case "remove":
                                {
                                    if (comArgs.Length == 2 && !string.IsNullOrEmpty(comArgs[1]))
                                    {
                                        if (Settings.Instance.Access.Whitelist.Remove(comArgs[1]))
                                        {
                                            Settings.Instance.Save();
                                            UpdateLists();
                                            return $"Successfully removed {comArgs[1]} from whitelist";
                                        }
                                        else
                                        {
                                            return $"{comArgs[1]} is not whitelisted";
                                        }
                                    }
                                    else
                                    {
                                        return $"Command usage: whitelist [enable/disable/add/remove] [nick/playerID/IP]";
                                    }
                                }
                            default:
                                {
                                    return $"Command usage: whitelist [enable/disable/add/remove] [nick/playerID/IP]";
                                }
                        }
                    }
                    else
                    {
                        return $"Command usage: whitelist [enable/disable/add/remove] [nick/playerID/IP]";
                    }
                }
            });

            availableCommands.Add(new Command()
            {
                name = "tickrate",
                help = "tickrate [5-150] - changes tickrate of ServerHub",
                function = (comArgs) => {
                    int tickrate = Settings.Instance.Server.Tickrate;
                    if (int.TryParse(comArgs[0], out tickrate))
                    {
#if !DEBUG
                                tickrate = Misc.Math.Clamp(tickrate, 5, 150);
#endif
                        Settings.Instance.Server.Tickrate = tickrate;
                        HighResolutionTimer.LoopTimer.Interval = 1000f / tickrate;

                        return $"Set tickrate to {Settings.Instance.Server.Tickrate}";
                    }
                    else
                    {
                        return $"Command usage: tickrate [5-150]";
                    }
                }
            });

            availableCommands.Add(new Command()
            {
                name = "colors",
                help = "colors [playerId] [r: 0-255] [g: 0-255] [b: 0-255] - changes color of the player",
                function = (comArgs) => {
                    ulong playerId;
                    byte r;
                    byte g;
                    byte b;
                    if (comArgs.Length == 4 && ulong.TryParse(comArgs[0], out playerId) && byte.TryParse(comArgs[1], out r) && byte.TryParse(comArgs[2], out g) && byte.TryParse(comArgs[3], out b))
                    {
                        if (Settings.Instance.Misc.PlayerColors.ContainsKey(playerId))
                        {
                            Settings.Instance.Misc.PlayerColors[playerId] = new Color32(r, g, b); 
                        }
                        else
                        {
                            Settings.Instance.Misc.PlayerColors.Add(playerId, new Color32(r, g, b));
                        }

                        return $"Set {playerId} to R: {r}, G: {g}, B: {b}";
                    }
                    else
                    {
                        return $"Command usage: colors [playerId] [r: 0-255] [g: 0-255] [b: 0-255]";
                    }
                }
            });

            availableCommands.Add(new Command()
            {
                name = "createroom",
                help = "createroom [presetname] - creates new room from provided preset",
                function = (comArgs) => {
                    if (HubListener.Listen)
                    {
                        if (comArgs.Length == 1)
                        {
                            string path = comArgs[0];
                            if (!path.EndsWith(".json"))
                            {
                                path += ".json";
                            }

                            if (!path.ToLower().StartsWith("roompresets"))
                            {
                                path = Path.Combine("RoomPresets", path);
                            }

                            if (File.Exists(path))
                            {
                                string json = File.ReadAllText(path);

                                RoomPreset preset = JsonConvert.DeserializeObject<RoomPreset>(json);
                                
                                uint roomId = RoomsController.CreateRoom(preset.GetRoomSettings());

                                return "Created room with ID " + roomId;
                            }
                            else
                            {
                                return $"Unable to create room! File not found: {Path.GetFullPath(path)}";
                            }
                        }
                        else
                        {
                            return $"Command usage: createroom [presetname]";
                        }
                    }
                    else
                        return "";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "saveroom",
                help = "saveroom [roomId] [presetname] - saves room with provided roomId to the preset",
                function = (comArgs) => {
                    if (HubListener.Listen)
                    {
                        if (comArgs.Length == 2)
                        {
                            uint roomId;
                            if (uint.TryParse(comArgs[0], out roomId))
                            {
                                BaseRoom room = RoomsController.GetRoomsList().FirstOrDefault(x => x.roomId == roomId);
                                if (room != null)
                                {
                                    string path = comArgs[1];
                                    if (!path.EndsWith(".json"))
                                    {
                                        path += ".json";
                                    }

                                    if (!path.ToLower().StartsWith("roompresets"))
                                    {
                                        path = Path.Combine("RoomPresets", path);
                                    }

                                    Logger.Instance.Log("Saving room...");

                                    File.WriteAllText(Path.GetFullPath(path), JsonConvert.SerializeObject(new RoomPreset(room), Formatting.Indented));

                                    return "Saved room with ID " + roomId;
                                }
                                else
                                {
                                    return "Room with ID " + roomId + " not found!";
                                }
                            }
                            else
                            {
                                return $"Command usage: saveroom [roomId] [presetname]";
                            }

                        }
                        else
                        {
                            return $"Command usage: saveroom [roomId] [presetname]";
                        }

                    }
                    else
                        return "";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "cloneroom",
                help = "cloneroom [roomId] - clones room with provided roomId",
                function = (comArgs) => {
                    if (HubListener.Listen)
                    {
                        if (comArgs.Length == 1)
                        {
                            uint roomId;
                            if (uint.TryParse(comArgs[0], out roomId))
                            {
                                BaseRoom room = RoomsController.GetRoomsList().FirstOrDefault(x => x.roomId == roomId);
                                if (room != null)
                                {
                                    uint newRoomId = RoomsController.CreateRoom(room.roomSettings, room.roomHost);
                                    return "Cloned room roomId is " + newRoomId;
                                }
                                else
                                {
                                    return "Room with ID " + roomId + " not found!";
                                }
                            }
                            else
                            {
                                return $"Command usage: cloneroom [roomId]";
                            }
                        }
                        else
                        {
                            return $"Command usage: cloneroom [roomId]";
                        }
                    }
                    else
                        return "";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "destroyroom",
                help = "destroyroom [roomId] - destroys room with provided roomId",
                function = (comArgs) => {
                    if (HubListener.Listen)
                    {
                        if (comArgs.Length == 1)
                        {
                            uint roomId;
                            if (uint.TryParse(comArgs[0], out roomId))
                            {
                                if (RoomsController.DestroyRoom(roomId))
                                    return "Destroyed room " + roomId;
                                else
                                    return "Room with ID " + roomId + " not found!";
                            }
                            else
                            {
                                return $"Command usage: destroyroom [roomId]";
                            }
                        }
                        else
                        {
                            return $"Command usage: destroyroom [roomId]";
                        }
                    }
                    else
                        return "";
                }
            });
            
            availableCommands.Add(new Command()
            {
                name = "destroyempty",
                help = "destroyempty - destroys empty rooms",
                function = (comArgs) => {
                    string roomsStr = "Destroying empty rooms...";
                    if (HubListener.Listen)
                    {
                        List<uint> emptyRooms = RoomsController.GetRoomsList().Where(x => x.roomClients.Count == 0).Select(y => y.roomId).ToList();
                        if (emptyRooms.Count > 0)
                        {
                            foreach (uint roomId in emptyRooms)
                            {
                                RoomsController.DestroyRoom(roomId);
                                roomsStr += $"\nDestroyed room {roomId}!";
                            }
                        }
                        else
                        {

                            roomsStr += $"\nNo empty rooms!";
                        }
                    }
                    return roomsStr;
                }
            });
            
            availableCommands.Add(new Command()
            {
                name = "message",
                help = "message [roomId or *] [displayTime] [fontSize] [text] - sends message to specified rooms",
                function = (comArgs) => {
                    if (HubListener.Listen)
                    {
                        if (comArgs.Length >= 4)
                        {
                            if (comArgs[0] == "*")
                            {
                                float displayTime;
                                float fontSize;
                                string message = string.Join(" ", comArgs.Skip(3).ToArray()).Replace("\\n", Environment.NewLine);
                                if (float.TryParse(comArgs[1], out displayTime) && float.TryParse(comArgs[2], out fontSize))
                                {
                                    string output = $"Sending message \"{string.Join(" ", comArgs.Skip(3).ToArray())}\" to all rooms...";

                                    List<BaseRoom> rooms = RoomsController.GetRoomsList();
                                    rooms.ForEach((x) =>
                                    {

                                        NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
                                        outMsg.Write((byte)CommandType.DisplayMessage);
                                        outMsg.Write(displayTime);
                                        outMsg.Write(fontSize);
                                        outMsg.Write(message);
                                        outMsg.Write((byte)MessagePosition.Top);

                                        x.BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                        x.BroadcastWebSocket(CommandType.DisplayMessage, new DisplayMessage(displayTime, fontSize, message, MessagePosition.Top));
                                        output += $"\nSent message to all players in room {x.roomId}!";
                                    });

                                    List<RadioChannel> channels = RadioController.radioChannels;
                                    channels.ForEach((x) =>
                                    {

                                        NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
                                        outMsg.Write((byte)CommandType.DisplayMessage);
                                        outMsg.Write(displayTime);
                                        outMsg.Write(fontSize);
                                        outMsg.Write(message);
                                        outMsg.Write((byte)MessagePosition.Top);

                                        x.BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                                        x.BroadcastWebSocket(CommandType.DisplayMessage, new DisplayMessage(displayTime, fontSize, message, MessagePosition.Top));
                                        output += $"\nSent message to all players in radio channel {x.channelId}: \"{x.channelInfo.name}\"!";
                                    });
                                    return output;
                                }
                                else
                                    return $"Command usage: message [roomId or *] [displayTime] [fontSize] [text]";
                            }
                            else
                            {
                                uint roomId;
                                if (uint.TryParse(comArgs[0], out roomId))
                                {
                                    List<BaseRoom> rooms = RoomsController.GetRoomsList();
                                    if (rooms.Any(x => x.roomId == roomId))
                                    {
                                        float displayTime;
                                        float fontSize;
                                        if (float.TryParse(comArgs[1], out displayTime) && float.TryParse(comArgs[2], out fontSize))
                                        {
                                            string message = string.Join(" ", comArgs.Skip(3).ToArray()).Replace("\\n", Environment.NewLine);

                                            BaseRoom room = rooms.First(x => x.roomId == roomId);

                                            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
                                            outMsg.Write((byte)CommandType.DisplayMessage);
                                            outMsg.Write(displayTime);
                                            outMsg.Write(fontSize);
                                            outMsg.Write(message);
                                            outMsg.Write((byte)MessagePosition.Top);

                                            room.BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                            room.BroadcastWebSocket(CommandType.DisplayMessage, new DisplayMessage(displayTime, fontSize, message, MessagePosition.Top));
                                            return $"Sent message \"{string.Join(" ", comArgs.Skip(3).ToArray())}\" to all players in room {roomId}!";
                                        }
                                        else
                                            return $"Command usage: message [roomId or *] [displayTime] [fontSize] [text]";
                                    }
                                    else
                                        return "Room with ID " + roomId + " not found!";
                                }
                                else
                                {
                                    return $"Command usage: message [roomId or *] [displayTime] [fontSize] [text]";
                                }
                            }
                        }
                        else
                        {
                            return $"Command usage: message [roomId or *] [displayTime] [fontSize] [text]";
                        }
                    }
                    else
                        return "";
                }
            });
            #endregion

            #region Radio command

            availableCommands.Add(new Command()
            {
                name = "radio",
                help = "radio [help] - controls radio channel",
                function = (comArgs) => {
                    if (HubListener.Listen && comArgs.Length > 0)
                    {
                        switch (comArgs[0].ToLower())
                        {
                            case "help":
                                {
                                    return  "\n> radio help - prints this text" +
                                            "\n> radio enable - enables radio channel in settings and starts it" +
                                            "\n> radio disable - disables radio channel in settings and stops it" +
                                            "\n> radio [channelId] set [name/iconurl/difficulty] [value] - set specified option to provided value" +
                                            "\n> radio [channelId] queue list - lists songs in the radio queue" +
                                            "\n> radio [channelId] queue remove [songName] - removes song with provided name from the radio queue" +
                                            "\n> radio [channelId] queue add [song key or playlist path/url] - adds song or playlist to the queue";
                                }
                            case "enable":
                                {
                                    if (!Settings.Instance.Radio.EnableRadio)
                                    {
                                        Settings.Instance.Radio.EnableRadio = true;
                                        Settings.Instance.Save();
                                    }
                                    if (!RadioController.radioStarted)
                                    {
                                        RadioController.StartRadio();
                                    }
                                    return "Radio started!";
                                };
                            case "disable":
                                {
                                    if (Settings.Instance.Radio.EnableRadio)
                                    {
                                        Settings.Instance.Radio.EnableRadio = false;
                                        Settings.Instance.Save();
                                    }
                                    if (RadioController.radioStarted)
                                    {
                                        RadioController.StopRadio("Channel disabled from console!");
                                    }
                                    return "Radio stopped!";
                                };
                            case "list":
                                {
                                    if (Settings.Instance.Radio.EnableRadio && RadioController.radioStarted)
                                    {
                                        string buffer = "\nChannels list:";
                                        foreach (RadioChannel channel in RadioController.radioChannels)
                                        {
                                            buffer += $"\n{channel.channelId}: {channel.channelInfo.name} - Now playing: ({channel.channelInfo.currentSong.key}) {channel.channelInfo.currentSong.songName}";
                                        }
                                        return buffer;
                                    }
                                    else
                                    {
                                        return "Radio stopped!";
                                    }
                                };
                            default:
                                {
                                    int channelId;
                                    if(int.TryParse(comArgs[0], out channelId))
                                    {
                                        if (RadioController.radioChannels.Count > channelId)
                                        {

                                            switch (comArgs[1])
                                            {
                                                case "set":
                                                    {
                                                        if (comArgs.Length > 3)
                                                        {
                                                            switch (comArgs[2].ToLower())
                                                            {
                                                                case "name":
                                                                    {
                                                                        Settings.Instance.Radio.RadioChannels[channelId].ChannelName = string.Join(' ', comArgs.Skip(3));
                                                                        Settings.Instance.Save();

                                                                        if (RadioController.radioStarted)
                                                                            RadioController.radioChannels[channelId].channelInfo.name = Settings.Instance.Radio.RadioChannels[channelId].ChannelName;

                                                                        return $"Channel name set to \"{Settings.Instance.Radio.RadioChannels[channelId].ChannelName}\"!";
                                                                    }
                                                                case "iconurl":
                                                                    {
                                                                        Settings.Instance.Radio.RadioChannels[channelId].ChannelIconUrl = string.Join(' ', comArgs.Skip(3));
                                                                        Settings.Instance.Save();

                                                                        if (RadioController.radioStarted)
                                                                            RadioController.radioChannels[channelId].channelInfo.iconUrl = Settings.Instance.Radio.RadioChannels[channelId].ChannelIconUrl;

                                                                        return $"Channel icon URL set to \"{Settings.Instance.Radio.RadioChannels[channelId].ChannelIconUrl}\"!";
                                                                    }
                                                                case "difficulty":
                                                                    {
                                                                        BeatmapDifficulty difficulty = BeatmapDifficulty.Easy;

                                                                        if (Enum.TryParse(comArgs[3], true, out difficulty))
                                                                        {

                                                                            Settings.Instance.Radio.RadioChannels[channelId].PreferredDifficulty = difficulty;
                                                                            Settings.Instance.Save();

                                                                            if (RadioController.radioStarted)
                                                                                RadioController.radioChannels[channelId].channelInfo.currentLevelOptions.difficulty = Settings.Instance.Radio.RadioChannels[channelId].PreferredDifficulty;

                                                                            return $"Channel preferred difficulty set to \"{Settings.Instance.Radio.RadioChannels[channelId].PreferredDifficulty}\"!";
                                                                        }
                                                                        else
                                                                            return $"Unable to parse difficulty name!\nAvailable difficulties:\n> Easy\n> Normal\n> Hard\n> Expert\n> ExpertPlus";
                                                                    }
                                                                default:
                                                                    return $"Unable to parse option name!\nAvaiable options:\n> name\n> iconurl\n> difficulty";
                                                            }
                                                        }
                                                    }; break;
                                                case "queue":
                                                    {
                                                        if (comArgs.Length > 2)
                                                        {
                                                            switch (comArgs[2].ToLower())
                                                            {
                                                                case "list":
                                                                    {
                                                                        if (RadioController.radioStarted)
                                                                        {
                                                                            string buffer = $"\n┌─Now playing:\n│ {(!string.IsNullOrEmpty(RadioController.radioChannels[channelId].channelInfo.currentSong.key) ? RadioController.radioChannels[channelId].channelInfo.currentSong.key + ": " : "")} {RadioController.radioChannels[channelId].channelInfo.currentSong.songName}";
                                                                            buffer += "\n├─Queue:";

                                                                            if (RadioController.radioChannels[channelId].radioQueue.Count == 0)
                                                                            {
                                                                                buffer += "\n│ No songs";
                                                                            }
                                                                            else
                                                                            {
                                                                                foreach (var song in RadioController.radioChannels[channelId].radioQueue)
                                                                                {
                                                                                    buffer += $"\n│ {(!string.IsNullOrEmpty(song.key) ? song.key + ": " : "")} {song.songName}";
                                                                                }
                                                                            }
                                                                            buffer += "\n└─";

                                                                            return buffer;
                                                                        }
                                                                        else
                                                                        {
                                                                            return "Radio channel is stopped!";
                                                                        }
                                                                    }
                                                                case "clear":
                                                                    {
                                                                        if (RadioController.radioStarted)
                                                                        {
                                                                            RadioController.radioChannels[channelId].radioQueue.Clear();

                                                                            return "Queue cleared!";
                                                                        }
                                                                        else
                                                                        {
                                                                            return "Radio channel is stopped!";
                                                                        }
                                                                    }
                                                                case "remove":
                                                                    {
                                                                        if (comArgs.Length > 3)
                                                                        {
                                                                            if (RadioController.radioStarted)
                                                                            {
                                                                                string arg = string.Join(' ', comArgs.Skip(3));
                                                                                SongInfo songInfo = RadioController.radioChannels[channelId].radioQueue.FirstOrDefault(x => x.songName.ToLower().Contains(arg.ToLower()) || x.key == arg);
                                                                                if (songInfo != null)
                                                                                {
                                                                                    RadioController.radioChannels[channelId].radioQueue = new Queue<SongInfo>(RadioController.radioChannels[channelId].radioQueue.Where(x => !x.songName.ToLower().Contains(arg.ToLower()) && x.key != arg));
                                                                                    File.WriteAllText("RadioQueue.json", JsonConvert.SerializeObject(RadioController.radioChannels[channelId].radioQueue, Formatting.Indented));
                                                                                    return $"Successfully removed \"{songInfo.songName}\" from queue!";
                                                                                }
                                                                                else
                                                                                {
                                                                                    return $"Queue doesn't contain \"{arg}\"!";
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                return "Radio channel is stopped!";
                                                                            }
                                                                        }
                                                                    }
                                                                    break;
                                                                case "add":
                                                                    {
                                                                        if (comArgs.Length > 3)
                                                                        {
                                                                            if (RadioController.radioStarted)
                                                                            {
                                                                                string key = comArgs[3];
                                                                                Regex regex = new Regex(@"(\d+-\d+)");
                                                                                if (regex.IsMatch(key))
                                                                                {
                                                                                    RadioController.AddSongToQueueByKey(key, channelId);
                                                                                    return "Adding song to the queue...";
                                                                                }
                                                                                else
                                                                                {
                                                                                    string playlistPath = string.Join(' ', comArgs.Skip(3));

                                                                                    RadioController.AddPlaylistToQueue(playlistPath, channelId);
                                                                                    return "Adding all songs from playlist to the queue...";
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                return "Radio channel is stopped!";
                                                                            }
                                                                        }
                                                                    }
                                                                    break;
                                                                default:
                                                                    return $"Unable to parse option name!\nAvaiable options:\n> list\n> add\n> remove";
                                                            }
                                                        }
                                                    };
                                                    break;
                                                default:
                                                    return $"Command help: radio [help]";
                                            }
                                        }
                                        else
                                        {
                                            return $"Channel with ID {channelId} doesn't exist!";
                                        }

                                    }


                                };  break;
                        }
                    }
                    return $"Command help: radio [help]";
                }
            });

            #endregion

            #region RCON commands
            availableCommands.Add(new Command()
            {
                name = "serverinfo",
                help = "",
                function = (comArgs) => {
                    if (HubListener.Listen)
                    {
                        ServerInfo info = new ServerInfo()
                        {
                            ServerHubName = Settings.Instance.Server.ServerHubName,
                            Hostname = IP + ":" + Settings.Instance.Server.Port,
                            Tickrate = (float)System.Math.Round(HubListener.Tickrate, 1),
                            Memory = (float)System.Math.Round((Process.GetCurrentProcess().WorkingSet64 / 1024f / 1024f), 2),
                            Players = HubListener.hubClients.Count + RoomsController.GetRoomsList().Sum(x => x.roomClients.Count),
                            Uptime = (int)DateTime.Now.Subtract(serverStartTime).TotalSeconds,
                            RoomCount = RoomsController.GetRoomsCount(),
                            NetworkIn = (int)networkBytesInLast,
                            NetworkOut = (int)networkBytesOutLast
                        };

                        return JsonConvert.SerializeObject(info);
                    }
                    else return "";
                }
            });
            
            availableCommands.Add(new Command()
            {
                name = "console.tail",
                help = "",
                function = (comArgs) => {
                    int historySize = int.Parse(comArgs[0]);
                    int skip = Misc.Math.Max(Logger.Instance.logHistory.Count - historySize, 0);
                    int take = Misc.Math.Min(historySize, Logger.Instance.logHistory.Count);
                    List<LogMessage> msgs = Logger.Instance.logHistory.Skip(skip).Take(take).ToList();
                    
                    return JsonConvert.SerializeObject(msgs);
                }
            });

            availableCommands.Add(new Command()
            {
                name = "playerlist",
                help = "",
                function = (comArgs) => {

                    if (comArgs.Length == 0)
                    {
                        List<RCONPlayerInfo> hubClients = new List<RCONPlayerInfo>();

                        hubClients.AddRange(HubListener.hubClients.Select(x => new RCONPlayerInfo(x)));

                        return JsonConvert.SerializeObject(new
                        {
                            hubClients
                        });
                    }
                    else if(comArgs.Length == 1)
                    {
                        if(uint.TryParse(comArgs[0], out uint roomId))
                        {
                            if (RoomsController.GetRoomsList().Any(x => x.roomId == roomId))
                            {
                                List<RCONPlayerInfo> roomClients = new List<RCONPlayerInfo>();

                                roomClients = RoomsController.GetRoomsList().First(x => x.roomId == roomId).roomClients.Select(x => new RCONPlayerInfo(x)).ToList();

                                return JsonConvert.SerializeObject(roomClients);
                            }
                            else
                            {
                                return "[]";
                            }
                        }
                        else
                        {
                            return "[]";
                        }
                    }
                    else
                    {
                        return "[]";
                    }
                }
            });

            availableCommands.Add(new Command()
            {
                name = "roomslist",
                help = "",
                function = (comArgs) => {
                    List<RoomInfo> roomInfos = new List<RoomInfo>();
                    roomInfos = RoomsController.GetRoomInfosList();

                    return JsonConvert.SerializeObject(roomInfos);
                }
            });

            availableCommands.Add(new Command()
            {
                name = "channelslist",
                help = "",
                function = (comArgs) => {
                    List<RCONChannelInfo> channelInfos = new List<RCONChannelInfo>();
                    foreach(RadioChannel channel in RadioController.radioChannels)
                    {
                        channelInfos.Add(new RCONChannelInfo() { channelId = channel.channelId, name = channel.channelInfo.name, icon = channel.channelInfo.iconUrl, difficulty = channel.channelInfo.currentLevelOptions.difficulty.ToString(), currentSong = channel.channelInfo.currentSong.songName, queueLength = channel.radioQueue.Count });
                    }

                    return JsonConvert.SerializeObject(channelInfos);
                }
            });

            availableCommands.Add(new Command()
            {
                name = "channelinfo",
                help = "",
                function = (comArgs) => {

                    if (int.TryParse(comArgs[0], out int channelId))
                    {

                        SongInfo currentSong = RadioController.radioChannels[channelId].channelInfo.currentSong;
                        List<SongInfo> queuedSongs = new List<SongInfo>();
                        List<RCONPlayerInfo> radioClients = new List<RCONPlayerInfo>();

                        queuedSongs.AddRange(RadioController.radioChannels[channelId].radioQueue);
                        radioClients.AddRange(RadioController.radioChannels[channelId].radioClients.Select(x => new RCONPlayerInfo(x)));

                        RCONChannelInfo channelInfo = new RCONChannelInfo() { channelId = RadioController.radioChannels[channelId].channelId, name = RadioController.radioChannels[channelId].channelInfo.name, icon = RadioController.radioChannels[channelId].channelInfo.iconUrl, difficulty = RadioController.radioChannels[channelId].channelInfo.currentLevelOptions.difficulty.ToString(), currentSong = RadioController.radioChannels[channelId].channelInfo.currentSong.songName, queueLength = RadioController.radioChannels[channelId].radioQueue.Count };

                        return JsonConvert.SerializeObject(new
                        {
                            currentSong,
                            queuedSongs,
                            radioClients,
                            channelInfo
                        });
                    }
                    else return "[]";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "getsettings",
                help = "",
                function = (comArgs) => {
                    return JsonConvert.SerializeObject(Settings.Instance);
                }
            });

            availableCommands.Add(new Command()
            {
                name = "setsettings",
                help = "",
                function = (comArgs) => {
                    Settings.Instance.Load(string.Join(' ', comArgs));

                    if (Settings.Instance.Radio.EnableRadio && !RadioController.radioStarted)
                        RadioController.StartRadio();
                    if (!Settings.Instance.Radio.EnableRadio && RadioController.radioStarted)
                        RadioController.StopRadio("Channel disabled from console!");

                    Settings.Instance.Server.Tickrate = Misc.Math.Clamp(Settings.Instance.Server.Tickrate, 5, 150);
                    HighResolutionTimer.LoopTimer.Interval = 1000f / Settings.Instance.Server.Tickrate;

                    UpdateLists();

                    if (!SentrySdk.IsEnabled && Settings.Instance.Server.SendCrashReports)
                        SentrySdk.Init(o => 
                        {
                            o.Dsn = new Dsn("https://b197685d885e473cbecbf93d8dc8628a@sentry.io/1380202");
                            o.AttachStacktrace = true;
#if DEBUG
                            o.Debug = true;
#endif
                        });


                    return "";
                }
            });

            availableCommands.Add(new Command()
            {
                name = "accesslist",
                help = "",
                function = (comArgs) => {
                    return JsonConvert.SerializeObject(Settings.Instance.Access);
                }
            });

            #endregion

#if DEBUG
            #region Debug commands
            availableCommands.Add(new Command()
            {
                name = "testroom",
                help = "",
                function = (comArgs) => {
                    uint id = RoomsController.CreateRoom(new RoomSettings() { Name = "Debug Server", UsePassword = true, Password = "test", PerPlayerDifficulty = false, MaxPlayers = 4, SelectionType = SongSelectionType.Manual}, new Data.PlayerInfo("andruzzzhka", 76561198047255564));
                    return "Created room with ID " + id;
                }
            });
            
            availableCommands.Add(new Command()
            {
                name = "testroomwopass",
                help = "",
                function = (comArgs) => {
                    uint id = RoomsController.CreateRoom(new RoomSettings() { Name = "Debug Server", UsePassword = false, Password = "test", PerPlayerDifficulty = false, MaxPlayers = 4, SelectionType = SongSelectionType.Manual}, new Data.PlayerInfo("andruzzzhka", 76561198047255564));
                    return "Created room with ID " + id;
                }
            });
            #endregion
#endif
        }

        static private void UpdateLists()
        {
            IPAddressRange tryIp;
            ulong tryId;

            blacklistedIPs = Settings.Instance.Access.Blacklist.Where(x => IPAddressRange.TryParse(x, out tryIp)).Select(y => IPAddressRange.Parse(y)).ToList();
            blacklistedIDs = Settings.Instance.Access.Blacklist.Where(x => ulong.TryParse(x, out tryId)).Select(y => ulong.Parse(y)).ToList();
            blacklistedNames = Settings.Instance.Access.Blacklist.Where(x => !IPAddressRange.TryParse(x, out tryIp) && !ulong.TryParse(x, out tryId)).ToList();

            whitelistedIPs = Settings.Instance.Access.Whitelist.Where(x => IPAddressRange.TryParse(x, out tryIp)).Select(y => IPAddressRange.Parse(y)).ToList();
            whitelistedIDs = Settings.Instance.Access.Whitelist.Where(x => ulong.TryParse(x, out tryId)).Select(y => ulong.Parse(y)).ToList();
            whitelistedNames = Settings.Instance.Access.Whitelist.Where(x => !IPAddressRange.TryParse(x, out tryIp) && !ulong.TryParse(x, out tryId)).ToList();

            List<Client> clientsToKick = new List<Client>();
            clientsToKick.AddRange(HubListener.hubClients);
            foreach (var room in RoomsController.GetRoomsList())
            {
                clientsToKick.AddRange(room.roomClients);
            }

            foreach (var client in clientsToKick)
            {
                if (Settings.Instance.Access.WhitelistEnabled && !HubListener.IsWhitelisted(client.playerConnection.RemoteEndPoint, client.playerInfo))
                {
                    client.KickClient("You are not whitelisted!");
                }
                if (HubListener.IsBlacklisted(client.playerConnection.RemoteEndPoint, client.playerInfo))
                {
                    client.KickClient("You are banned!");
                }
            }
        }

        public static string GetPublicIPv4() {
            using (var client = new WebClient()) {
                try
                {
                    return client.DownloadString("https://api.ipify.org");
                }
                catch
                {
                    Logger.Instance.Warning($"Unable to fetch public IP!");
                    return "127.0.0.1";
                }
            }
        }
    }
}
