using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Rooms
{
    public enum JoinResult : byte { Success, RoomNotFound, IncorrectPassword, TooMuchPlayers}

    static class RoomsController
    {
        static List<Room> rooms = new List<Room>();

        public static uint CreateRoom(RoomSettings settings, PlayerInfo host)
        {
            Room room = new Room(GetNextFreeID(), settings, host);
            rooms.Add(room);
            room.StartRoom();
#if DEBUG
            Logger.Instance.Log($"New room created! Settings: name={settings.Name}, password={settings.Password}, usePassword={settings.UsePassword}, maxPlayers={settings.MaxPlayers}, noFail={settings.NoFail}, songSelecionType={settings.SelectionType}, songsCount={settings.AvailableSongs.Count}");
#endif
            return room.roomId;
        }

        public static void DestroyRoom(PlayerInfo sender, uint roomId)
        {
#if DEBUG
            Logger.Instance.Log("Destroying room " + roomId);
#endif
            if (rooms.Any(x => x.roomId == roomId))
            {
                Room room = rooms.First(x => x.roomId == roomId);
                room.StopRoom();
                room.BroadcastPacket(new BasePacket(CommandType.LeaveRoom, new byte[0]));
                rooms.Remove(room);
            }
        }

        public static bool ClientJoined(Client client, uint roomId, string password)
        {
#if DEBUG
            Logger.Instance.Log("Client joining room " + roomId);
#endif
            if (rooms.Any(x => x.roomId == roomId))
            {
                Room room = rooms.First(x => x.roomId == roomId);
                RoomInfo roomInfo = room.GetRoomInfo();

                if (roomInfo.players < roomInfo.maxPlayers || roomInfo.maxPlayers == 0)
                {
                    if (roomInfo.usePassword)
                    {
                        if (room.roomSettings.Password == password)
                        {
                            client.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.Success }));
                            client.joinedRoomID = room.roomId;
                            room.roomClients.Add(client);
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
                        room.roomClients.Add(client);
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

        public static void ClientLeftRoom(Client client)
        {
            Room room = rooms.Find(x => x.roomClients.Contains(client));
            if (room != null)
            {
                room.roomClients.Remove(client);
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

        public static List<Room> GetRoomsList()
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
