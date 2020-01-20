using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BeatSaberMultiplayer.Data
{
    public enum PlayerState: byte { Disconnected, Lobby, Room, Game, Spectating, DownloadingSongs }

    public struct PlayerScore : IComparable<PlayerScore>, IEquatable<PlayerScore>
    {
        public ulong id;
        public string name;
        public Color32 color;
        public uint score;
        public bool valid;
        public ExtraPlayerFlags playerFlags;

        public PlayerScore(ulong id, string name, uint score, Color32 color, bool valid, ExtraPlayerFlags playerFlags)
        {
            this.id = id;
            this.name = name;
            this.score = score;
            this.color = color;
            this.valid = valid;
            this.playerFlags = playerFlags;
        }

        public PlayerScore(PlayerInfo update)
        {
            id = update.playerId;
            name = update.playerName;
            score = update.updateInfo.playerScore;
            color = update.updateInfo.playerNameColor;
            valid = update.updateInfo.playerState == PlayerState.Game;
            playerFlags = update.updateInfo.playerFlags;
        }

        public PlayerScore(OnlinePlayerController update)
        {
            id = update.playerInfo.playerId;
            name = update.playerInfo.playerName;
            score = update.playerInfo.updateInfo.playerScore;
            color = update.playerInfo.updateInfo.playerNameColor;
            valid = update.playerInfo.updateInfo.playerState == PlayerState.Game;
            playerFlags = update.playerInfo.updateInfo.playerFlags;
        }

        public int CompareTo(PlayerScore other)
        {
            return valid ? other.score.CompareTo(score) : 1;
        }

        public bool Equals(PlayerScore other)
        {
            return id == other.id &&
                    name == other.name &&
                    score == other.score &&
                    color.r == other.color.r &&
                    color.g == other.color.g &&
                    color.b == other.color.b &&
                    color.a == other.color.a &&
                    playerFlags.Equals(other.playerFlags);
        }

        public static bool operator ==(PlayerScore c1, PlayerScore c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(PlayerScore c1, PlayerScore c2)
        {
            return !c1.Equals(c2);
        }
    }

    public struct HitData
    {
        public int objectId;

        public bool noteWasCut;
        public bool speedOK;
        public bool directionOK;
        public bool saberTypeOK;
        public bool wasCutTooSoon;
        public bool isSaberA;

        public HitData(NoteData data, bool noteWasCut, NoteCutInfo info = null)
        {
            objectId = data.id;

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
        }

        public HitData(NetIncomingMessage msg)
        {
            objectId = msg.ReadInt32();

            noteWasCut = msg.ReadBoolean();
            isSaberA = msg.ReadBoolean();
            speedOK = msg.ReadBoolean();
            directionOK = msg.ReadBoolean();
            saberTypeOK = msg.ReadBoolean();
            wasCutTooSoon = msg.ReadBoolean();
            msg.ReadBoolean();
            msg.ReadBoolean();
        }

#if DEBUG
        public HitData(byte[] data)
        {
            objectId = BitConverter.ToInt32(data, 0);
            
            BitArray bits = new BitArray(new byte[] { data[4] });

            noteWasCut = bits[0];
            isSaberA = bits[1];
            speedOK = bits[2];
            directionOK = bits[3];
            saberTypeOK = bits[4];
            wasCutTooSoon = bits[5];
        }
#endif

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(objectId);

            msg.Write(noteWasCut);
            msg.Write(isSaberA);
            msg.Write(speedOK);
            msg.Write(directionOK);
            msg.Write(saberTypeOK);
            msg.Write(wasCutTooSoon);
            msg.Write(false);
            msg.Write(false);
        }

        public NoteCutInfo GetCutInfo()
        {
            if (noteWasCut)
            {
                return new NoteCutInfo(speedOK, directionOK, saberTypeOK, wasCutTooSoon, 3f, Vector3.down, isSaberA ? Saber.SaberType.SaberA : Saber.SaberType.SaberB, 0, 0, Vector3.zero, Vector3.down, null, 0f);
            }
            else
            {
                return null;
            }
        }
    }

    public struct PlayerUpdate : IEquatable<PlayerUpdate>
    {
        public Color32 playerNameColor;
        public PlayerState playerState;

        public uint playerScore;
        public uint playerCutBlocks;
        public uint playerComboBlocks;
        public uint playerTotalBlocks;
        public float playerEnergy;

        public float playerProgress;

        public LevelOptionsInfo playerLevelOptions;

        public ExtraPlayerFlags playerFlags;

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

        public PlayerUpdate(NetIncomingMessage msg)
        {
            playerNameColor = new Color32(msg.ReadByte(), msg.ReadByte(), msg.ReadByte(), 255);

            playerState = (PlayerState)msg.ReadByte();

            fullBodyTracking = (msg.ReadByte() == 1);

            playerScore = msg.ReadVariableUInt32();
            playerCutBlocks = msg.ReadVariableUInt32();
            playerComboBlocks = msg.ReadVariableUInt32();
            playerTotalBlocks = msg.ReadVariableUInt32();
            playerEnergy = msg.ReadFloat();
            playerProgress = msg.ReadFloat();

            playerLevelOptions = new LevelOptionsInfo(msg);

            playerFlags = new ExtraPlayerFlags(msg);

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
            else
            {
                pelvisPos = Vector3.zero;
                leftLegPos = Vector3.zero;
                rightLegPos = Vector3.zero;

                pelvisRot = Quaternion.identity;
                leftLegRot = Quaternion.identity;
                rightLegRot = Quaternion.identity;
            }
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(playerNameColor.r);
            msg.Write(playerNameColor.g);
            msg.Write(playerNameColor.b);

            msg.Write((byte)playerState);

            msg.Write(fullBodyTracking ? (byte)1 : (byte)0);
            msg.WriteVariableUInt32(playerScore);
            msg.WriteVariableUInt32(playerCutBlocks);
            msg.WriteVariableUInt32(playerComboBlocks);
            msg.WriteVariableUInt32(playerTotalBlocks);
            msg.Write(playerEnergy);
            msg.Write(playerProgress);

            if (playerLevelOptions == default)
                playerLevelOptions = new LevelOptionsInfo(BeatmapDifficulty.Hard, GameplayModifiers.defaultModifiers, "Standard");

            playerLevelOptions.AddToMessage(msg);

            playerFlags.AddToMessage(msg);

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
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerUpdate info &&
                   playerNameColor.r == info.playerNameColor.r &&
                   playerNameColor.g == info.playerNameColor.g &&
                   playerNameColor.b == info.playerNameColor.b &&
                   playerNameColor.a == info.playerNameColor.a &&
                   playerState == info.playerState &&
                   playerScore == info.playerScore &&
                   playerCutBlocks == info.playerCutBlocks &&
                   playerComboBlocks == info.playerComboBlocks &&
                   playerTotalBlocks == info.playerTotalBlocks &&
                   playerEnergy == info.playerEnergy &&
                   playerProgress == info.playerProgress &&
                   playerLevelOptions == info.playerLevelOptions &&
                   fullBodyTracking == info.fullBodyTracking &&
                   headPos.Equals(info.headPos) &&
                   rightHandPos.Equals(info.rightHandPos) &&
                   leftHandPos.Equals(info.leftHandPos) &&
                   rightLegPos.Equals(info.rightLegPos) &&
                   leftLegPos.Equals(info.leftLegPos) &&
                   pelvisPos.Equals(info.pelvisPos) &&
                   headRot.Equals(info.headRot) &&
                   rightHandRot.Equals(info.rightHandRot) &&
                   leftHandRot.Equals(info.leftHandRot) &&
                   rightLegRot.Equals(info.rightLegRot) &&
                   leftLegRot.Equals(info.leftLegRot) &&
                   pelvisRot.Equals(info.pelvisRot);
        }

        public bool Equals(PlayerUpdate info)
        {
            return playerNameColor.r == info.playerNameColor.r &&
                   playerNameColor.g == info.playerNameColor.g &&
                   playerNameColor.b == info.playerNameColor.b &&
                   playerNameColor.a == info.playerNameColor.a &&
                   playerState == info.playerState &&
                   playerScore == info.playerScore &&
                   playerCutBlocks == info.playerCutBlocks &&
                   playerComboBlocks == info.playerComboBlocks &&
                   playerTotalBlocks == info.playerTotalBlocks &&
                   playerEnergy == info.playerEnergy &&
                   playerProgress == info.playerProgress &&
                   playerLevelOptions == info.playerLevelOptions &&
                   fullBodyTracking == info.fullBodyTracking &&
                   headPos.Equals(info.headPos) &&
                   rightHandPos.Equals(info.rightHandPos) &&
                   leftHandPos.Equals(info.leftHandPos) &&
                   rightLegPos.Equals(info.rightLegPos) &&
                   leftLegPos.Equals(info.leftLegPos) &&
                   pelvisPos.Equals(info.pelvisPos) &&
                   headRot.Equals(info.headRot) &&
                   rightHandRot.Equals(info.rightHandRot) &&
                   leftHandRot.Equals(info.leftHandRot) &&
                   rightLegRot.Equals(info.rightLegRot) &&
                   leftLegRot.Equals(info.leftLegRot) &&
                   pelvisRot.Equals(info.pelvisRot);
        }

        public override int GetHashCode()
        {
            var hashCode = -277278763;
            hashCode = hashCode * -1521134295 + EqualityComparer<Color32>.Default.GetHashCode(playerNameColor);
            hashCode = hashCode * -1521134295 + playerScore.GetHashCode();
            hashCode = hashCode * -1521134295 + playerCutBlocks.GetHashCode();
            hashCode = hashCode * -1521134295 + playerComboBlocks.GetHashCode();
            hashCode = hashCode * -1521134295 + playerTotalBlocks.GetHashCode();
            hashCode = hashCode * -1521134295 + playerEnergy.GetHashCode();
            hashCode = hashCode * -1521134295 + playerProgress.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<LevelOptionsInfo>.Default.GetHashCode(playerLevelOptions);
            hashCode = hashCode * -1521134295 + fullBodyTracking.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(headPos);
            hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(rightHandPos);
            hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(leftHandPos);
            hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(rightLegPos);
            hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(leftLegPos);
            hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(pelvisPos);
            hashCode = hashCode * -1521134295 + EqualityComparer<Quaternion>.Default.GetHashCode(headRot);
            hashCode = hashCode * -1521134295 + EqualityComparer<Quaternion>.Default.GetHashCode(rightHandRot);
            hashCode = hashCode * -1521134295 + EqualityComparer<Quaternion>.Default.GetHashCode(leftHandRot);
            hashCode = hashCode * -1521134295 + EqualityComparer<Quaternion>.Default.GetHashCode(rightLegRot);
            hashCode = hashCode * -1521134295 + EqualityComparer<Quaternion>.Default.GetHashCode(leftLegRot);
            hashCode = hashCode * -1521134295 + EqualityComparer<Quaternion>.Default.GetHashCode(pelvisRot);
            return hashCode;
        }

        public static bool operator ==(PlayerUpdate c1, PlayerUpdate c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(PlayerUpdate c1, PlayerUpdate c2)
        {
            return !c1.Equals(c2);
        }
    }

    public class PlayerInfo : IComparable<PlayerInfo>
    {
        public const string avatarHashPlaceholder = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

        public string playerName;
        public ulong playerId;

        public string avatarHash;

        public PlayerUpdate updateInfo;

        public List<HitData> hitsLastUpdate = new List<HitData>(16);

        public PlayerInfo(string _name, ulong _id)
        {
            playerName = _name;
            playerId = _id;
            avatarHash = avatarHashPlaceholder;

            updateInfo = new PlayerUpdate() { playerNameColor = new Color32(255,255,255,255) };
        }

        public PlayerInfo(NetIncomingMessage msg)
        {
            playerName = msg.ReadString();
            playerId = msg.ReadUInt64();

            updateInfo = new PlayerUpdate(msg);

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
            updateInfo = new PlayerUpdate();
            updateInfo.playerState = (PlayerState)data[0];

            updateInfo.playerScore = BitConverter.ToUInt32(data, 1);
            updateInfo.playerCutBlocks = BitConverter.ToUInt32(data, 5);
            updateInfo.playerComboBlocks = BitConverter.ToUInt32(data, 9);
            updateInfo.playerTotalBlocks = BitConverter.ToUInt32(data, 13);
            updateInfo.playerEnergy = BitConverter.ToSingle(data, 17);
            updateInfo.playerProgress = BitConverter.ToSingle(data, 21);

            updateInfo.rightHandPos = new Vector3(BitConverter.ToSingle(data, 25), BitConverter.ToSingle(data, 29), BitConverter.ToSingle(data, 33));
            updateInfo.leftHandPos = new Vector3(BitConverter.ToSingle(data, 37), BitConverter.ToSingle(data, 41), BitConverter.ToSingle(data, 45));
            updateInfo.headPos = new Vector3(BitConverter.ToSingle(data, 49), BitConverter.ToSingle(data, 53), BitConverter.ToSingle(data, 57));

            updateInfo.rightHandRot = new Quaternion(BitConverter.ToSingle(data, 61), BitConverter.ToSingle(data, 65), BitConverter.ToSingle(data, 69), BitConverter.ToSingle(data, 73));
            updateInfo.leftHandRot = new Quaternion(BitConverter.ToSingle(data, 77), BitConverter.ToSingle(data, 81), BitConverter.ToSingle(data, 85), BitConverter.ToSingle(data, 89));
            updateInfo.headRot = new Quaternion(BitConverter.ToSingle(data, 93), BitConverter.ToSingle(data, 97), BitConverter.ToSingle(data, 101), BitConverter.ToSingle(data, 105));

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

            updateInfo.AddToMessage(msg);

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

        public int CompareTo(PlayerInfo other)
        {
            return playerId.CompareTo(other.playerId);
        }
    }
}
