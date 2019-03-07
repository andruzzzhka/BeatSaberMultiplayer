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

        public Color32 playerNameColor = new Color32(255, 255, 255, 255);

        public PlayerState playerState;

        public uint playerScore;
        public uint playerCutBlocks;
        public uint playerComboBlocks;
        public uint playerTotalBlocks;
        public float playerEnergy;

        public float playerProgress;

        public List<HitData> hitsLastUpdate = new List<HitData>();

        public bool fullBodyTracking;

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

            playerNameColor = new Color32(msg.ReadByte(), msg.ReadByte(), msg.ReadByte(), 255);

            playerState = (PlayerState)msg.ReadByte();

            fullBodyTracking = msg.ReadBoolean();
            playerScore = msg.ReadVariableUInt32();
            playerCutBlocks = msg.ReadVariableUInt32();
            playerComboBlocks = msg.ReadVariableUInt32();
            playerTotalBlocks = msg.ReadVariableUInt32();
            msg.ReadPadBits();
            playerEnergy = msg.ReadFloat();
            playerProgress = msg.ReadFloat();

            rightHandPos = msg.ReadVector3();
            leftHandPos = msg.ReadVector3();
            headPos = msg.ReadVector3();

            rightHandRot = msg.ReadQuaternion();
            leftHandRot = msg.ReadQuaternion();
            headRot = msg.ReadQuaternion();

            if (fullBodyTracking)
            {
                msg.ReadVector3(); //Pelvis Pos
                msg.ReadVector3(); //Left Leg Pos
                msg.ReadVector3(); //Right Leg Pos

                msg.ReadQuaternion(); //Pelvis Rot
                msg.ReadQuaternion(); //Left Leg Rot
                msg.ReadQuaternion(); //Left Leg Pos
            }

            avatarHash = BitConverter.ToString(msg.ReadBytes(16)).Replace("-", "");

            hitsLastUpdate.Clear();

            byte hitsCount = msg.ReadByte();

            for (int i = 0; i < hitsCount; i++)
            {
                hitsLastUpdate.Add(new HitData(msg));
            }
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

            rightHandPos.AddToMessage(msg);
            leftHandPos.AddToMessage(msg);
            headPos.AddToMessage(msg);

            rightHandRot.AddToMessage(msg);
            leftHandRot.AddToMessage(msg);
            headRot.AddToMessage(msg);

            if (fullBodyTracking)
            {
                Vector3.zero.AddToMessage(msg); //Pelvis Pos
                Vector3.zero.AddToMessage(msg); //Left Leg Pos
                Vector3.zero.AddToMessage(msg); //Right Leg Pos

                Quaternion.identity.AddToMessage(msg); //Pelvis Rot
                Quaternion.identity.AddToMessage(msg); //Left Leg Rot
                Quaternion.identity.AddToMessage(msg); //Left Leg Pos
            }

            msg.Write(HexConverter.ConvertHexToBytesX(avatarHash));

            msg.Write((byte)hitsLastUpdate.Count);
            
            for (int i = 0; i < (byte)hitsLastUpdate.Count; i++)
            {
                hitsLastUpdate[i].AddToMessage(msg);
            }

            hitsLastUpdate.Clear();
        }

        public override bool Equals(object obj)
        {
            if (obj is PlayerInfo)
            {
                return (playerId == (obj as PlayerInfo).playerId);
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
