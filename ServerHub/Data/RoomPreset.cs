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
        public List<Song> songs;
        
        public RoomPreset()
        {

        }

        public RoomPreset(Room room)
        {
            settings = room.roomSettings;
            songs = ConvertSongHashes(settings.AvailableSongs.ConvertAll(x => x.levelId));
        }

        public RoomSettings GetRoomSettings()
        {
            settings.AvailableSongs = songs.ConvertAll(x => new SongInfo() { levelId = x.HashMD5, songName = x.Name });

            return settings;
        }

        public void Update()
        {
            songs.Where(x => string.IsNullOrEmpty(x.HashMD5)).AsParallel().WithDegreeOfParallelism(4).ForAll(y => y.HashMD5 = FetchByID(y.Key).HashMD5);
        }
    }
}
