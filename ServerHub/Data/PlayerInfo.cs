using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Data
{
    public enum PlayerState: byte { Disconnected, Lobby, Room, Game, Spectating, DownloadingSongs }
    public enum GameState : byte { Intro, Playing, Paused, Finished, Failed}

    public class PlayerInfo
    {
        public string playerName;
        public ulong playerId;

        public PlayerState playerState;

        public uint playerScore;
        public uint playerCutBlocks;
        public uint playerComboBlocks;
        public float playerEnergy;

        public float playerProgress;

        public byte[] playerAvatar;

        public PlayerInfo(string _name, ulong _id, byte[] _avatar = null)
        {
            playerName = _name;
            playerId = _id;
            playerAvatar = (_avatar == null) ? new byte[84] : _avatar;
        }

        public PlayerInfo(byte[] data)
        {            
            int nameLength = BitConverter.ToInt32(data, 0);
            playerName = Encoding.UTF8.GetString(data, 4, nameLength);
            playerId = BitConverter.ToUInt64(data, 4 + nameLength);

            playerState = (PlayerState)data[12 + nameLength];

            playerScore = BitConverter.ToUInt32(data, 13 + nameLength);
            playerCutBlocks = BitConverter.ToUInt32(data, 17 + nameLength);
            playerComboBlocks = BitConverter.ToUInt32(data, 21 + nameLength);
            playerEnergy = BitConverter.ToSingle(data, 25 + nameLength);
            
            playerProgress = BitConverter.ToSingle(data, 29 + nameLength);

            playerAvatar = data.Skip(33 + nameLength).Take(84).ToArray();
        }

        public byte[] ToBytes(bool includeSize = true)
        {
            List<byte> buffer = new List<byte>();

            byte[] nameBuffer = Encoding.UTF8.GetBytes(playerName);
            buffer.AddRange(BitConverter.GetBytes(nameBuffer.Length));
            buffer.AddRange(nameBuffer);
            buffer.AddRange(BitConverter.GetBytes(playerId));
            
            buffer.Add((byte)playerState);

            buffer.AddRange(BitConverter.GetBytes(playerScore));
            buffer.AddRange(BitConverter.GetBytes(playerCutBlocks));
            buffer.AddRange(BitConverter.GetBytes(playerComboBlocks));
            buffer.AddRange(BitConverter.GetBytes(playerEnergy));

            buffer.AddRange(BitConverter.GetBytes(playerProgress));

            buffer.AddRange(playerAvatar);

            if (includeSize)
                buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
        }

        public override bool Equals(object obj)
        {
            if (obj is PlayerInfo)
            {
                return (playerId == (obj as PlayerInfo).playerId) && (playerName == (obj as PlayerInfo).playerName);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = -2041759944;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(playerName);
            hashCode = hashCode * -1521134295 + playerId.GetHashCode();
            return hashCode;
        }
    }
}
