namespace BeatSaberMultiplayerServer
{
    using Newtonsoft.Json;
    using SimpleJSON;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using static BeatSaberMultiplayerServer.CustomSongInfo;

    class SongLoader
    {
        private static List<string> SongFolderPaths =>
            Directory
                .GetDirectories(Environment.CurrentDirectory.Replace('\\', '/') + "/AvailableSongs")
                .ToList();

        public static List<CustomSongInfo> RetrieveAllSongs() =>
            SongFolderPaths
                .Where(songFolderPath => GetSongInfoFilePaths(songFolderPath).Length > 0)
                .Select(songFolderPath => GetSongInfoFilePaths(songFolderPath))
                .SelectMany(songInfoFilePaths => songInfoFilePaths)
                .Select(songInfoFilePath => GetCustomSongInfo(GetSongInfoDirectoryName(songInfoFilePath)))
                .ToList();

        private static string[] GetSongInfoFilePaths(string songFolderPath) =>
            Directory.GetFiles(songFolderPath, "info.json", SearchOption.AllDirectories);

        private static string GetSongInfoText(string songPath) =>
            File.ReadAllText(songPath + "/info.json");

        private static string GetSongInfoDirectoryName(string songFolderPath) =>
            Path.GetDirectoryName(songFolderPath).Replace('\\', '/');

        private static CustomSongInfo GetCustomSongInfo(string songPath)
        {
            try
            {
                var customSongInfo = JsonConvert.DeserializeObject<CustomSongInfo>(GetSongInfoText(songPath));
                customSongInfo.path = songPath;
                customSongInfo.difficultyLevels = GetDifficultyLevels(GetSongInfoText(songPath));
                customSongInfo.levelId = customSongInfo.GetIdentifier();

                return customSongInfo;
            }
            catch (Exception)
            {
                Console.WriteLine("Error parsing song: " + songPath);
                return null;
            }
        }

        private static DifficultyLevel[] GetDifficultyLevels(string songInfoText)
        {
            //Here comes SimpleJSON to the rescue when JSONUtility can't handle an array.
            var difficultyLevels = new List<DifficultyLevel>();

            foreach(var difficultyLevel in JSON.Parse(songInfoText)["difficultyLevels"].AsArray)
            {
                difficultyLevels.Add(new DifficultyLevel()
                {
                    difficulty = difficultyLevel.Value["difficulty"],
                    difficultyRank = difficultyLevel.Value["difficultyRank"].AsInt,
                    audioPath = difficultyLevel.Value["audioPath"],
                    jsonPath = difficultyLevel.Value["jsonPath"]
                });
            }

            return difficultyLevels.ToArray();
        }
    }
}
