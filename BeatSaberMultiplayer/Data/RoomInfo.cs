using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Data
{
    public enum RoomState: byte {SelectingSong, Preparing, InGame, Results }

    public class RoomInfo
    {
        public uint roomId;

        public string name;
        public bool usePassword;

        public RoomState roomState;
        public PlayerInfo roomHost;

        public int players;
        public int maxPlayers;

        public bool noFail;


        public RoomInfo()
        {

        }

        public RoomInfo(byte[] data)
        {
            if (data.Length > 25)
            {
                roomId = BitConverter.ToUInt32(data, 0);

                int nameLength = BitConverter.ToInt32(data, 4);
                name = Encoding.UTF8.GetString(data, 8, nameLength);

                usePassword = (data[8 + nameLength] == 0) ? false : true;

                roomState = (RoomState)data[9 + nameLength];

                int hostInfoLength = BitConverter.ToInt32(data, 10 + nameLength);
                roomHost = new PlayerInfo(data.Skip(14 + nameLength).Take(hostInfoLength).ToArray());

                players = BitConverter.ToInt32(data, 14 + nameLength + hostInfoLength);
                maxPlayers = BitConverter.ToInt32(data, 18 + nameLength + hostInfoLength);
                
                noFail = (data[22 + nameLength + hostInfoLength] == 0) ? false : true;
            }
        }

        public byte[] ToBytes(bool includeSize = true)
        {
            List<byte> buffer = new List<byte>();

            buffer.AddRange(BitConverter.GetBytes(roomId));

            byte[] nameStr = Encoding.UTF8.GetBytes(name);
            buffer.AddRange(BitConverter.GetBytes(nameStr.Length));
            buffer.AddRange(nameStr);

            buffer.Add(usePassword ? (byte)1 : (byte)0);

            buffer.Add((byte)roomState);

            byte[] hostInfo = roomHost.ToBytes();
            buffer.AddRange(hostInfo);

            buffer.AddRange(BitConverter.GetBytes(players));
            buffer.AddRange(BitConverter.GetBytes(maxPlayers));
            
            buffer.Add(noFail ? (byte)1 : (byte)0);
            
            if(includeSize)
                buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
        }

        public override bool Equals(object obj)
        {
            if (obj is RoomInfo)
            {
                return (name == ((RoomInfo)obj).name) && (usePassword == ((RoomInfo)obj).usePassword) && (players == ((RoomInfo)obj).players) && (maxPlayers == ((RoomInfo)obj).maxPlayers) && (noFail == ((RoomInfo)obj).noFail) && (roomHost.Equals(((RoomInfo)obj).roomHost));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = 1133574822;
            hashCode = hashCode * -1521134295 + roomId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name);
            hashCode = hashCode * -1521134295 + usePassword.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<PlayerInfo>.Default.GetHashCode(roomHost);
            hashCode = hashCode * -1521134295 + players.GetHashCode();
            hashCode = hashCode * -1521134295 + maxPlayers.GetHashCode();
            hashCode = hashCode * -1521134295 + noFail.GetHashCode();
            return hashCode;
        }
    }

}
