using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using ServerHub.Rooms;
using ServerHub.Misc;
using Logger = ServerHub.Misc.Logger;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace ServerHub.Hub
{
    public class ListServices : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();

            List<string> paths = WebSocketListener.Server.WebSocketServices.Paths.ToList<string>();
            Send(JsonConvert.SerializeObject(paths));
        }
    }

    public class Broadcast : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();
            Logger.Instance.Log($"WebSocket Client Connected!");
        }
    }

    static class WebSocketListener
    {
        public static WebSocketServer Server;

        public static void Init()
        {
            int webSocketPort = Settings.Instance.Server.WebSocketPort;
            Server = new WebSocketServer(webSocketPort);

            Logger.Instance.Log($"Hosting WebSocket Server @ {Program.GetPublicIPv4()}:{webSocketPort}");

            Server.AddWebSocketService<ListServices>("/");
            Server.Start();
        }

        public static void BroadcastState()
        {
            if (Server != null)
            {
                List<string> paths = Server.WebSocketServices.Paths.ToList<string>();
                string data = JsonConvert.SerializeObject(paths);
                Server.WebSocketServices["/"].Sessions.BroadcastAsync(data, null);
            }
        }

        public static void AddRoom(Room room)
        {
            if (Server != null)
            {
                Server.AddWebSocketService<Broadcast>($"/room/{room.roomId}");
                BroadcastState();
            }
        }

        public static void DestroyRoom(Room room)
        {
            if (Server != null)
            {
                Server.RemoveWebSocketService($"/room/{room.roomId}");
                BroadcastState();
            }
        }
    }
}
