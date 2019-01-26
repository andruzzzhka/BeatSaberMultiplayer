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
    public class RoomPreset
    {
        public RoomSettings settings;

        [NonSerialized]
        private string path;

        public RoomPreset()
        {

        }

        public RoomPreset(RoomSettings roomSettings)
        {
            settings = roomSettings;
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
            return settings;
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
