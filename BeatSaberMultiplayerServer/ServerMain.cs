using BeatSaberMultiplayerServer.Misc;
using Newtonsoft.Json;
using ServerCommons.Data;
using ServerCommons.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Math = ServerCommons.Misc.Math;
using Settings = BeatSaberMultiplayerServer.Misc.Settings;
using Logger = ServerCommons.Misc.Logger;
using WebSocketSharp;
using WebSocketSharp.Server;
using NAudio.Wave;
using NVorbis;
using Open.Nat;

namespace BeatSaberMultiplayerServer
{
    public class Broadcast : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();
            Logger.Instance.Log("WebSocket Client Connected!");
        }
    }

    class ServerMain
    {
        public static Version serverVersion = Assembly.GetEntryAssembly().GetName().Version;

        static TcpListener _listener;

        public static List<Client> clients = new List<Client>();
        
        private static string beatSaverURL = "https://beatsaver.com";

        public static ServerState serverState = ServerState.Voting;
        public static List<CustomSongInfo> availableSongs = new List<CustomSongInfo>();

        public static int currentSongIndex = -1;
        private static int lastSelectedSong = -1;

        private static string TitleFormat = "{0} - {1} Clients Connected";

        public static TimeSpan playTime = new TimeSpan();

        static List<ServerHubClient> _serverHubClients = new List<ServerHubClient>();

        private Thread ListenerThread { get; set; }
        private Thread ServerLoopThread { get; set; }

        public WebSocketServer wss;

        static void Main(string[] args) => new ServerMain().Start(args);

        public void Start(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            if (args.Length > 0)
            {
                int port;
                if (int.TryParse(args[0], out port))
                {
                    Settings.Instance.Server.Port = port;
                }
                else
                {
                    if (args[0].StartsWith("--"))
                    {
                        ProcessCommand(args[0].TrimStart('-'), args.Skip(1).ToArray(), true);
                    }
                    else
                    {

                    }
                }
            }

            Console.Title = string.Format(TitleFormat, Settings.Instance.Server.ServerName, 0);
            Logger.Instance.Log($"Beat Saber Multiplayer Server v{serverVersion}");

            VersionChecker.CheckForUpdates();
            
            Logger.Instance.Log($"Hosting Server @ {Settings.Instance.Server.IP}:{Settings.Instance.Server.Port}");

            Logger.Instance.Log("Downloading songs from BeatSaver...");
            DownloadSongs();

            StartServer();

            ShutdownEventCatcher.Shutdown += OnServerShutdown;
            
            Logger.Instance.Warning($"Use [Help] to display commands");
            Logger.Instance.Warning($"Use [Quit] to exit");
            while (Thread.CurrentThread.IsAlive)
            {
                var x = Console.ReadLine();
                if (x == string.Empty) continue;
                var comParts = x?.Split(' ');
                var comName = comParts[0];
                var comArgs = comParts.Skip(1).ToArray();

                ProcessCommand(comName, comArgs, false);
            }
        }

        void ProcessCommand(string comName, string[] comArgs, bool exitAfterPrint)
        {
            string s = string.Empty;
            switch (comName.ToLower())
            {
                case "help":
                    foreach (var com in new[] { "help", "quit", "clients", "blacklist [add/remove] [playerID/IP]", "whitelist [enable/disable/add/remove] [playerID/IP]", "songs [add/remove/start/list] [songID on BeatSaver]" })
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
                        foreach (var t in clients)
                        {
                            var client = t.playerInfo;
                            if (t.playerInfo == null)
                            {
                                s +=
                                    $"{Environment.NewLine}[{t.state}] NOT AVAILABLE @ {((IPEndPoint)t._client.Client.RemoteEndPoint).Address}";
                            }
                            else
                            {
                                s +=
                                    $"{Environment.NewLine}[{t.state}] {client.playerName} @ {((IPEndPoint)t._client.Client.RemoteEndPoint).Address}";
                            }
                        }

                        if (s == String.Empty) s = " No Clients";
                        Logger.Instance.Log($"Connected Clients:{s}", exitAfterPrint);
                    }
                    break;
                case "blacklist":
                    {
                        if (comArgs.Length == 2 && !comArgs[1].IsNullOrEmpty())
                        {
                            switch (comArgs[0])
                            {
                                case "add":
                                    {
                                        if (!Settings.Instance.Access.Blacklist.Contains(comArgs[1]))
                                        {
                                            clients.Where(y => y.clientIP == comArgs[1] || y.playerId.ToString() == comArgs[1]).AsParallel().ForAll(z => z.KickClient());
                                            Settings.Instance.Access.Blacklist.Add(comArgs[1]);
                                            Logger.Instance.Log($"Successfully banned {comArgs[1]}", exitAfterPrint);
                                            Settings.Instance.Save();
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
                                    }
                                    break;
                                case "disable":
                                    {
                                        Settings.Instance.Access.WhitelistEnabled = false;
                                        Logger.Instance.Log($"Whitelist disabled", exitAfterPrint);
                                        Settings.Instance.Save();
                                    }
                                    break;
                                case "add":
                                    {
                                        if (comArgs.Length == 2 && !comArgs[1].IsNullOrEmpty())
                                        {
                                            if (!Settings.Instance.Access.Whitelist.Contains(comArgs[1]))
                                            {
                                                Settings.Instance.Access.Whitelist.Add(comArgs[1]);
                                                Settings.Instance.Save();
                                                Logger.Instance.Log($"Successfully whitelisted {comArgs[1]}", exitAfterPrint);
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
                                        if (comArgs.Length == 2 && !comArgs[1].IsNullOrEmpty())
                                        {
                                            clients.Where(y => y.clientIP == comArgs[1] || y.playerId.ToString() == comArgs[1]).AsParallel().ForAll(z => z.KickClient());
                                            if (Settings.Instance.Access.Whitelist.Remove(comArgs[1]))
                                            {
                                                Logger.Instance.Log($"Successfully removed {comArgs[1]} from whitelist", exitAfterPrint);
                                                Settings.Instance.Save();
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
                case "songs":
                    {
                        if (comArgs.Length >= 1)
                        {
                            switch (comArgs[0])
                            {
                                case "add":
                                    {
                                        string songId = comArgs[1];
                                        if (comArgs.Length == 2 && !comArgs[1].IsNullOrEmpty())
                                        {
                                            if (!Settings.Instance.AvailableSongs.Songs.Contains(songId))
                                            {
                                                List<string> songs = new List<string>(Settings.Instance.AvailableSongs.Songs);
                                                songs.Add(songId);
                                                Settings.Instance.AvailableSongs.Songs = songs.ToArray();
                                                Settings.Instance.Save();
                                                string path = DownloadSongByID(songId);

                                                ProcessSong(SongLoader.GetCustomSongInfo(path));

                                                Logger.Instance.Log($"Successfully added {comArgs[1]} to the song list", exitAfterPrint);

                                                SendToAllClients(new ServerCommand(
                                                    ServerCommandType.DownloadSongs,
                                                    _songs: availableSongs.Select(y => y.levelId).ToArray()), wss);
                                            }
                                            else
                                            {
                                                Logger.Instance.Log($"{comArgs[1]} is already on the song list", exitAfterPrint);
                                            }

                                        }
                                        else
                                        {
                                            Logger.Instance.Warning($"Command usage: songs [add/remove/start/list] [songID on BeatSaver]", exitAfterPrint);
                                        }
                                    }
                                    break;
                                case "remove":
                                    {
                                        string songId = comArgs[1];
                                        if (comArgs.Length == 2 && !comArgs[1].IsNullOrEmpty())
                                        {
                                            clients.Where(y => y.clientIP == comArgs[1] || y.playerId.ToString() == comArgs[1]).AsParallel().ForAll(z => z.KickClient());

                                            List<string> songs = new List<string>(Settings.Instance.AvailableSongs.Songs);

                                            if (songs.Remove(songId))
                                            {
                                                Settings.Instance.AvailableSongs.Songs = songs.ToArray();
                                                Settings.Instance.Save();
                                                availableSongs.RemoveAll(y => y.beatSaverId == songId);
                                                Logger.Instance.Log($"Successfully removed {comArgs[1]} from the song list", exitAfterPrint);
                                            }
                                            else
                                            {
                                                Logger.Instance.Warning($"{comArgs[1]} is not in the song list", exitAfterPrint);
                                            }
                                        }
                                        else
                                        {
                                            Logger.Instance.Warning($"Command usage: songs [add/remove/start/list] [songID on BeatSaver]", exitAfterPrint);
                                        }
                                    }
                                    break;
                                case "list":
                                    {
                                        if (exitAfterPrint)
                                        {
                                            foreach (var com in Settings.Instance.AvailableSongs.Songs)
                                            {
                                                s += $"{Environment.NewLine}> {com}";
                                            }
                                        }
                                        else
                                        {
                                            foreach (var com in availableSongs)
                                            {
                                                s += $"{Environment.NewLine}> {com.beatSaverId}: {com.authorName} - {com.songName} {com.songSubName}";
                                            }
                                        }

                                        Logger.Instance.Log($"Songs:{s}", exitAfterPrint);

                                    }; break;
                                case "start":
                                    {
                                        if (!exitAfterPrint && serverState != ServerState.Playing && currentSongIndex == -1)
                                        {
                                            string songId = comArgs[1];
                                            if (comArgs.Length == 2 && !comArgs[1].IsNullOrEmpty())
                                            {
                                                currentSongIndex = availableSongs.FindIndex(x => x.beatSaverId == songId);
                                                SendToAllClients(new ServerCommand(
                                                    ServerCommandType.SetSelectedSong,
                                                    _difficulty: GetPreferredDifficulty(availableSongs[currentSongIndex])), wss);
                                                Logger.Instance.Log($"Next song is {availableSongs[currentSongIndex].songName} {availableSongs[currentSongIndex].songSubName}");
                                            }
                                            else
                                            {
                                                Logger.Instance.Warning($"Command usage: songs [add/remove/start/list] [songID on BeatSaver]", exitAfterPrint);
                                            }
                                        }
                                    }; break;
                                default:
                                    {
                                        Logger.Instance.Warning($"Command usage: songs [add/remove/start/list] [songID on BeatSaver]", exitAfterPrint);
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            Logger.Instance.Warning($"Command usage: songs [add/remove/start/list] [songID on BeatSaver]", exitAfterPrint);
                        }
                    }; break;
                case "crash":
                    throw new Exception("DebugException");

            }

            if (exitAfterPrint)
            {
                Environment.Exit(0);
            }
        }
        
        private static void DownloadSongs()
        {
            Settings.Instance.Server.Downloaded.GetDirectories().AsParallel().ForAll(dir => dir.Delete(true));

            Settings.Instance.AvailableSongs.Songs.ToList().AsParallel().ForAll(id =>
            {
                DownloadSongByID(id);
            });

            Logger.Instance.Log("All songs downloaded!");

            List<CustomSongInfo> _songs = SongLoader.RetrieveAllSongs();

            _songs.AsParallel().ForAll(song =>
            {
                ProcessSong(song);
             });

            if (Settings.Instance.AvailableSongs.SongOrder == Settings.SongOrder.List)
            {
                CustomSongInfo[] buffer = new CustomSongInfo[availableSongs.Count];
                availableSongs.CopyTo(buffer);
                availableSongs.Clear();

                foreach (string id in Settings.Instance.AvailableSongs.Songs)
                {
                    try
                    {
                        availableSongs.Add(buffer.First(x => x.beatSaverId == id));
                    }
                    catch (Exception)
                    {
                        Logger.Instance.Warning($"Can't find song with ID {id}");
                    }
                }

                var notSortedSongs = buffer.Except(availableSongs).ToList();
                if (notSortedSongs.Count > 0)
                {
                    Logger.Instance.Warning($"{notSortedSongs.Count} songs not sorted!");
                    availableSongs.AddRange(notSortedSongs);
                }
            }
            

            Logger.Instance.Log("Done!");
        }

        private static string DownloadSongByID(string id)
        {
            var zipPath = Path.Combine(Settings.Instance.Server.Downloads.FullName, $"{id}.zip");
            Thread.Sleep(25);
            using (var client = new WebClient())
            {
                client.Headers.Add("user-agent",
                    $"BeatSaberMultiplayerServer-{Assembly.GetEntryAssembly().GetName().Version}");
                if (Settings.Instance.Server.Downloads.GetFiles().All(o => o.Name != $"{id}.zip"))
                {
                    Logger.Instance.Log($"Downloading {id}.zip");
                    try
                    {
                        client.DownloadFile($"{beatSaverURL}/download/{id}", zipPath);
                    }catch(Exception e)
                    {
                        Logger.Instance.Warning($"Can't download {id}.zip! Exception: {e}");
                        return null;
                    }
                }
            }

            ZipArchive zip = null;
            try
            {
                zip = ZipFile.OpenRead(zipPath);
            }
            catch (Exception ex)
            {
                Logger.Instance.Exception(ex.Message);
                return null;
            }

            var songName = zip?.Entries[0].FullName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            try
            {
                zip?.ExtractToDirectory(Path.Combine(Settings.Instance.Server.Downloaded.FullName, id.ToString()));
                try
                {
                    zip?.Dispose();
                }
                catch (IOException)
                {
                    Logger.Instance.Exception($"Failed to remove Zip [{id}]");
                    return null;
                }
            }
            catch (IOException)
            {
                Logger.Instance.Exception($"Folder [{songName}] exists. Continuing.");
                try
                {
                    zip?.Dispose();
                }
                catch (IOException)
                {
                    Logger.Instance.Exception($"Failed to remove Zip [{id}]");
                    return null;
                }
            }
            return Path.Combine(Settings.Instance.Server.Downloaded.FullName, id, songName);
        }

        private static void ProcessSong(CustomSongInfo song)
        {
            try
            {
                Logger.Instance.Log($"Processing {song.songName} {song.songSubName}");

                if (song.difficultyLevels[0].audioPath.ToLower().EndsWith(".ogg"))
                {
                    string pathToLoad = $"{song.path}/{song.difficultyLevels[0].audioPath}";
                    string[] actualFileNames = Directory.GetFiles(song.path,"*.ogg",SearchOption.TopDirectoryOnly);

                    if (!File.Exists(pathToLoad))
                    {
                        pathToLoad = $"{song.path}/{actualFileNames[0]}";
                    }

                    using (VorbisReader vorbis = new VorbisReader(pathToLoad))
                    {
                        song.duration = vorbis.TotalTime;
                    }
                }
                else
                {
                    string pathToLoad = $"{song.path}/{song.difficultyLevels[0].audioPath}";
                    string[] actualFileNames = Directory.GetFiles(song.path, "*.wav", SearchOption.TopDirectoryOnly);

                    if (!File.Exists(pathToLoad))
                    {
                        pathToLoad = $"{song.path}/{actualFileNames[0]}";
                    }

                    using (AudioFileReader wave = new AudioFileReader(pathToLoad))
                    {
                        song.duration = wave.TotalTime;
                    }
                }

                availableSongs.Add(song);
            }
            catch (AggregateException e)
            {
                Logger.Instance.Error(e.Message);
                Logger.Instance.Warning("One common cause of this is incorrect case sensitivity in the song's json file in comparison to its actual song name.");
            }
        }

        async void StartServer()
        {
            if (Settings.Instance.Server.TryUPnP)
            {
                Logger.Instance.Log($"Trying to open port {Settings.Instance.Server.Port} using UPnP...");
                try
                {
                    NatDiscoverer discoverer = new NatDiscoverer();
                    CancellationTokenSource cts = new CancellationTokenSource(2500);
                    NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                    await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, Settings.Instance.Server.Port, Settings.Instance.Server.Port, "BeatSaber Multiplayer Server"));

                    Logger.Instance.Log($"Port {Settings.Instance.Server.Port} is open!");
                }
                catch (Exception)
                {
                    Logger.Instance.Warning($"Can't open port {Settings.Instance.Server.Port} using UPnP!");
                }
            }


            Logger.Instance.Log("Starting server...");
            _listener = new TcpListener(IPAddress.Any, Settings.Instance.Server.Port);

            _listener.Start();

            Logger.Instance.Log("Waiting for clients...");

            ServerLoopThread = new Thread(ServerLoop) { IsBackground = true };
            ServerLoopThread.Start();

            AcceptClientThread();

            if (Settings.Instance.Server.WSEnabled)
            {
                if (Settings.Instance.Server.TryUPnP)
                {
                    Logger.Instance.Log($"Trying to open port {Settings.Instance.Server.WSPort} using UPnP...");
                    try
                    {
                        NatDiscoverer discoverer = new NatDiscoverer();
                        CancellationTokenSource cts = new CancellationTokenSource(2500);
                        NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                        await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, Settings.Instance.Server.WSPort, Settings.Instance.Server.WSPort, "BeatSaber Multiplayer WebSocket Server"));

                        Logger.Instance.Log($"Port {Settings.Instance.Server.WSPort} is open!");
                    }
                    catch (Exception)
                    {
                        Logger.Instance.Warning($"Can't open port {Settings.Instance.Server.WSPort} using UPnP!");
                    }
                }

                wss = new WebSocketServer(Settings.Instance.Server.WSPort);
                wss.AddWebSocketService<Broadcast>("/");
                wss.Start();
                Logger.Instance.Log($"WebSocket Server started @ {Settings.Instance.Server.IP}:{Settings.Instance.Server.WSPort}");
            }

            Dictionary<string, int> _serverHubs = new Dictionary<string, int>();

            for (int i = 0; i < Settings.Instance.Server.ServerHubIPs.Length; i++)
            {
                if (Settings.Instance.Server.ServerHubPorts.Length <= i)
                {
                    _serverHubs.Add(Settings.Instance.Server.ServerHubIPs[i], 3700);
                }
                else
                {
                    _serverHubs.Add(Settings.Instance.Server.ServerHubIPs[i], Settings.Instance.Server.ServerHubPorts[i]);
                }
            }

            _serverHubs.AsParallel().ForAll(x =>
            {
                ServerHubClient client = new ServerHubClient();
                _serverHubClients.Add(client);

                client.Connect(x.Key, x.Value);
            }
            );
            
        }

        void ServerLoop()
        {
            Stopwatch _timer = new Stopwatch();
            _timer.Start();
            int _timerSeconds = 0;
            TimeSpan _lastTime = new TimeSpan();

            float lobbyTimer = 0;
            float sendTimer = 0;

            float sendTime = 1f / 20;

            int lobbyTime = Settings.Instance.Server.LobbyTime;

            TimeSpan deltaTime;

            while (ServerLoopThread.IsAlive)
            {
                deltaTime = (_timer.Elapsed - _lastTime);

                _lastTime = _timer.Elapsed;

                try
                {
                    switch (serverState)
                    {
                        case ServerState.Preparing:
                        case ServerState.Voting:
                            {

                                sendTimer += (float)deltaTime.TotalSeconds;
                                lobbyTimer += (float)deltaTime.TotalSeconds;

                                if (clients.Count == 0)
                                {
                                    lobbyTimer = 0;
                                }

                                if (Math.Ceiling(lobbyTimer) > _timerSeconds && _timerSeconds > -1)
                                {
                                    _timerSeconds = Math.Ceiling(lobbyTimer);
                                    SendToAllClients(
                                        new ServerCommand(ServerCommandType.SetLobbyTimer,
                                            Math.Max(lobbyTime - _timerSeconds, 0)));
                                }

                                if (sendTimer >= sendTime)
                                {
                                    SendToAllClients(new ServerCommand(
                                        ServerCommandType.SetPlayerInfos,
                                        _playerInfos: (clients.Where(x => x != null && x.playerInfo != null)
                                            .Select(x => JsonConvert.SerializeObject(x.playerInfo))).ToArray()
                                        ));
                                    sendTimer = 0f;
                                }


                                if (currentSongIndex == -1 && availableSongs.Count > 0)
                                {
                                    switch (Settings.Instance.AvailableSongs.SongOrder)
                                    {
                                        case Settings.SongOrder.Voting: {

                                                if(lobbyTimer >= lobbyTime / 2)
                                                {
                                                    if (clients.Any(x => x.votedFor != null))
                                                    {
                                                        string selectedLevelId = clients.Where(x => x.votedFor != null).Select(x => x.votedFor).GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key.levelId;

                                                        CustomSongInfo song = availableSongs.FirstOrDefault(x => x.levelId == selectedLevelId);

                                                        if (song != null)
                                                        {
                                                            currentSongIndex = availableSongs.IndexOf(song);
                                                        }
                                                        else
                                                        {
                                                            currentSongIndex = lastSelectedSong + 1;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        currentSongIndex = lastSelectedSong + 1;
                                                    }
                                                }

                                            };break;
                                        case Settings.SongOrder.Shuffle: {
                                                Random rand = new Random();
                                                currentSongIndex = rand.Next(availableSongs.Count);
                                                if (currentSongIndex == lastSelectedSong) currentSongIndex = lastSelectedSong + 1;
                                            };break;
                                        case Settings.SongOrder.List: {
                                                currentSongIndex = lastSelectedSong + 1;
                                            };break;
                                        case Settings.SongOrder.Manual:
                                            {
                                                serverState = ServerState.Preparing;
                                                lobbyTimer = 0;
                                            };break;

                                    }

                                    if (currentSongIndex >= availableSongs.Count)
                                    {
                                        currentSongIndex = 0;
                                    }

                                    if (currentSongIndex != -1)
                                    {
                                        serverState = ServerState.Preparing;
                                        SendToAllClients(new ServerCommand(
                                            ServerCommandType.SetSelectedSong,
                                            _difficulty: GetPreferredDifficulty(availableSongs[currentSongIndex])), wss);
                                        Logger.Instance.Log($"Next song is {availableSongs[currentSongIndex].songName} {availableSongs[currentSongIndex].songSubName}");
                                    }
                                }

                                if (lobbyTimer >= lobbyTime)
                                {
                                    if (availableSongs.Count > 0)
                                    {
                                        SendToAllClients(new ServerCommand(
                                            ServerCommandType.StartSelectedSongLevel,
                                            _difficulty: GetPreferredDifficulty(availableSongs[currentSongIndex])), wss);

                                        serverState = ServerState.Playing;
                                        Logger.Instance.Log("Starting song " + availableSongs[currentSongIndex].songName + " " +
                                                            availableSongs[currentSongIndex].songSubName + "...");
                                    }
                                    _timerSeconds = 0;
                                    lobbyTimer = 0;
                                }
                            };
                            break;
                        case ServerState.Playing:
                            {
                                sendTimer += (float)deltaTime.TotalSeconds;
                                playTime += deltaTime;

                                if (sendTimer >= sendTime)
                                {
                                    SendToAllClients(new ServerCommand(
                                        ServerCommandType.SetPlayerInfos,
                                        _playerInfos: (clients.Where(x => x.playerInfo != null)
                                            .OrderByDescending(x => x.playerInfo.playerScore)
                                            .Select(x => JsonConvert.SerializeObject(x.playerInfo))).ToArray(),
                                        _selectedSongDuration: availableSongs[currentSongIndex].duration.TotalSeconds,
                                        _selectedSongPlayTime: playTime.TotalSeconds), wss);
                                    sendTimer = 0f;
                                }

                                if (playTime.TotalSeconds >= availableSongs[currentSongIndex].duration.TotalSeconds + 15f)
                                {
                                    playTime = new TimeSpan();
                                    sendTimer = 0f;
                                    serverState = ServerState.Voting;
                                    lastSelectedSong = currentSongIndex;
                                    currentSongIndex = -1;
                                    Logger.Instance.Log("Returning to lobby...");

                                    SendToAllClients(new ServerCommand(ServerCommandType.SetServerState));
                                }


                                if (clients.Count(x => x.state == ClientState.Playing) == 0 && playTime.TotalSeconds > 5 && playTime.TotalSeconds <= availableSongs[currentSongIndex].duration.TotalSeconds-10f)
                                {
                                    playTime = new TimeSpan();
                                    sendTimer = 0f;
                                    serverState = ServerState.Voting;
                                    lastSelectedSong = currentSongIndex;
                                    currentSongIndex = -1;
                                    Logger.Instance.Log("Returning to lobby (NO PLAYERS)...");

                                    SendToAllClients(new ServerCommand(ServerCommandType.SetServerState));
                                }
                            };
                            break;
                    }

                    Console.Title = string.Format(TitleFormat, Settings.Instance.Server.ServerName, clients.Count);
                }catch(Exception e)
                {
                    Logger.Instance.Error($"Server loop exception: {e}");
                }
                Thread.Sleep(5);
            }
        }

        static int GetPreferredDifficulty(CustomSongInfo _song)
        {
            int difficulty = 0;

            foreach (CustomSongInfo.DifficultyLevel diff in _song.difficultyLevels)
            {
                if ((int)Enum.Parse(typeof(Difficulty), diff.difficulty, true) <= (int)Settings.Instance.Server.PreferredDifficulty &&
                    (int)Enum.Parse(typeof(Difficulty), diff.difficulty, true) >= difficulty)
                {
                    difficulty = (int)Enum.Parse(typeof(Difficulty), diff.difficulty, true);
                }
            }

            if (difficulty == 0 && _song.difficultyLevels.Length > 0)
            {
                difficulty = (int)Enum.Parse(typeof(Difficulty), _song.difficultyLevels[0].difficulty, true);
            }

            return difficulty;
        }

        static void SendToAllClients(ServerCommand command, bool retryOnError = false)
        {
            try
            {
                clients.Where(x => x != null && (x.state == ClientState.Connected || x.state == ClientState.Playing))
                    .AsParallel().ForAll(x => { x.sendQueue.Enqueue(command); });
            }
            catch (Exception e)
            {
                Logger.Instance.Exception("Can't send message to all clients! Exception: " + e);
            }
        }

        static void SendToAllClients(ServerCommand command, WebSocketServer wss, bool retryOnError = false)
        {
            try
            {
                clients.Where(x => x != null && (x.state == ClientState.Connected || x.state == ClientState.Playing))
                    .AsParallel().ForAll(x => { x.sendQueue.Enqueue(command); });
                
                if (wss != null)
                {
                    wss.WebSocketServices["/"].Sessions.Broadcast(JsonConvert.SerializeObject(command));
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Exception("Can't send message to all clients! Exception: " + e);
            }
        }

        public static void UpdateServerHubs()
        {
            try
            {
                _serverHubClients.ForEach(x => x.UpdateServerState());
            }
            catch (Exception)
            {

            }
        }

        async void AcceptClientThread()
        {
            while (ServerLoopThread.IsAlive)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                clients.Add(new Client(client));
                UpdateServerHubs();
            }
        }
        
        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Instance.Exception(e.ExceptionObject.ToString());
            Environment.FailFast("UnhadledException", e.ExceptionObject as Exception);
        }

        private void OnServerShutdown(ShutdownEventArgs args)
        {
            Logger.Instance.Log("Shutting down server...");

            _serverHubClients.AsParallel().ForAll(x => x.Disconnect());

            clients.AsParallel().ForAll(x => x.KickClient("Server closed"));
            
            _listener.Stop();
        }
    }

    class ServerHubClient
    {
        public string ip;
        public int port;

        TcpClient client;

        public int serverID;
        
        public void Connect(string serverHubIP, int serverHubPort)
        {
            ip = serverHubIP;
            port = serverHubPort;

            try
            {
                client = new TcpClient(ip, port);

                ServerDataPacket packet = new ServerDataPacket
                {
                    ConnectionType = ConnectionType.Server,
                    FirstConnect = true,
                    IPv4 = Settings.Instance.Server.IP,
                    Port = Settings.Instance.Server.Port,
                    Name = Settings.Instance.Server.ServerName,
                    MaxPlayers = Settings.Instance.Server.MaxPlayers,
                    Players = 0
                };

                byte[] packetBytes = packet.ToBytes();

                client.GetStream().Write(packetBytes, 0, packetBytes.Length);

                byte[] bytes = new byte[Packet.MAX_BYTE_LENGTH];
                if (client.GetStream().Read(bytes, 0, bytes.Length) != 0)
                {
                    packet = (ServerDataPacket)Packet.ToPacket(bytes);
                }

                serverID = packet.ID;


                Logger.Instance.Log($"Connected to ServerHub @ {ip}:{port} with ID {serverID}");
            }
            catch(Exception e)
            {
                Logger.Instance.Warning($"Can't connect to ServerHub @ {ip}:{port}");
                Logger.Instance.Warning($"Exception: {e.Message}");
            }

        }

        public void UpdateServerState()
        {
            if (client.Connected)
            {
                try {
                    ServerDataPacket packet = new ServerDataPacket
                    {
                        ConnectionType = ConnectionType.Server,
                        FirstConnect = false,
                        IPv4 = Settings.Instance.Server.IP,
                        Port = Settings.Instance.Server.Port,
                        ID = serverID,
                        Name = Settings.Instance.Server.ServerName,
                        MaxPlayers = Settings.Instance.Server.MaxPlayers,
                        Players = ServerMain.clients.Count(x => x != null && x._client != null && x._client.Connected)
                    };

                    byte[] packetBytes = packet.ToBytes();

                    client.GetStream().Write(packetBytes, 0, packetBytes.Length);
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"Can't send updated server state to ServerHub @ {ip}:{port}");
                    Logger.Instance.Warning($"Exception: {e.Message}");
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                if (client != null && client.Connected)
                {
                    ServerDataPacket packet = new ServerDataPacket
                    {
                        ConnectionType = ConnectionType.Server,
                        ID = serverID,
                        FirstConnect = false,
                        IPv4 = Settings.Instance.Server.IP,
                        Port = Settings.Instance.Server.Port,
                        Name = Settings.Instance.Server.ServerName,
                        RemoveFromCollection = true
                    };

                    client.GetStream().Write(packet.ToBytes(), 0, packet.ToBytes().Length);
                    Logger.Instance.Log($"Removed this server from ServerHub @ {ip}:{port}");

                    client.Close();
                }
            }catch(Exception e)
            {
                Logger.Instance.Warning($"Can't remove server from ServerHub @ {ip}:{port}");
                Logger.Instance.Warning($"Exception: {e.Message}");
            }
        }

    }
}