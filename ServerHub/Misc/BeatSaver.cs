using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServerHub.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ServerHub.Misc
{
    public static class BeatSaver
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
            public int Id { get; set; }
            public string Key { get; set; }
            public string Name { get; set; }
            public string HashMD5 { get; set; }
        }

        public static async Task<Song> FetchByID (string id)
        {
            using (WebClient w = new WebClient())
            {
                try
                {
                    string response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/detail/{id}");
                    JsonResponseDetails json = JsonConvert.DeserializeObject<JsonResponseDetails>(response);
                    return json.Song;
                    
                }
                catch(WebException e)
                {
                    Logger.Instance.Warning($"Unable to fetch song with key {id}! Error code: {(int)((HttpWebResponse)e.Response).StatusCode}");
                    return null;
                }
                catch (Exception e)
                {
#if DEBUG
                    Logger.Instance.Exception($"Unable to fetch song with key {id}! Exception: {e}");
#endif
                    return null;
                }
            }
        }

        public static async Task<SongInfo> InfoFromID(string id)
        {
            Song song = await FetchByID(id);
            if (song == null)
                return null;

            SongInfo info = new SongInfo() { levelId = song.HashMD5.ToUpper(), songName = song.Name };
            return info;
        }

        public static async Task<SongInfo> InfoFromHash(string id)
        {
            Song song = await FetchByHash(id);
            if (song == null)
                return null;

            SongInfo info = new SongInfo() { levelId = song.HashMD5.ToUpper(), songName = song.Name };
            return info;
        }


        public static List<SongInfo> ConvertSongIDs(List<string> ids)
        {
            List<SongInfo> songs = ids.AsParallel().WithDegreeOfParallelism(4).Select(id => { var task = InfoFromID(id); task.Wait(); return task.Result; }).ToList();
            return songs.Where(song => song != null).ToList();
        }

        public static async Task<SongInfo> GetRandomSong()
        {
            using (WebClient w = new WebClient())
            {
                Random rand = new Random();
                int maxId = 11000;
                try
                {
                    string response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/new");
                    JsonResponseSearch json = JsonConvert.DeserializeObject<JsonResponseSearch>(response);
                    if (json.Total > 0)
                    {
                        maxId = json.Songs[0].Id;
                    }

                    bool found = false;
                    do {
                        try
                        {
                            response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/detail/{rand.Next(1, maxId)}");
                            found = true;
                        }
                        catch (WebException wex)
                        {
                            if (((HttpWebResponse)wex.Response).StatusCode != HttpStatusCode.NotFound)
                            {
                                throw;
                            }
                        }
                    } while (!found);

                    JsonResponseDetails jsonDetails = JsonConvert.DeserializeObject<JsonResponseDetails>(response);
                    return new SongInfo() { levelId = jsonDetails.Song.HashMD5.ToUpper(), songName = jsonDetails.Song.Name };  
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

        public static async Task<Song> FetchByHash(string hash)
        {
            using (WebClient w = new WebClient())
            {
                try
                {
                    string response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/search/hash/{hash}");
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
            List<Song> songs = hashes.AsParallel().WithDegreeOfParallelism(4).Select(hash => { var task = FetchByHash(hash); task.Wait(); return task.Result; }).ToList();
            return songs.Where(song => song != null).ToList();
        }
    }
}
