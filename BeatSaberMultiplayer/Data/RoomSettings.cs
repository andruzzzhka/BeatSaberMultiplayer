using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BeatSaberMultiplayer.Data
{
    public class RoomSettings
    {
        public string Name;

        public bool UsePassword;
        public string Password;

        public SongSelectionType SelectionType;
        public int MaxPlayers;
        public bool NoFail;

        public List<SongInfo> availableSongs;

        public RoomSettings()
        {

        }

        public RoomSettings(byte[] data)
        {
            if (data.Length > 14)
            {
                int nameLength = BitConverter.ToInt32(data, 0);
                Name = Encoding.UTF8.GetString(data, 4, nameLength);

                UsePassword = (data[4 + nameLength] == 0) ? false : true;

                int passLength = BitConverter.ToInt32(data, 5 + nameLength);
                Password = Encoding.UTF8.GetString(data, 9 + nameLength, passLength);

                MaxPlayers = BitConverter.ToInt32(data, 9 + nameLength + passLength);

                SelectionType = (SongSelectionType)(data[13 + nameLength + passLength]);
                NoFail = (data[14 + nameLength + passLength] == 0) ? false : true;

                int songsCount = BitConverter.ToInt32(data, 15 + nameLength + passLength);

                Stream byteStream = new MemoryStream(data, 19 + nameLength + passLength, data.Length - (19 + nameLength + passLength));

                availableSongs = new List<SongInfo>();
                for (int j = 0; j < songsCount; j++)
                {
                    byte[] sizeBytes = new byte[4];
                    byteStream.Read(sizeBytes, 0, 4);

                    int songInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                    byte[] songInfoBytes = new byte[songInfoSize];
                    byteStream.Read(songInfoBytes, 0, songInfoSize);

                    availableSongs.Add(new SongInfo(songInfoBytes));
                }
            }
        }

        public byte[] ToBytes(bool includeSize = true)
        {
            List<byte> buffer = new List<byte>();

            byte[] nameStr = Encoding.UTF8.GetBytes(Name);
            buffer.AddRange(BitConverter.GetBytes(nameStr.Length));
            buffer.AddRange(nameStr);

            buffer.Add(UsePassword ? (byte)1 : (byte)0);

            byte[] passStr = Encoding.UTF8.GetBytes(Password);
            buffer.AddRange(BitConverter.GetBytes(passStr.Length));
            buffer.AddRange(passStr);

            buffer.AddRange(BitConverter.GetBytes(MaxPlayers));

            buffer.Add((byte)SelectionType);
            buffer.Add(NoFail ? (byte)1 : (byte)0);

            buffer.AddRange(BitConverter.GetBytes(availableSongs.Count));
            availableSongs.ForEach(x => buffer.AddRange(x.ToBytes()));

            if (includeSize)
                buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
        }

        public override bool Equals(object obj)
        {
            if (obj is RoomSettings)
            {
                return (Name == ((RoomSettings)obj).Name) && (UsePassword == ((RoomSettings)obj).UsePassword) && (Password == ((RoomSettings)obj).Password) && (MaxPlayers == ((RoomSettings)obj).MaxPlayers) && (NoFail == ((RoomSettings)obj).NoFail);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = -1123100830;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + UsePassword.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Password);
            hashCode = hashCode * -1521134295 + MaxPlayers.GetHashCode();
            hashCode = hashCode * -1521134295 + NoFail.GetHashCode();
            return hashCode;
        }
    }
}
