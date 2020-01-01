using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            BeatmapLevelsModel _levelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();

            foreach (var item in _levelsModel.ostAndExtrasPackCollection.beatmapLevelPacks.Concat(_levelsModel.dlcBeatmapLevelPackCollection.beatmapLevelPacks).SelectMany(x => x.beatmapLevelCollection.beatmapLevels))
            {
                originalLevels.Add(item.levelID, BitConverter.ToString(HexConverter.GetStringHashBytes(item.levelID)).Replace("-", ""));
            }
        }
        
        public SongInfo(IPreviewBeatmapLevel level)
        {
            songName = level.songName;
            songSubName = level.songSubName;
            authorName = level.songAuthorName;
            
            key = "";
            if (level is CustomPreviewBeatmapLevel)
            {
                string path = Path.GetFileName((level as CustomPreviewBeatmapLevel).customLevelPath);

                Regex keyMatch = new Regex(@"[0-9a-fA-F]+ \(.*\)");
                if (keyMatch.IsMatch(path))
                {
                    key = path.Substring(0, path.IndexOf(' '));
                }
            }

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
                    levelId = SongCore.Collections.levelIDsForHash(hash).FirstOrDefault();
                }

                if (string.IsNullOrEmpty(levelId))
                    levelId = hash;

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
        
        public void UpdateLevelId()
        {
            if (!string.IsNullOrEmpty(hash))
            {
                levelId = SongCore.Collections.levelIDsForHash(hash).FirstOrDefault();

                if (string.IsNullOrEmpty(levelId))
                    levelId = hash;
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
