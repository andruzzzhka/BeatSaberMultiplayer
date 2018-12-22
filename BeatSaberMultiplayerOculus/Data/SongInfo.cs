using BeatSaberMultiplayer.Misc;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BeatSaberMultiplayer.Data
{
    public class SongInfo
    {
        public static Dictionary<string, LevelSO> originalLevels = new Dictionary<string, LevelSO>();

        public string songName;
        public string levelId;
        public float songDuration;

        public SongInfo()
        {

        }

        public static void GetOriginalLevelHashes()
        {
            originalLevels.Clear();

            foreach (var item in Resources.FindObjectsOfTypeAll<LevelCollectionSO>().First().GetLevelsWithBeatmapCharacteristic(Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>().First(x => x.characteristicName == "Standard")).Where(y => y.levelID.Length < 32))
            {
                originalLevels.Add(BitConverter.ToString(HexConverter.GetStringHashBytes(item.levelID)).Replace("-", ""), item);
            }
        }

        public SongInfo(byte[] data)
        {
            if(data.Length > 23)
            {
                int nameLength = BitConverter.ToInt32(data, 0);
                songName = Encoding.UTF8.GetString(data, 4, nameLength);
                
                levelId = BitConverter.ToString(data.Skip(4 + nameLength).Take(16).ToArray()).Replace("-", "");

                if (originalLevels.ContainsKey(levelId))
                {
                    levelId = originalLevels[levelId].levelID;
                }

                /*
                if (data.Skip(5 + nameLength).Take(15).Max() == 0)
                {
                    levelId = originalLevels.First(x => x.Value == data[4 + nameLength]).Key;
                }
                else
                {
                    levelId = BitConverter.ToString(data.Skip(4 + nameLength).Take(16).ToArray()).Replace("-", "");
                }
                */

                songDuration = BitConverter.ToSingle(data, 20+nameLength);
            }
        }

        public byte[] ToBytes(bool includeSize = true)
        {
            List<byte> buffer = new List<byte>();

            byte[] nameBuffer = Encoding.UTF8.GetBytes(songName);
            buffer.AddRange(BitConverter.GetBytes(nameBuffer.Length));
            buffer.AddRange(nameBuffer);

            if (levelId.Length >= 32)
            {
                buffer.AddRange(HexConverter.ConvertHexToBytesX(levelId));
            }
            else
            {
                buffer.AddRange(HexConverter.GetStringHashBytes(levelId));
            }

            /*
            if (originalLevels.ContainsKey(levelId))
            {
                buffer.Add(originalLevels[levelId]);
                buffer.AddRange(new byte[15]);
            }
            else
            {
                buffer.AddRange(HexConverter.ConvertHexToBytesX(levelId));
            }
            */

            buffer.AddRange(BitConverter.GetBytes(songDuration));

            if (includeSize)
                buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
        }

        public string GetSongKey()
        {
            if (levelId.Length >= 32) {
                CustomLevel level = SongLoader.CustomLevels.FirstOrDefault(x => x.levelID.Contains(levelId));
                if (level != null)
                {
                    Regex expression = new Regex(@"\d{1,}-\d{1,}");
                    string match = expression.Match(level.customSongInfo.path).Value;
                    if (string.IsNullOrEmpty(match))
                    {
                        Misc.Logger.Warning("Unable to retrieve BeatSaver ID of the song from the path! Are you sure you are using the correct folder structure?");
                        return "0-0";
                    }
                    else
                    {
                        return match;
                    }
                }
                else
                {
                    Misc.Logger.Warning("Song with id "+levelId+" not found!");
                    return "0-0";
                }
            }
            else
            {
                return "0-0";
            }
        }

        public override bool Equals(object obj)
        {
            if(obj is SongInfo)
            {
                return levelId == (obj as SongInfo).levelId;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = -1413302877;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(levelId);
            return hashCode;
        }
    }
}
