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

            Close();
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

        public static void AddRoom (Room room)
        {
            Server.AddWebSocketService<Broadcast>($"/room/{room.roomId}");
        }

        public static void DestroyRoom (Room room)
        {
            Server.RemoveWebSocketService($"/room/{room.roomId}");
        }
    }
}
