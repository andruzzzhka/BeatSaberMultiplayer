using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

#if DEBUG
        public HitData(byte[] data)
        {
            objectTime = BitConverter.ToSingle(data, 0);
            
            BitArray bits = new BitArray(new byte[] { data[4] });

            noteWasCut = bits[0];
            isSaberA = bits[1];
            speedOK = bits[2];
            directionOK = bits[3];
            saberTypeOK = bits[4];
            wasCutTooSoon = bits[5];
            reserved1 = bits[6];
            reserved2 = bits[7];
        }
#endif

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
        public const string avatarHashPlaceholder = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

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

        public LevelOptionsInfo playerLevelOptions;

        public List<HitData> hitsLastUpdate = new List<HitData>();

        public bool fullBodyTracking;

        public Vector3 headPos;
        public Vector3 rightHandPos;
        public Vector3 leftHandPos;
        public Vector3 rightLegPos;
        public Vector3 leftLegPos;
        public Vector3 pelvisPos;

        public Quaternion headRot;
        public Quaternion rightHandRot;
        public Quaternion leftHandRot;
        public Quaternion rightLegRot;
        public Quaternion leftLegRot;
        public Quaternion pelvisRot;
        
        public string avatarHash;

        public PlayerInfo(string _name, ulong _id)
        {
            playerName = _name;
            playerId = _id;
            avatarHash = avatarHashPlaceholder;
        }

        public PlayerInfo(PlayerInfo original)
        {

            var fields = typeof(PlayerInfo).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.FieldType.IsValueType)
                {
                    field.SetValue(this, field.GetValue(original));
                }
            }

            playerLevelOptions = new LevelOptionsInfo(original.playerLevelOptions);
            hitsLastUpdate = new List<HitData>(original.hitsLastUpdate);
            playerName = original.playerName;
            avatarHash = original.avatarHash;
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

            playerLevelOptions = new LevelOptionsInfo(msg);

            rightHandPos = msg.ReadVector3();
            leftHandPos = msg.ReadVector3();
            headPos = msg.ReadVector3();

            rightHandRot = msg.ReadQuaternion();
            leftHandRot = msg.ReadQuaternion();
            headRot = msg.ReadQuaternion();

            if (fullBodyTracking)
            {
                pelvisPos = msg.ReadVector3();
                leftLegPos = msg.ReadVector3();
                rightLegPos = msg.ReadVector3();

                pelvisRot = msg.ReadQuaternion();
                leftLegRot = msg.ReadQuaternion();
                rightLegRot = msg.ReadQuaternion();
            }

            avatarHash = BitConverter.ToString(msg.ReadBytes(16)).Replace("-", "");

            hitsLastUpdate.Clear();

            byte hitsCount = msg.ReadByte();

            for (int i = 0; i < hitsCount; i++)
            {
                hitsLastUpdate.Add(new HitData(msg));
            }
        }

#if DEBUG
        public PlayerInfo(byte[] data)
        {
            playerState = (PlayerState)data[0];

            playerScore = BitConverter.ToUInt32(data, 1);
            playerCutBlocks = BitConverter.ToUInt32(data, 5);
            playerComboBlocks = BitConverter.ToUInt32(data, 9);
            playerTotalBlocks = BitConverter.ToUInt32(data, 13);
            playerEnergy = BitConverter.ToSingle(data, 17);
            playerProgress = BitConverter.ToSingle(data, 21);

            rightHandPos = new Vector3(BitConverter.ToSingle(data, 25), BitConverter.ToSingle(data, 29), BitConverter.ToSingle(data, 33));
            leftHandPos = new Vector3(BitConverter.ToSingle(data, 37), BitConverter.ToSingle(data, 41), BitConverter.ToSingle(data, 45));
            headPos = new Vector3(BitConverter.ToSingle(data, 49), BitConverter.ToSingle(data, 53), BitConverter.ToSingle(data, 57));

            rightHandRot = new Quaternion(BitConverter.ToSingle(data, 61), BitConverter.ToSingle(data, 65), BitConverter.ToSingle(data, 69), BitConverter.ToSingle(data, 73));
            leftHandRot = new Quaternion(BitConverter.ToSingle(data, 77), BitConverter.ToSingle(data, 81), BitConverter.ToSingle(data, 85), BitConverter.ToSingle(data, 89));
            headRot = new Quaternion(BitConverter.ToSingle(data, 93), BitConverter.ToSingle(data, 97), BitConverter.ToSingle(data, 101), BitConverter.ToSingle(data, 105));

            hitsLastUpdate.Clear();

            byte hitsCount = data[109];

            if (hitsCount > 0)
            {
                MemoryStream stream = new MemoryStream(data, 110, hitsCount * 5);

                for (int i = 0; i < hitsCount; i++)
                {
                    byte[] hit = new byte[5];
                    stream.Read(hit, 0, 5);
                    hitsLastUpdate.Add(new HitData(hit));
                }
            }
        }
#endif

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

            if (playerLevelOptions == null)
                new LevelOptionsInfo(BeatmapDifficulty.Hard, GameplayModifiers.defaultModifiers, "Standard").AddToMessage(msg);
            else
                playerLevelOptions.AddToMessage(msg);

            rightHandPos.AddToMessage(msg);
            leftHandPos.AddToMessage(msg);
            headPos.AddToMessage(msg);

            rightHandRot.AddToMessage(msg);
            leftHandRot.AddToMessage(msg);
            headRot.AddToMessage(msg);

            if (fullBodyTracking)
            {
                pelvisPos.AddToMessage(msg);
                leftLegPos.AddToMessage(msg);
                rightLegPos.AddToMessage(msg);

                pelvisRot.AddToMessage(msg);
                leftLegRot.AddToMessage(msg);
                rightLegRot.AddToMessage(msg);
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
