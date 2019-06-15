using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
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
        public static Dictionary<string, string> originalLevels = new Dictionary<string, string>();

        public string songName;
        public string songSubName;
        public string authorName;
        public string key;
        public string levelId;
        public string hash;
        public float songDuration;

        public SongInfo()
        {

        }

        public static void GetOriginalLevelHashes()
        {
            originalLevels.Clear();

            foreach (var item in SongCore.Loader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).Where(y => !(y is CustomPreviewBeatmapLevel)))
            {
                originalLevels.Add(item.levelID, BitConverter.ToString(HexConverter.GetStringHashBytes(item.levelID)).Replace("-", ""));
            }
        }
        
        public SongInfo(IPreviewBeatmapLevel level)
        {
            songName = level.songName;
            songSubName = level.songSubName;
            authorName = level.levelAuthorName;
            
            key = "";
            //TODO: Uncomment and fix key detection
            //if (level is CustomLevel)
            //{
            //    Regex keyMatch = new Regex("\\d+-\\d+");
            //    var matches = keyMatch.Matches((level as CustomLevel).customSongInfo.path);
            //    if (matches.Count > 0)
            //    {
            //        key = matches[matches.Count - 1].Value;
            //    }
            //}

            levelId = level.levelID;

            if (originalLevels.ContainsKey(levelId))
            {
                hash = originalLevels[levelId];
            }
            else
            {
                hash = SongCore.Collections.hashForLevelID(levelId);
            }

            if (level is IBeatmapLevel)
            {
                songDuration = (level as IBeatmapLevel).beatmapLevelData.audioClip.length;
            }
            else
            {
                songDuration = level.songDuration;
            }
        }

        public SongInfo(NetIncomingMessage msg)
        {
            if (msg.LengthBytes > 20)
            {
                songName = msg.ReadString();
                songSubName = msg.ReadString();
                authorName = msg.ReadString();
                key = msg.ReadString();
                hash = BitConverter.ToString(msg.ReadBytes(20)).Replace("-", "");

                if (originalLevels.ContainsValue(hash))
                {
                    levelId = originalLevels.First(x => x.Value == hash).Key;
                }
                else
                {
                    levelId = SongCore.Collections.levelIDsForHash(hash).First();
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
            msg.Write(HexConverter.ConvertHexToBytesX(hash));
            msg.Write(songDuration);
        }

        public string GetSongKey()
        {
            //TODO: Uncomment and fix key detection
            //if (levelId.Length >= 32) {
            //    CustomLevel level = SongLoader.CustomLevels.FirstOrDefault(x => x.levelID.Contains(levelId));
            //    if (level != null)
            //    {
            //        Regex expression = new Regex(@"\d{1,}-\d{1,}");
            //        string match = expression.Match(level.customSongInfo.path).Value;
            //        if (string.IsNullOrEmpty(match))
            //        {
            //            Plugin.log.Warn("Unable to retrieve BeatSaver ID of the song from the path! Are you sure you are using the correct folder structure?");
            //            return "0-0";
            //        }
            //        else
            //        {
            //            return match;
            //        }
            //    }
            //    else
            //    {
            //        Plugin.log.Warn("Song with ID " +levelId+" not found!");
            //        return "0-0";
            //    }
            //}
            //else
            //{
            //    return "0-0";
            //}
            return "0-0";
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
