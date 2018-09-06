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
    public struct WSRoomInfo
    {
        public string Path;
        public string RoomName;

        public WSRoomInfo(string _path)
        {
            Path = _path;

            string[] split = _path.Split('/');
            int _id = Convert.ToInt32(split[split.Length - 1]);

            Room _room = RoomsController.GetRoomsList().Find(room => room.roomId == _id);
            RoomName = _room.roomSettings.Name;
        }
    }

    public class ListServices : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();

            List<string> paths = WebSocketListener.Server.WebSocketServices.Paths.ToList();
            paths.Remove("/");

            List<WSRoomInfo> rooms = paths.ConvertAll(x => new WSRoomInfo(x));
            Send(JsonConvert.SerializeObject(rooms));
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

        public static void Start()
        {
            int webSocketPort = Settings.Instance.Server.WebSocketPort;
            Server = new WebSocketServer(webSocketPort);

            Logger.Instance.Log($"Hosting WebSocket Server @ {Program.GetPublicIPv4()}:{webSocketPort}");

            Server.AddWebSocketService<ListServices>("/");
            Server.Start();
        }

        public static void Stop()
        {
            if(Server != null)
                Server.Stop();
        }

        public static void BroadcastState()
        {
            if (Server != null)
            {
                List<string> paths = WebSocketListener.Server.WebSocketServices.Paths.ToList();
                paths.Remove("/");

                List<WSRoomInfo> rooms = paths.ConvertAll(x => new WSRoomInfo(x));
                string data = JsonConvert.SerializeObject(rooms);

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
