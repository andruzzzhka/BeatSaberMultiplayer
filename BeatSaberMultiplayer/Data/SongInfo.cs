using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
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
        public static Dictionary<string, BeatmapLevelSO> originalLevels = new Dictionary<string, BeatmapLevelSO>();

        public string songName;
        public string songSubName;
        public string authorName;
        public string key;
        public string levelId;
        public float songDuration;

        public SongInfo()
        {

        }

        public static void GetOriginalLevelHashes()
        {
            originalLevels.Clear();

            foreach (var item in SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).Where(y => y.levelID.Length < 32 && y is BeatmapLevelSO))
            {
                originalLevels.Add(BitConverter.ToString(HexConverter.GetStringHashBytes(item.levelID)).Replace("-", ""), (BeatmapLevelSO)item);
            }
        }
        
        public SongInfo(BeatmapLevelSO level)
        {
            songName = level.songName;
            songSubName = level.songSubName;
            authorName = level.levelAuthorName;
            
            key = "";
            if (level is CustomLevel)
            {
                Regex keyMatch = new Regex("\\d+-\\d+");
                var matches = keyMatch.Matches((level as CustomLevel).customSongInfo.path);
                if (matches.Count > 0)
                {
                    key = matches[matches.Count - 1].Value;
                }
            }
            
            levelId = level.levelID.Substring(0, Math.Min(32, level.levelID.Length));
            
            if (originalLevels.ContainsKey(levelId))
            {
                levelId = originalLevels[levelId].levelID;
            }
            if (level.beatmapLevelData == null || level.beatmapLevelData.audioClip == null)
            {
                songDuration = 0f;
            }
            else
            {
                songDuration = level.beatmapLevelData.audioClip.length;
            }
        }

        public SongInfo(NetIncomingMessage msg)
        {
            if (msg.LengthBytes > 16)
            {
                songName = msg.ReadString();
                songSubName = msg.ReadString();
                authorName = msg.ReadString();
                key = msg.ReadString();
                levelId = BitConverter.ToString(msg.ReadBytes(16)).Replace("-", "");

                if (originalLevels.ContainsKey(levelId))
                {
                    levelId = originalLevels[levelId].levelID;
                }

                songDuration = msg.ReadFloat();
            }
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(songName);
            msg.Write(songSubName);
            msg.Write(authorName);
            msg.Write(key);

            if (levelId.Length >= 32)
            {
                msg.Write(HexConverter.ConvertHexToBytesX(levelId));
            }
            else
            {
                msg.Write(HexConverter.GetStringHashBytes(levelId));
            }
            msg.Write(songDuration);
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
                        Plugin.log.Warn("Unable to retrieve BeatSaver ID of the song from the path! Are you sure you are using the correct folder structure?");
                        return "0-0";
                    }
                    else
                    {
                        return match;
                    }
                }
                else
                {
                    Plugin.log.Warn("Song with ID " +levelId+" not found!");
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
