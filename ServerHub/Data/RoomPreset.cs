using ServerHub.Misc;
using ServerHub.Rooms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static ServerHub.Misc.BeatSaver;

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
