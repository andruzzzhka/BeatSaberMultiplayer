using Lidgren.Network;
using ServerHub.Misc;
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

        public Color32 playerNameColor  = new Color32( 255, 255, 255);

        public PlayerState playerState;

        public uint playerScore;
        public uint playerCutBlocks;
        public uint playerComboBlocks;
        public uint playerTotalBlocks;
        public float playerEnergy;

        public float playerProgress;

        public byte[] hitsLastUpdate;

        public bool fullBodyTracking;
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

            playerNameColor = new Color32(msg.ReadByte(), msg.ReadByte(), msg.ReadByte());

            playerState = (PlayerState)msg.ReadByte();

            fullBodyTracking = msg.ReadBoolean();
            playerScore = msg.ReadVariableUInt32();
            playerCutBlocks = msg.ReadVariableUInt32();
            playerComboBlocks = msg.ReadVariableUInt32();
            playerTotalBlocks = msg.ReadVariableUInt32();
            msg.ReadPadBits();
            playerEnergy = msg.ReadFloat();
            playerProgress = msg.ReadFloat();
            
            avatarData = msg.ReadBytes(84 * (fullBodyTracking ? 2 : 1) + 16);

            byte hitsCount = msg.ReadByte();

            if(hitsCount > 0)
                hitsLastUpdate = msg.ReadBytes(hitsCount * 5);
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(playerName);
            msg.Write(playerId);

            msg.Write(playerNameColor.r);
            msg.Write(playerNameColor.g);
            msg.Write(playerNameColor.b);

            msg.Write((byte)playerState);

            msg.Write(fullBodyTracking);
            msg.WriteVariableUInt32(playerScore);
            msg.WriteVariableUInt32(playerCutBlocks);
            msg.WriteVariableUInt32(playerComboBlocks);
            msg.WriteVariableUInt32(playerTotalBlocks);
            msg.WritePadBits();
            msg.Write(playerEnergy);
            msg.Write(playerProgress);

            msg.Write(avatarData ?? new byte[84 * (fullBodyTracking ? 2 : 1) + 16]);

            if (hitsLastUpdate != null)
            {
                msg.Write((byte)(hitsLastUpdate.Length / 5));

                msg.Write(hitsLastUpdate);
            }
            else
            {
                msg.Write((byte)0);
            }
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
