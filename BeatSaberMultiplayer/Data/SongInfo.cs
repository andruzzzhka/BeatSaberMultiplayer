using BeatSaberMultiplayer.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeatSaberMultiplayer.Data
{
    public class SongInfo
    {
        public string songName;
        public string levelId;
        public float songDuration;

        public SongInfo()
        {

        }

        public SongInfo(byte[] data)
        {
            if(data.Length > 23)
            {
                int nameLength = BitConverter.ToInt32(data, 0);
                songName = Encoding.UTF8.GetString(data, 4, nameLength);

                levelId = BitConverter.ToString(data.Skip(4 + nameLength).Take(16).ToArray()).Replace("-","");

                songDuration = BitConverter.ToSingle(data, 20+nameLength);
            }
        }

        public byte[] ToBytes(bool includeSize = true)
        {
            List<byte> buffer = new List<byte>();

            byte[] nameBuffer = Encoding.UTF8.GetBytes(songName);
            buffer.AddRange(BitConverter.GetBytes(nameBuffer.Length));
            buffer.AddRange(nameBuffer);

            buffer.AddRange(HexConverter.ConvertHexToBytesX(levelId));

            buffer.AddRange(BitConverter.GetBytes(songDuration));

            if (includeSize)
                buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
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
