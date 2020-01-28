using ServerHub.Hub;
using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using Lidgren.Network;

namespace ServerHub.Rooms
{
    public enum JoinResult : byte { Success, RoomNotFound, IncorrectPassword, TooMuchPlayers}

    public static class RoomsController
    {
        public static Type roomClass = typeof(BaseRoom);

        static List<BaseRoom> rooms = new List<BaseRoom>();

        public static uint CreateRoom(RoomSettings settings, PlayerInfo host)
        {

            BaseRoom room = Activator.CreateInstance(roomClass, GetNextFreeID(), settings, host) as BaseRoom;
            rooms.Add(room);
            room.StartRoom();
            WebSocketListener.AddRoom(room);
#if DEBUG
            Logger.Instance.Log($"New room created! Host={host.playerName}({host.playerId}) Settings: name={settings.Name}, password={settings.Password}, usePassword={settings.UsePassword}, maxPlayers={settings.MaxPlayers}, songSelecionType={settings.SelectionType}");
#endif
            return room.roomId;
        }

        public static uint CreateRoom(RoomSettings settings)
        {
            BaseRoom room = Activator.CreateInstance(roomClass, GetNextFreeID(), settings, new PlayerInfo("server", long.MaxValue)) as BaseRoom;
            room.noHost = true;
            rooms.Add(room);
            room.StartRoom();
            WebSocketListener.AddRoom(room);
#if DEBUG
            Logger.Instance.Log($"New room created! Settings: name={settings.Name}, password={settings.Password}, usePassword={settings.UsePassword}, maxPlayers={settings.MaxPlayers}, songSelecionType={settings.SelectionType}");
#endif
            return room.roomId;
        }

        public static bool DestroyRoom(uint roomId, string reason = "")
        {
#if DEBUG
            Logger.Instance.Log("Destroying room " + roomId);
#endif
            if (rooms.Any(x => x.roomId == roomId))
            {
                BaseRoom room = rooms.First(x => x.roomId == roomId);
                room.StopRoom();
                WebSocketListener.DestroyRoom(room);

                if (string.IsNullOrEmpty(reason))
                    reason = "Room destroyed!";

                NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

                outMsg.Write((byte)CommandType.Disconnect);
                outMsg.Write(reason);

                room.BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                rooms.Remove(room);

                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool ClientJoined(Client client, uint roomId, string password)
        {

            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

            if (rooms.Any(x => x.roomId == roomId))
            {
                BaseRoom room = rooms.First(x => x.roomId == roomId);
                RoomInfo roomInfo = room.GetRoomInfo();

#if DEBUG
                Logger.Instance.Log($"Client joining room {roomId} {(roomInfo.usePassword ? $"with password \"{password}\" ({((room.roomSettings.Password == password) ? ("Correct") : ("Incorrect"))})": "")}");
#endif

                if (roomInfo.players < roomInfo.maxPlayers || roomInfo.maxPlayers == 0)
                {
                    if (roomInfo.usePassword)
                    {
                        if (room.roomSettings.Password == password)
                        {
                            outMsg.Write((byte)CommandType.JoinRoom);
                            outMsg.Write((byte)JoinResult.Success);

                            client.playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                            Program.networkBytesOutNow += outMsg.LengthBytes;

                            client.joinedRoomID = room.roomId;
                            if (room.roomClients.Any(x => x.playerInfo == client.playerInfo))
                            {
                                Client loggedIn = room.roomClients.Find(x => x.playerInfo == client.playerInfo);
                                if (loggedIn.playerConnection != client.playerConnection)
                                {
                                    loggedIn.KickClient("You logged in from another location");
                                }
                                else
                                {
                                    room.roomClients.Remove(loggedIn);
                                }
                            }
                            AddClient(room, client);
                            return true;
                        }
                        else
                        {
                            outMsg.Write((byte)CommandType.JoinRoom);
                            outMsg.Write((byte)JoinResult.IncorrectPassword);

                            client.playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                            Program.networkBytesOutNow += outMsg.LengthBytes;
                            return false;
                        }

                    }
                    else
                    {
                        outMsg.Write((byte)CommandType.JoinRoom);
                        outMsg.Write((byte)JoinResult.Success);

                        client.playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                        Program.networkBytesOutNow += outMsg.LengthBytes;

                        client.joinedRoomID = room.roomId;
                        if (room.roomClients.Any(x => x.playerInfo == client.playerInfo))
                        {
                            Client loggedIn = room.roomClients.Find(x => x.playerInfo.Equals(client.playerInfo));
                            if (!loggedIn.playerConnection.Equals(client.playerConnection))
                            {
                                loggedIn.KickClient("You logged in from another location");
                            }
                            else
                            {
                                room.roomClients.Remove(loggedIn);
                            }
                        }
                        AddClient(room, client);
                        return true;
                    }
                }
                else
                {
                    outMsg.Write((byte)CommandType.JoinRoom);
                    outMsg.Write((byte)JoinResult.TooMuchPlayers);

                    client.playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                    Program.networkBytesOutNow += outMsg.LengthBytes;
                    return false;
                }
            }
            else
            {
                outMsg.Write((byte)CommandType.JoinRoom);
                outMsg.Write((byte)JoinResult.RoomNotFound);

                client.playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                Program.networkBytesOutNow += outMsg.LengthBytes;
                return false;
            }
        } 

        public static void AddClient(BaseRoom room, Client client)
        {
            if (room.GetRoomInfo().players == 0 && room.noHost)
            {
                room.ForceTransferHost(client.playerInfo);
                room.noHost = false;
            }

            room.roomClients.Add(client);
        }

        public static void ClientLeftRoom(Client client)
        {
            foreach (BaseRoom room in rooms)
            {
                if (room.roomClients.Contains(client))
                {
                    room.PlayerLeft(client);
                    client.joinedRoomID = 0;
                    return;
                }
            }
        }

        public static int GetRoomsCount()
        {
            return rooms.Count;
        }

        public static List<RoomInfo> GetRoomInfosList()
        {
            return rooms.Select(x => x.GetRoomInfo()).ToList();
        }

        public static List<BaseRoom> GetRoomsList()
        {
            return rooms;
        }

        public static void AddRoomListToMessage(NetOutgoingMessage msg)
        {
            msg.Write(rooms.Count);

            rooms.ForEach(x => x.GetRoomInfo().AddToMessage(msg));
        }


        public static uint GetNextFreeID()
        {
            return (rooms.Count > 0) ? rooms.Last().roomId + 1 : 1;
        }
    }

}
