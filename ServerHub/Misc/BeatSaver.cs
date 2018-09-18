using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServerHub.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace ServerHub.Misc
{
    static class BeatSaver
    {
        static readonly string BeatSaverAPI = "https://beatsaver.com/api/songs";

        public class JsonResponseDetails
        {
            public Song Song { get; set; }
        }

        public class JsonResponseSearch
        {
            public List<Song> Songs { get; set; }
            public int Total { get; set; }
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
                    string response = w.DownloadString($"{BeatSaverAPI}/detail/{id}");
                    JsonResponseDetails json = JsonConvert.DeserializeObject<JsonResponseDetails>(response);
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

            SongInfo info = new SongInfo() { levelId = song.HashMD5.ToUpper(), songName = "" };
            return info;
        }

        public static List<SongInfo> ConvertSongIDs(List<string> ids)
        {
            List<SongInfo> songs = ids.AsParallel().WithDegreeOfParallelism(4).Select(id => InfoFromID(id)).ToList();
            return songs.Where(song => song != null).ToList();
        }


        public static Song FetchByHash(string hash)
        {
            using (WebClient w = new WebClient())
            {
                try
                {
                    string response = w.DownloadString($"{BeatSaverAPI}/search/hash/{hash}");
                    JsonResponseSearch json = JsonConvert.DeserializeObject<JsonResponseSearch>(response);
                    if (json.Total > 0)
                    {
                        return json.Songs[0];
                    }
                    else
                    {
                        Logger.Instance.Log($"Song wih hash {hash} not found!");
                        return null;
                    }
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

        public static List<Song> ConvertSongHashes(List<string> hashes)
        {
            List<Song> songs = hashes.AsParallel().WithDegreeOfParallelism(4).Select(hash => FetchByHash(hash)).ToList();
            return songs.Where(song => song != null).ToList();
        }
    }
}
