using Lidgren.Network;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServerHub.Data
{
    public class SongInfo
    {
        public string key;

        public string songName;
        public string songSubName;
        public string authorName;
        public string levelId;
        public float songDuration;

        public SongInfo()
        {

        }

        public SongInfo(NetIncomingMessage msg)
        {
            songName = msg.ReadString();
            songSubName = msg.ReadString();
            authorName = msg.ReadString();
            key = msg.ReadString();
            levelId = BitConverter.ToString(msg.ReadBytes(20)).Replace("-", "");
            songDuration = msg.ReadFloat();

            if (string.IsNullOrEmpty(key))
            {
                Task.Run(async () =>
                {
                    var song = await BeatSaver.FetchByHash(levelId);
                    key = song.Key;
                });
            }
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(songName);
            msg.Write(songSubName);
            msg.Write(authorName);
            msg.Write(key);
            msg.Write(HexConverter.ConvertHexToBytesX(levelId));
            msg.Write(songDuration);
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
