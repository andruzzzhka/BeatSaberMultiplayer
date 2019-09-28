using Newtonsoft.Json;
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
        static readonly string BeatSaverAPI = "https://beatsaver.com/api/maps";

        public class JsonResponseSearch
        {
            [JsonProperty("docs")]
            public Song[] Songs { get; set; }
            [JsonProperty("totalDocs")]
            public long TotalDocs { get; set; }
        }

        public class Song
        {
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("_id")]
            public string Id { get; set; }
            [JsonProperty("key")]
            public string Key { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("hash")]
            public string Hash { get; set; }
            [JsonProperty("downloadURL")]
            public string DownloadUrl { get; set; }
            [JsonProperty("coverURL")]
            public string CoverUrl { get; set; }
        }

        public static async Task<Song> FetchByID (string id)
        {
            using (WebClient w = new WebClient())
            {
                try
                {
                    string response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/detail/{id}");
                    Song json = JsonConvert.DeserializeObject<Song>(response);
                    return json;
                    
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

            SongInfo info = new SongInfo() { levelId = song.Hash.ToUpper(), songName = song.Name, key = song.Key };
            return info;
        }

        public static async Task<SongInfo> InfoFromHash(string id)
        {
            Song song = await FetchByHash(id);
            if (song == null)
                return null;

            SongInfo info = new SongInfo() { levelId = song.Hash.ToUpper(), songName = song.Name, key = song.Key };
            return info;
        }


        public static async Task<List<SongInfo>> ConvertSongIDsAsync(List<string> ids)
        {
            List<SongInfo> songs = new List<SongInfo>();
            foreach (string  id  in ids)
            {
                songs.Add(await InfoFromID(id));
            }

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
                    string response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/latest");
                    JsonResponseSearch json = JsonConvert.DeserializeObject<JsonResponseSearch>(response);
                    if (json.Songs.Length > 0)
                    {
                        maxId = Convert.ToInt32(json.Songs[0].Key, 16);
                    }

                    bool found = false;
                    do {
                        try
                        {
                            response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/detail/{rand.Next(1, maxId).ToString("x")}");
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

                    Song jsonSong = JsonConvert.DeserializeObject<Song>(response);
                    return new SongInfo() { levelId = jsonSong.Hash.ToUpper(), songName = jsonSong.Name, key = jsonSong.Key };  
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
                    string response = await w.DownloadStringTaskAsync($"{BeatSaverAPI}/by-hash/{hash.ToLower()}");
                    Song json = JsonConvert.DeserializeObject<Song>(response);
                    if (json != null)
                    {
                        return json;
                    }
                    else
                    {
                        Logger.Instance.Log($"Song with hash {hash} not found!");
                        return null;
                    }
                }
                catch (WebException wex)
                {
                    if (((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        Logger.Instance.Log($"Song with hash {hash} not found!");
                    }
                    else
                    {
                        Logger.Instance.Exception($"Unable to fetch song by hash! Status code: {(int)((HttpWebResponse)wex.Response).StatusCode}({((HttpWebResponse)wex.Response).StatusCode})");
                    }
                    return null;
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
