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

    public class Client : IEquatable<Client>
    {
        public event Action<Client> ClientDisconnected;

        public NetConnection playerConnection;
        public PlayerInfo playerInfo;
        public DateTime joinTime;

        public bool fullUpdate;

        public Queue<PlayerUpdate> playerUpdateHistory = new Queue<PlayerUpdate>(10);
        public List<HitData> playerHitsHistory = new List<HitData>(30);
        public Queue<UnityVOIP.VoipFragment> playerVoIPQueue = new Queue<UnityVOIP.VoipFragment>();

        public uint joinedRoomID;

        public bool active;

        public Client(NetConnection playerConnection, PlayerInfo playerInfo)
        {
            this.playerConnection = playerConnection;
            this.playerInfo = playerInfo;

            active = true;
            joinTime = DateTime.Now;
        }

        public void UpdatePlayerInfo(NetIncomingMessage msg)
        {
            if(msg.ReadByte() == 0)
            {
                playerInfo.updateInfo = new PlayerUpdate(msg);
                byte hitCount = msg.ReadByte();

                for(int i = 0; i < hitCount; i++)
                {
                    playerInfo.hitsLastUpdate.Add(new HitData(msg));
                }
                fullUpdate = false;
            }
            else
            {
                playerInfo = new PlayerInfo(msg);
                fullUpdate = true;
            }

            playerUpdateHistory.Enqueue(playerInfo.updateInfo);

            while (playerUpdateHistory.Count >= 10)
            {
                playerUpdateHistory.Dequeue();
            }

            playerHitsHistory.AddRange(playerInfo.hitsLastUpdate);

            if(playerHitsHistory.Count > 30)
            {
                playerHitsHistory.RemoveRange(0, playerHitsHistory.Count - 30);
            }
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

        public override bool Equals(object obj)
        {
            if(obj is Client)
            {
                return (((Client)obj).playerConnection.RemoteEndPoint.Equals(playerConnection.RemoteEndPoint));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return playerConnection.RemoteEndPoint.GetHashCode();
        }

        public bool Equals(Client other)
        {
            return other.playerConnection.RemoteEndPoint.Equals(playerConnection.RemoteEndPoint);
        }
    }
}
