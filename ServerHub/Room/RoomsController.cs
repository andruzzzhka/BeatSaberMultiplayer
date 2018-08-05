using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Room
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

            Logger.Instance.Log($"New room created! Settings: name={settings.Name}, password={settings.Password}, usePassword={settings.UsePassword}, maxPlayers={settings.MaxPlayers}, noFail={settings.NoFail}");

            return room.roomId;
        }

        public static void ClientJoined(Client client, uint roomId, string password)
        {
            if(rooms.Any(x => x.roomId == roomId))
            {
                Room room = rooms.First(x => x.roomId == roomId);
                RoomInfo roomInfo = room.GetRoomInfo();

                if (roomInfo.players < roomInfo.maxPlayers || roomInfo.maxPlayers == 0)
                {
                    if (roomInfo.usePassword)
                    {
                        if (room.roomSettings.Password == password)
                        {
                            client.tcpClient.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.Success }));
                            room.roomClients.Add(client);
                        }
                        else
                        {
                            client.tcpClient.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.IncorrectPassword }));
                        }
                    }
                    else
                    {
                        client.tcpClient.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.Success }));
                    }
                }
                else
                {
                    client.tcpClient.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.TooMuchPlayers }));
                }
            }
            else
            {
                client.tcpClient.SendData(new BasePacket(CommandType.JoinRoom, new byte[1] { (byte)JoinResult.RoomNotFound}));
            }
        } 

        public static void ClientLeftRoom(Client client)
        {
            Room room = rooms.Find(x => x.roomClients.Contains(client));
            if (room != null)
            {
                room.roomClients.Remove(client);
            }
        }

        public static int GetRoomsCount()
        {
            return rooms.Count;
        }

        public static List<RoomInfo> GetRoomsList()
        {
            return rooms.Select(x => x.GetRoomInfo()).ToList();
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
