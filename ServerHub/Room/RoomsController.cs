using ServerHub.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServerHub.Room
{
    static class RoomsController
    {
        static List<Room> rooms = new List<Room>();

        public static void CreateRoom(RoomSettings settings, PlayerInfo host)
        {
            Room room = new Room(settings, host);
            rooms.Add(room);
            room.StartRoom();
        }

        public static byte[] GetRoomsListInBytes()
        {
            List<byte> buffer = new List<byte>();

            buffer.AddRange(BitConverter.GetBytes(rooms.Count));

            rooms.ForEach(x => buffer.AddRange(x.GetRoomInfo().ToBytes()));

            return buffer.ToArray();
        }

    }
}
