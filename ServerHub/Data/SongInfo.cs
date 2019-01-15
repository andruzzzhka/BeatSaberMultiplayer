using Lidgren.Network;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Data
{
    public class SongInfo
    {
        public string songName;
        public string levelId;
        public float songDuration;

        public SongInfo()
        {

        }

        public SongInfo(NetIncomingMessage msg)
        {
            songName = msg.ReadString();
            levelId = BitConverter.ToString(msg.ReadBytes(16)).Replace("-", "");
            songDuration = msg.ReadFloat();
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(songName);
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
