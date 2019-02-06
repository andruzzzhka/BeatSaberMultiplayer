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

        public byte[] hitsLastUpdate;

        public byte[] avatarData;

        public PlayerInfo(string _name, ulong _id)
        {
            playerName = _name;
            playerId = _id;
        }

        public PlayerInfo(NetIncomingMessage msg)
        {
            playerName = msg.ReadString();
            playerId = msg.ReadUInt64();
            playerIdString = playerId.ToString();

            playerState = (PlayerState)msg.ReadByte();

            playerScore = msg.ReadVariableUInt32();
            playerCutBlocks = msg.ReadVariableUInt32();
            playerComboBlocks = msg.ReadVariableUInt32();
            playerTotalBlocks = msg.ReadVariableUInt32();
            playerEnergy = msg.ReadFloat();
            playerProgress = msg.ReadFloat();

            byte hitsCount = msg.ReadByte();

            hitsLastUpdate = msg.ReadBytes(hitsCount * 5);

            avatarData = msg.ReadBytes(100);
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(playerName);
            msg.Write(playerId);

            msg.Write((byte)playerState);

            msg.Write(playerScore, 24);
            msg.Write(playerCutBlocks, 19);
            msg.Write(playerComboBlocks, 18);
            msg.Write(playerTotalBlocks, 19);
            msg.Write(playerEnergy);
            msg.Write(playerProgress);

            msg.Write((byte)(hitsLastUpdate.Length / 5));

            msg.Write(hitsLastUpdate);

            msg.Write(avatarData);
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
