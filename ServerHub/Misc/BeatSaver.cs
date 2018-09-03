using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Linq;
using ServerHub.Data;
using Newtonsoft.Json;

namespace ServerHub.Misc
{
    static class BeatSaver
    {
        static readonly string BeatSaverAPI = "https://beatsaver.com/api/songs/detail";

        public class JsonResponse
        {
            public Song Song { get; set; }
        }

        public class Song
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public string HashMD5 { get; set; }
        }

        public static Song FetchByID (string id)
        {
            using (WebClient w = new WebClient())
            {
                try
                {
                    string response = w.DownloadString($"{BeatSaverAPI}/{id}");
                    JsonResponse json = JsonConvert.DeserializeObject<JsonResponse>(response);
                    return json.Song;
                    
                }
                catch (Exception e)
                {
#if DEBUG
                    Logger.Instance.Exception(e);
#endif
                    return null;
                }
            }
        }

        public static SongInfo InfoFromID(string id)
        {
            Song song = FetchByID(id);
            if (song == null)
                return null;

            SongInfo info = new SongInfo() { levelId = song.HashMD5.ToUpper(), songName = song.Name };
            return info;
        }

        public static List<SongInfo> ConvertSongIDs(List<string> ids)
        {
            List<SongInfo> songs = ids.ConvertAll(id => InfoFromID(id));
            return songs.Where(song => song != null).ToList();
        }
    }
}
