using System.Collections.Generic;

namespace ServerHub.Data
{
    public class PlaylistSong
    {
        public string key { get; set; }
        public string songName { get; set; }
        public string hash { get; set; }
        public string levelId { get; set; }
    }

    public class Playlist
    {
        public string playlistTitle { get; set; }
        public string playlistAuthor { get; set; }
        public string image { get; set; }
        public List<PlaylistSong> songs { get; set; }
        public string fileLoc { get; set; }
    }
}
