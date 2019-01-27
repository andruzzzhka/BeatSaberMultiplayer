using Lidgren.Network;
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
        [NonSerialized]
        public ulong playerId;

        public string playerIdString;

        public PlayerState playerState;

        public uint playerScore;
        public uint playerCutBlocks;
        public uint playerComboBlocks;
        public uint playerTotalBlocks;
        public float playerEnergy;

        public float playerProgress;

        [NonSerialized]
        public byte[] playerAvatar;

        public PlayerInfo(string _name, ulong _id, byte[] _avatar = null)
        {
            playerName = _name;
            playerId = _id;
            playerAvatar = (_avatar == null) ? new byte[100] : _avatar;
        }

        public PlayerInfo(NetIncomingMessage msg)
        {
            playerName = msg.ReadString();
            playerId = msg.ReadUInt64();
            playerIdString = playerId.ToString();

            playerState = (PlayerState)msg.ReadByte();

            playerScore = msg.ReadUInt32();
            playerCutBlocks = msg.ReadUInt32();
            playerComboBlocks = msg.ReadUInt32();
            playerTotalBlocks = msg.ReadUInt32();
            playerEnergy = msg.ReadFloat();
            playerProgress = msg.ReadFloat();

            playerAvatar = msg.ReadBytes(100);
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(playerName);
            msg.Write(playerId);

            msg.Write((byte)playerState);

            msg.Write(playerScore);
            msg.Write(playerCutBlocks);
            msg.Write(playerComboBlocks);
            msg.Write(playerTotalBlocks);
            msg.Write(playerEnergy);
            msg.Write(playerProgress);

            msg.Write(playerAvatar);
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
