using ServerHub.Rooms;
using System;

namespace ServerHub.Data
{
    [Serializable]
    class RoomPreset
    {
        public RoomSettings settings;
        
        public RoomPreset()
        {

        }

        public RoomPreset(BaseRoom room)
        {
            settings = room.roomSettings;
        }

        public RoomSettings GetRoomSettings()
        {
            return settings;
        }
    }
}
