using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Data
{
    public enum RoomState: byte {SelectingSong, Preparing, InGame, Results }
    public enum SongSelectionType : byte { Manual,  Random, Voting }

    public class RoomInfo
    {
        public uint roomId;

        public string name;
        public bool usePassword;

        public RoomState roomState;
        public SongSelectionType songSelectionType;
        public PlayerInfo roomHost;

        public int players;
        public int maxPlayers;

        public bool noFail;

        public byte selectedDifficulty;
        public SongInfo selectedSong;


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
                songSelectionType = (SongSelectionType)data[10 + nameLength];

                int hostInfoLength = BitConverter.ToInt32(data, 11 + nameLength);
                roomHost = new PlayerInfo(data.Skip(15 + nameLength).Take(hostInfoLength).ToArray());

                players = BitConverter.ToInt32(data, 15 + nameLength + hostInfoLength);
                maxPlayers = BitConverter.ToInt32(data, 19 + nameLength + hostInfoLength);
                
                noFail = (data[23 + nameLength + hostInfoLength] == 0) ? false : true;

                if(data.Length > 24 + nameLength + hostInfoLength)
                {
                    selectedDifficulty = data[24 + nameLength + hostInfoLength];
                    selectedSong = new SongInfo(data.Skip(25 + nameLength + hostInfoLength).ToArray());
                }
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
            buffer.Add((byte)songSelectionType);

            byte[] hostInfo = roomHost.ToBytes();
            buffer.AddRange(hostInfo);

            buffer.AddRange(BitConverter.GetBytes(players));
            buffer.AddRange(BitConverter.GetBytes(maxPlayers));
            
            buffer.Add(noFail ? (byte)1 : (byte)0);

            if (selectedSong != null)
            {
                buffer.Add(selectedDifficulty);
                buffer.AddRange(selectedSong.ToBytes(false));
            }
            
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
