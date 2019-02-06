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

    public struct HitData
    {
        public float objectTime;

        public bool noteWasCut;
        public bool speedOK;
        public bool directionOK;
        public bool saberTypeOK;
        public bool wasCutTooSoon;
        public bool isSaberA;
        public bool reserved1;
        public bool reserved2;

        public HitData(NoteData data, bool noteWasCut, NoteCutInfo info = null)
        {
            objectTime = data.time;

            this.noteWasCut = noteWasCut;

            if (noteWasCut)
            {
                isSaberA = info.saberType == Saber.SaberType.SaberA;
                speedOK = info.speedOK;
                directionOK = info.directionOK;
                saberTypeOK = info.saberTypeOK;
                wasCutTooSoon = info.wasCutTooSoon;
            }
            else
            {
                isSaberA = false;
                speedOK = false;
                directionOK = false;
                saberTypeOK = false;
                wasCutTooSoon = false;
            }

            reserved1 = reserved2 = false;
        }

        public HitData(NetIncomingMessage msg)
        {
            objectTime = msg.ReadFloat();

            noteWasCut = msg.ReadBoolean();
            isSaberA = msg.ReadBoolean();
            speedOK = msg.ReadBoolean();
            directionOK = msg.ReadBoolean();
            saberTypeOK = msg.ReadBoolean();
            wasCutTooSoon = msg.ReadBoolean();
            reserved1 = msg.ReadBoolean();
            reserved2 = msg.ReadBoolean();
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(objectTime);

            msg.Write(noteWasCut);
            msg.Write(isSaberA);
            msg.Write(speedOK);
            msg.Write(directionOK);
            msg.Write(saberTypeOK);
            msg.Write(wasCutTooSoon);
            msg.Write(reserved1);
            msg.Write(reserved2);
        }

        public NoteCutInfo GetCutInfo()
        {
            if (noteWasCut)
            {
                return new NoteCutInfo(speedOK, directionOK, saberTypeOK, wasCutTooSoon, 3f, Vector3.down, isSaberA ? Saber.SaberType.SaberA : Saber.SaberType.SaberB, 0, 0, 0, Vector3.zero, Vector3.down, null, 0f);
            }
            else
            {
                return null;
            }
        }
    }

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

        public List<HitData> hitsLastUpdate = new List<HitData>();

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

            playerScore = msg.ReadVariableUInt32();
            playerCutBlocks = msg.ReadVariableUInt32();
            playerComboBlocks = msg.ReadVariableUInt32();
            playerTotalBlocks = msg.ReadVariableUInt32();
            playerEnergy = msg.ReadFloat();
            playerProgress = msg.ReadFloat();

            hitsLastUpdate.Clear();

            byte hitsCount = msg.ReadByte();

            for(int i = 0; i < hitsCount; i++)
            {
                hitsLastUpdate.Add(new HitData(msg));
            }

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

            msg.Write(playerScore, 24);
            msg.Write(playerCutBlocks, 19);
            msg.Write(playerComboBlocks, 18);
            msg.Write(playerTotalBlocks, 19);
            msg.Write(playerEnergy);
            msg.Write(playerProgress);

            msg.Write((byte)hitsLastUpdate.Count);

            for (int i = 0; i < (byte)hitsLastUpdate.Count; i++)
            {
                hitsLastUpdate[i].AddToMessage(msg);
            }

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
