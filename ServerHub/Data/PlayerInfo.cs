using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Data
{
    public class PlayerInfo
    {
        public string playerName;
        public ulong playerId;

        public uint playerScore;
        public uint playerCutBlocks;
        public uint playerComboBlocks;
        public float playerEnergy;

        public byte[] playerAvatar;

        public PlayerInfo(string _name, ulong _id, byte[] _avatar = null)
        {
            playerName = _name;
            playerId = _id;
            playerAvatar = (_avatar == null) ? new byte[0] : _avatar;
        }

        public PlayerInfo(byte[] data)
        {
            int length = BitConverter.ToInt32(data, 0);
            
            int nameLength = BitConverter.ToInt32(data, 4);
            playerName = Encoding.UTF8.GetString(data, 8, nameLength);
            playerId = BitConverter.ToUInt64(data, 8 + nameLength);

            playerScore = BitConverter.ToUInt32(data, 16 + nameLength);
            playerCutBlocks = BitConverter.ToUInt32(data, 20 + nameLength);
            playerComboBlocks = BitConverter.ToUInt32(data, 24 + nameLength);
            playerEnergy = BitConverter.ToSingle(data, 28 + nameLength);

            playerAvatar = data.Skip(32 + nameLength).Take(84).ToArray();
        }

        public byte[] ToBytes()
        {
            List<byte> buffer = new List<byte>();

            byte[] nameBuffer = Encoding.UTF8.GetBytes(playerName);
            buffer.AddRange(BitConverter.GetBytes(nameBuffer.Length));
            buffer.AddRange(nameBuffer);

            buffer.AddRange(BitConverter.GetBytes(playerId));
            buffer.AddRange(BitConverter.GetBytes(playerScore));
            buffer.AddRange(BitConverter.GetBytes(playerCutBlocks));
            buffer.AddRange(BitConverter.GetBytes(playerComboBlocks));
            buffer.AddRange(BitConverter.GetBytes(playerEnergy));

            buffer.AddRange(playerAvatar);

            buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
        }

        public override bool Equals(object obj)
        {
            if (obj is PlayerInfo)
            {
                return (playerId == (obj as PlayerInfo).playerId) && (playerName == (obj as PlayerInfo).playerName) && (playerScore == (obj as PlayerInfo).playerScore) && (playerCutBlocks == (obj as PlayerInfo).playerCutBlocks) && (playerComboBlocks == (obj as PlayerInfo).playerComboBlocks) && (playerEnergy == (obj as PlayerInfo).playerEnergy) && ((obj as PlayerInfo).playerAvatar.SequenceEqual(playerAvatar));
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
