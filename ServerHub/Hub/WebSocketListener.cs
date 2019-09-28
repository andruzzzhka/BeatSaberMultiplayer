using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ServerHub.Rooms;
using ServerHub.Misc;
using Logger = ServerHub.Misc.Logger;
using static ServerHub.Hub.RCONStructs;
using System.Linq;
using WebSocketSharp.Server;
using System.Reflection;
using ServerHub.Data;
using System.Security.Cryptography.X509Certificates;

namespace ServerHub.Hub
{
    public struct WebSocketPacket
    {
        public string commandType;
        public object data;

        public WebSocketPacket(CommandType command, object d)
        {
            commandType = command.ToString();
            data = d;
        }
    }

    public struct SongWithOptions
    {
        public SongInfo song;
        public LevelOptionsInfo options;

        public SongWithOptions(SongInfo songInfo, LevelOptionsInfo optionsInfo)
        {
            song = songInfo;
            options = optionsInfo;
        }
    }

    public struct ServerHubInfo
    {
        public string ServerHubName;
        public string Version;
        public int TotalClients;
        public List<WSRoomInfo> Rooms;
        public List<RCONChannelInfo> RadioChannels;
    }

    public struct WSRoomInfo
    {
        public string Path;
        public string RoomName;

        public WSRoomInfo(string _path)
        {
            Path = _path;

            string[] split = _path.Split('/');
            int _id = Convert.ToInt32(split[split.Length - 1]);

            BaseRoom _room = RoomsController.GetRoomsList().Find(room => room.roomId == _id);
            RoomName = _room.roomSettings.Name;
        }
    }

    public class ListServices : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();

