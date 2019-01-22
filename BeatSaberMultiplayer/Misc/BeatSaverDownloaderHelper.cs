using BeatSaverDownloader.Misc;
using BeatSaverDownloader.UI;
using IllusionPlugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BeatSaberMultiplayer.Misc
{
    public enum VoteType { Upvote, Downvote, None};

    public struct SongVote
    {
        public string key;
        [JsonConverter(typeof(StringEnumConverter))]
        public VoteType voteType;

        public SongVote(string key, VoteType voteType)
        {
            this.key = key;
            this.voteType = voteType;
        }
    }

    static class BeatSaverDownloaderHelper
    {
        public static Dictionary<string, SongVote> votedSongs;
        public static List<string> favoriteSongs = new List<string>();
        public static string apiAccessToken { get; private set; }

        static public string apiTokenPlaceholder = "replace-this-with-your-api-token";

        public static void ReloadConfig()
        {
            PluginConfig.LoadOrCreateConfig();
            SongListTweaks.Instance.AddDefaultPlaylists();
        }

        public static VoteType GetVoteForSong(string levelId)
        {
            if (votedSongs.ContainsKey(levelId.Substring(0, 32)))
            {
                SongVote song = votedSongs[levelId.Substring(0, 32)];
                return song.voteType;
            }
            else
                return VoteType.None;
        }

        public static void LoadDownloaderConfig()
        {
            if (File.Exists("UserData\\votedSongs.json"))
            {
                votedSongs = JsonConvert.DeserializeObject<Dictionary<string, SongVote>>(File.ReadAllText("UserData\\votedSongs.json", Encoding.UTF8));
            }

            if (File.Exists("UserData\\favoriteSongs.cfg"))
            {
                favoriteSongs.Clear();
                favoriteSongs.AddRange(File.ReadAllLines("UserData\\favoriteSongs.cfg", Encoding.UTF8));
            }

            if (ModPrefs.HasKey("BeatSaverDownloader", "apiAccessToken"))
            {
                apiAccessToken = ModPrefs.GetString("BeatSaverDownloader", "apiAccessToken");
            }
        }

        public static void SaveDownloaderConfig()
        {
            File.WriteAllText("UserData\\votedSongs.json", JsonConvert.SerializeObject(votedSongs, Formatting.Indented), Encoding.UTF8);
            File.WriteAllLines("UserData\\favoriteSongs.cfg", favoriteSongs, Encoding.UTF8);

            ReloadConfig();
        }
    }
}
