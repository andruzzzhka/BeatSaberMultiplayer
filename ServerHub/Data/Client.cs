using Lidgren.Network;
using ServerHub.Data;
using ServerHub.Hub;
using ServerHub.Misc;
using ServerHub.Rooms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;

namespace ServerHub.Data
{

    public class Client
    {
        public event Action<Client> ClientDisconnected;

        public NetConnection playerConnection;
        public PlayerInfo playerInfo;
        public DateTime joinTime;

        public uint joinedRoomID;

        public bool active;

        public Client(NetConnection playerConnection, PlayerInfo playerInfo)
        {
            this.playerConnection = playerConnection;
            this.playerInfo = playerInfo;

            active = true;
            joinTime = DateTime.Now;
        }

        public void KickClient(string reason = "")
        {
            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
            outMsg.Write((byte)CommandType.Disconnect);
            outMsg.Write(reason);

            playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            Program.networkBytesOutNow += outMsg.LengthBytes;

            playerConnection.Disconnect(reason);

            ClientDisconnected?.Invoke(this);
            active = false;
        }
    }
}
