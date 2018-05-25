using Newtonsoft.Json;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BeatSaberMultiplayerServer
{
    class SongLoader
    {

        public static List<CustomSongInfo> RetrieveAllSongs()
        {
            var customSongInfos = new List<CustomSongInfo>();
            var path = Environment.CurrentDirectory;
            path = path.Replace('\\', '/');

            var currentHashes = new List<string>();

            var songFolders = Directory.GetDirectories(path + "/AvailableSongs").ToList();

            foreach (var song in songFolders)
            {
                var results = Directory.GetFiles(song, "info.json", SearchOption.AllDirectories);
                if (results.Length == 0)
                {
                    continue;
                }

                foreach (var result in results)
                {
                    var songPath = Path.GetDirectoryName(result).Replace('\\', '/');
                    var customSongInfo = GetCustomSongInfo(songPath);
                    if (customSongInfo == null) continue;
                    customSongInfos.Add(customSongInfo);
                }
            }

            return customSongInfos;
        }

        private static CustomSongInfo GetCustomSongInfo(string songPath)
        {
            var infoText = File.ReadAllText(songPath + "/info.json");
            CustomSongInfo songInfo;
            try
            {
                songInfo = JsonConvert.DeserializeObject<CustomSongInfo>(infoText);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error parsing song: " + songPath);
                return null;
            }

            songInfo.path = songPath;

            //Here comes SimpleJSON to the rescue when JSONUtility can't handle an array.
            var diffLevels = new List<CustomSongInfo.DifficultyLevel>();
            var n = JSON.Parse(infoText);
            var diffs = n["difficultyLevels"];
            for (int i = 0; i < diffs.AsArray.Count; i++)
            {
                n = diffs[i];
                diffLevels.Add(new CustomSongInfo.DifficultyLevel()
                {
                    difficulty = n["difficulty"],
                    difficultyRank = n["difficultyRank"].AsInt,
                    audioPath = n["audioPath"],
                    jsonPath = n["jsonPath"]
                });
            }

            songInfo.difficultyLevels = diffLevels.ToArray();
            songInfo.levelId = songInfo.GetIdentifier();

            return songInfo;
        }
    }
}
