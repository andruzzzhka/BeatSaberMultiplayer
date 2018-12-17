using BeatSaberMultiplayer.Misc;
using Newtonsoft.Json;
using SimpleJSON;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BeatSaberMultiplayer.Data
{
    [Serializable]
    public class PresetSong
    {
        public string Key;
        public string Name;
        public string HashMD5;

        public PresetSong()
        {
        }

        public string GetHash()
        {
            if (SongLoader.AreSongsLoaded)
            {
                CustomLevel level = SongLoader.CustomLevels.FirstOrDefault(x => x.customSongInfo.path.Contains(Key));
                if (level != null)
                {
                    HashMD5 = level.levelID.Substring(0, Math.Min(32, level.levelID.Length));
                    return HashMD5;
                }
                else
                    return "";
            }
            else
                return ""; 
        }
    }

    [Serializable]
    public class RoomPreset
    {
        public RoomSettings settings;
        public List<PresetSong> songs;

        [NonSerialized]
        private string path;

        public RoomPreset()
        {

        }

        public RoomPreset(RoomSettings roomSettings)
        {
            settings = roomSettings;
            songs = roomSettings.AvailableSongs.ConvertAll(x => new PresetSong() { Name = x.songName, HashMD5 = x.levelId, Key = x.GetSongKey() });
        }

        public static RoomPreset LoadPreset(string path)
        {
            if (File.Exists(path))
            {
                string presetText = File.ReadAllText(path);

                RoomPreset preset = JsonConvert.DeserializeObject<RoomPreset>(presetText);
                preset.path = path;

                return preset;
            }
            else
            {
                return null;
            }
        }

        public string GetName()
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        public RoomSettings GetRoomSettings()
        {
            settings.AvailableSongs = songs.ConvertAll(x => new SongInfo() { levelId = (string.IsNullOrEmpty(x.HashMD5) ? x.GetHash() : x.HashMD5), songName = x.Name });

            return settings;
        }

        public void Update()
        {
            songs.Where(x => string.IsNullOrEmpty(x.HashMD5)).AsParallel().WithDegreeOfParallelism(4).ForAll(y => SongDownloader.Instance.RequestSongByKey(y.Key, UpdateSong));
        }

        public void UpdateSong(Song songInfo)
        {
            PresetSong presetSong = songs.FirstOrDefault(x => x.Key == songInfo.id);
            if(presetSong != null)
                presetSong.HashMD5 = songInfo.hash;

            if(songs.All(x => !string.IsNullOrEmpty(x.HashMD5)))
            {
                SavePreset();
            }
        }

        public void SavePreset()
        {
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(this));
            }
        }

        public void SavePreset(string newPath)
        {
            if (!string.IsNullOrEmpty(newPath))
            {
                File.WriteAllText(newPath, JsonConvert.SerializeObject(this));
                path = newPath;
            }
        }
    }
}