            if (Settings.Instance.Server.EnableWebSocketRoomInfo)
            {
                try { 
                    List<string> paths = WebSocketListener.Server.WebSocketServices.Paths.ToList();
                    paths.Remove("/");
                    if (Settings.Instance.Server.EnableWebSocketRCON)
                    {
                        paths.Remove("/" + Settings.Instance.Server.RCONPassword);
                    }

                    List<WSRoomInfo> rooms = paths.Where(x => x.Contains("room")).Select(x => new WSRoomInfo(x)).ToList();
                    List<RCONChannelInfo> channels = paths.Where(x => x.Contains("channel")).Select(x => new RCONChannelInfo(x)).ToList();
                    ServerHubInfo info = new ServerHubInfo()
                    {
                        ServerHubName = Settings.Instance.Server.ServerHubName,
                        Version = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                        TotalClients = HubListener.hubClients.Count + RoomsController.GetRoomsList().Sum(x => x.roomClients.Count) + RadioController.radioChannels.Sum(x => x.radioClients.Count),
                        Rooms = rooms,
                        RadioChannels = channels
                    };
                    Send(JsonConvert.SerializeObject(info));
                }catch(Exception e)
                {
                    Logger.Instance.Warning("Unable to send ServerHub info to WebSocket client! Exception: " + e);
                }
            }
        }
    }

    public class Broadcast : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();
            Logger.Instance.Debug($"WebSocket Client Connected!");
            
            string[] split = Context.RequestUri.AbsolutePath.Split('/');
            int _id = Convert.ToInt32(split[split.Length - 1]);

            if (split[split.Length - 2] == "room")
            {
                BaseRoom _room = RoomsController.GetRoomsList().FirstOrDefault(room => room.roomId == _id);
                if (_room != null)
                    _room.OnOpenWebSocket();
            }else if(split[split.Length - 2] == "channel")
            {
                RadioChannel _channel = RadioController.radioChannels.FirstOrDefault(channel => channel.channelId == _id);
                if (_channel != null)
                    _channel.OnOpenWebSocket();
            }
        }
    }

    public class RCONBehaviour : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();
            Logger.Instance.Log($"RCON Client Connected!");
        }

        protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
        {
            base.OnMessage(e);

            try
            {
                if (e.IsText)
                {
                    IncomingMessage message = JsonConvert.DeserializeObject<IncomingMessage>(e.Data);
                    Logger.Instance.Debug($"Got message from RCON client: ID={message.Identifier}, Message={message.Message}");

                    List<string> args = Program.ParseLine(message.Message);
                    var response = Program.ProcessCommand(args[0], args.Skip(1).ToArray());

                    OutgoingMessage outMsg = new OutgoingMessage() { Identifier = message.Identifier, Message = response, Type = "Generic", Stacktrace = "" };

                    Send(JsonConvert.SerializeObject(outMsg));
                }
            }catch(Exception ex)
            {
                Logger.Instance.Warning("Unable to proccess a command from RCON client! Exception: "+ex);
            }
        }
    }

    static class WebSocketListener
    {
        public static WebSocketServer Server;

        public static void Start()
        {
            int webSocketPort = Settings.Instance.Server.WebSocketPort;
            Server = new WebSocketServer(webSocketPort, Settings.Instance.Server.SecureWebSocket && !string.IsNullOrEmpty(Settings.Instance.Server.WebSocketCertPath));

            if (Settings.Instance.Server.SecureWebSocket && !string.IsNullOrEmpty(Settings.Instance.Server.WebSocketCertPath))
            {
                if (!string.IsNullOrEmpty(Settings.Instance.Server.WebSocketCertPass))
                {
                    Server.SslConfiguration.ServerCertificate = new X509Certificate2(Settings.Instance.Server.WebSocketCertPath, Settings.Instance.Server.WebSocketCertPass);
                }
                else
                {
                    Server.SslConfiguration.ServerCertificate = new X509Certificate2(Settings.Instance.Server.WebSocketCertPath);
                }
                Server.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            }

            Logger.Instance.Log($"Hosting {(Settings.Instance.Server.SecureWebSocket && !string.IsNullOrEmpty(Settings.Instance.Server.WebSocketCertPath) ? "Secure " : "")}WebSocket Server @ {Program.GetPublicIPv4()}:{webSocketPort}");
            
            Server.AddWebSocketService<ListServices>("/");
            if (Settings.Instance.Server.EnableWebSocketRCON)
            {
                Server.AddWebSocketService<RCONBehaviour>("/"+Settings.Instance.Server.RCONPassword);
            }
            Server.ReuseAddress = true;
            Server.Start();
        }

        public static void Stop(WebSocketSharp.CloseStatusCode code = WebSocketSharp.CloseStatusCode.Away, string reason = "")
        {
            if(Server != null)
                Server.Stop(code, reason);
        }

        public static void BroadcastState()
        {
            if (Server != null && Settings.Instance.Server.EnableWebSocketRoomInfo)
            {
                try
                {
                    List<string> paths = Server.WebSocketServices.Paths.ToList();
                    paths.Remove("/");
                    if (Settings.Instance.Server.EnableWebSocketRCON)
                    {
                        paths.Remove("/" + Settings.Instance.Server.RCONPassword);
                    }

                    List<WSRoomInfo> rooms = paths.Where(x => x.Contains("room")).Select(x => new WSRoomInfo(x)).ToList();
                    List<RCONChannelInfo> channels = paths.Where(x => x.Contains("channel")).Select(x => new RCONChannelInfo(x)).ToList();

                    string data = JsonConvert.SerializeObject(new { rooms, channels });

                    Server.WebSocketServices["/"].Sessions.BroadcastAsync(data, null);
                }catch(Exception e)
                {
                    Misc.Logger.Instance.Error("WebSocket broadcast failed! Exception: "+e);
                }
            }
        }

        public static void AddChannel(RadioChannel channel)
        {
            if (Server != null && Settings.Instance.Server.EnableWebSocketRoomInfo)
            {
                Server.AddWebSocketService<Broadcast>($"/channel/{channel.channelId}");
                BroadcastState();
            }
        }

        public static void AddRoom(BaseRoom room)
        {
            if (Server != null && Settings.Instance.Server.EnableWebSocketRoomInfo)
            {
                Server.AddWebSocketService<Broadcast>($"/room/{room.roomId}");
                BroadcastState();
            }
        }

        public static void DestroyRoom(BaseRoom room)
        {
            if (Server != null && Settings.Instance.Server.EnableWebSocketRoomInfo)
            {
                Server.RemoveWebSocketService($"/room/{room.roomId}");
                BroadcastState();
            }
        }
    }
}
