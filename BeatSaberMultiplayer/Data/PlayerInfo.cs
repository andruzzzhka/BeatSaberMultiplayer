using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace BeatSaberMultiplayer.Data
{
    public enum PlayerState: byte { Disconnected, Lobby, Room, Game, Spectating, DownloadingSongs }

    public class PlayerInfo
    {
        public string playerName;
        public ulong playerId;

        public PlayerState playerState;

        public uint playerScore;
        public uint playerCutBlocks;
        public uint playerComboBlocks;
        public uint playerTotalBlocks;
        public float playerEnergy;

        public float playerProgress;

        public Vector3 headPos;
        public Vector3 rightHandPos;
        public Vector3 leftHandPos;

        public Quaternion headRot;
        public Quaternion rightHandRot;
        public Quaternion leftHandRot;

        public string avatarHash;

        public PlayerInfo(string _name, ulong _id)
        {
            playerName = _name;
            playerId = _id;
            avatarHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
        }

        public PlayerInfo(NetIncomingMessage msg)
        {
            playerName = msg.ReadString();
            playerId = msg.ReadUInt64();

            playerState = (PlayerState)msg.ReadByte();

            playerScore = msg.ReadUInt32();
            playerCutBlocks = msg.ReadUInt32();
            playerComboBlocks = msg.ReadUInt32();
            playerTotalBlocks = msg.ReadUInt32();
            playerEnergy = msg.ReadFloat();
            playerProgress = msg.ReadFloat();

            byte[] avatar = msg.ReadBytes(100);

            rightHandPos = Serialization.ToVector3(avatar.Take(12).ToArray());
            leftHandPos = Serialization.ToVector3(avatar.Skip(12).Take(12).ToArray());
            headPos = Serialization.ToVector3(avatar.Skip(24).Take(12).ToArray());

            rightHandRot = Serialization.ToQuaternion(avatar.Skip(36).Take(16).ToArray());
            leftHandRot = Serialization.ToQuaternion(avatar.Skip(52).Take(16).ToArray());
            headRot = Serialization.ToQuaternion(avatar.Skip(68).Take(16).ToArray());

            avatarHash = BitConverter.ToString(avatar.Skip(84).Take(16).ToArray()).Replace("-", "");
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

            msg.Write(Serialization.Combine(
                            Serialization.ToBytes(rightHandPos),
                            Serialization.ToBytes(leftHandPos),
                            Serialization.ToBytes(headPos),
                            Serialization.ToBytes(rightHandRot),
                            Serialization.ToBytes(leftHandRot),
                            Serialization.ToBytes(headRot), 
                            HexConverter.ConvertHexToBytesX(avatarHash)));
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
