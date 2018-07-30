using System;
using System.Collections.Generic;
using System.Text;

namespace ServerHub.Data
{
    enum RoomState: byte {WaitingForHost,  }

    public class RoomInfo
    {
        public uint roomId;

        public string name;
        public bool usePassword;

        public int players;
        public int maxPlayers;

        public bool noFail;


        public RoomInfo()
        {

        }

        public RoomInfo(byte[] data)
        {
            if (data.Length > 17)
            {
                roomId = BitConverter.ToUInt32(data, 0);

                int nameLength = BitConverter.ToInt32(data, 4);
                name = Encoding.UTF8.GetString(data, 8, nameLength);

                usePassword = (data[8 + nameLength] == 0) ? false : true;

                players = BitConverter.ToInt32(data, 9 + nameLength);
                maxPlayers = BitConverter.ToInt32(data, 13 + nameLength);
                
                noFail = (data[17 + nameLength] == 0) ? false : true;
            }
        }

        public byte[] ToBytes()
        {
            List<byte> buffer = new List<byte>();

            buffer.AddRange(BitConverter.GetBytes(roomId));

            byte[] nameStr = Encoding.UTF8.GetBytes(name);
            buffer.AddRange(BitConverter.GetBytes(nameStr.Length));
            buffer.AddRange(nameStr);

            buffer.Add(usePassword ? (byte)1 : (byte)0);

            buffer.AddRange(BitConverter.GetBytes(players));
            buffer.AddRange(BitConverter.GetBytes(maxPlayers));
            
            buffer.Add(noFail ? (byte)1 : (byte)0);
            
            buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
        }

        public override bool Equals(object obj)
        {
            if (obj is RoomInfo)
            {
                return (name == ((RoomInfo)obj).name) && (usePassword == ((RoomInfo)obj).usePassword) && (players == ((RoomInfo)obj).players) && (maxPlayers == ((RoomInfo)obj).maxPlayers) && (noFail == ((RoomInfo)obj).noFail);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = -1322469166;
            hashCode = hashCode * -1521134295 + roomId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name);
            hashCode = hashCode * -1521134295 + usePassword.GetHashCode();
            hashCode = hashCode * -1521134295 + players.GetHashCode();
            hashCode = hashCode * -1521134295 + maxPlayers.GetHashCode();
            hashCode = hashCode * -1521134295 + noFail.GetHashCode();
            return hashCode;
        }
    }

}
