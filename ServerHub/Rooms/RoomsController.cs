using ServerHub.Hub;
using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            Logger.Instance.Log($"New room created! Host={host.playerName}({host.playerId}) Settings: name={settings.Name}, password={settings.Password}, usePassword={settings.UsePassword}, maxPlayers={settings.MaxPlayers}, noFail={settings.NoFail}, songSelecionType={settings.SelectionType}, songsCount={settings.AvailableSongs.Count}");
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
            Logger.Instance.Log($"New room created! Settings: name={settings.Name}, password={settings.Password}, usePassword={settings.UsePassword}, maxPlayers={settings.MaxPlayers}, noFail={settings.NoFail}, songSelecionType={settings.SelectionType}, songsCount={settings.AvailableSongs.Count}");
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

                List<byte> buffer = new List<byte>();
                byte[] reasonText;
                if (string.IsNullOrEmpty(reason))
                    reasonText = Encoding.UTF8.GetBytes("Room destroyed!");
                else
                    reasonText = Encoding.UTF8.GetBytes(reason);
                buffer.AddRange(BitConverter.GetBytes(reasonText.Length));
                buffer.AddRange(reasonText);

                room.BroadcastPacket(new BasePacket(CommandType.Disconnect, buffer.ToArray()));
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
#if DEBUG
            Logger.Instance.Log("Client joining room " + roomId);
#endif
            if (rooms.Any(x => x.roomId == roomId))
            {
                BaseRoom room = rooms.First(x => x.roomId == roomId);
                RoomInfo roomInfo = room.GetRoomInfo();

                if (roomInfo.players < roomInfo.maxPlayers || roomInfo.maxPlayers == 0)
                {
                    if (roomInfo.usePassword)
                    {
                        if (room.roomSettings.Password == password)
                        {
                            client.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.Success }));
                            client.joinedRoomID = room.roomId;
                            if (room.roomClients.Any(x => x.playerInfo == client.playerInfo))
                            {
                                Client loggedIn = room.roomClients.Find(x => x.playerInfo == client.playerInfo);
                                if (loggedIn.socket.RemoteEndPoint != client.socket.RemoteEndPoint)
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
                            client.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.IncorrectPassword }));
                            return false;
                        }

                    }
                    else
                    {
                        client.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.Success }));
                        client.joinedRoomID = room.roomId;
                        AddClient(room, client);
                        return true;
                    }
                }
                else
                {
                    client.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.TooMuchPlayers }));
                    return false;
                }
            }
            else
            {
                client.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.RoomNotFound}));
                return false;
            }
        } 

        public static void AddClient(BaseRoom room, Client client)
        {
            if (room.GetRoomInfo().players == 0 && room.noHost)
                room.ForceTransferHost(client.playerInfo);

            room.roomClients.Add(client);
        }

        public static void ClientLeftRoom(Client client)
        {
            BaseRoom room = rooms.Find(x => x.roomId == client.joinedRoomID);
            if (room != null)
            {
                room.PlayerLeft(client);
            }
            client.joinedRoomID = 0;
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

        public static byte[] GetRoomsListInBytes()
        {
            List<byte> buffer = new List<byte>();

            buffer.AddRange(BitConverter.GetBytes(rooms.Count));

            rooms.ForEach(x => buffer.AddRange(x.GetRoomInfo().ToBytes()));

            return buffer.ToArray();
        }


        public static uint GetNextFreeID()
        {
            return (rooms.Count > 0) ? rooms.Last().roomId + 1 : 1;
        }
    }

}
