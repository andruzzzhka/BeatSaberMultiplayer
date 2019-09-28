using Lidgren.Network;
using ServerHub.Misc;
using System;
using System.Collections.Generic;

namespace ServerHub.Data
{
    public enum PlayerState: byte { Disconnected, Lobby, Room, Game, Spectating, DownloadingSongs }
    public enum GameState : byte { Intro, Playing, Paused, Finished, Failed}

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

        public byte[] avatarData;

        public PlayerUpdate(NetIncomingMessage msg)
        {
            playerNameColor = new Color32(msg.ReadByte(), msg.ReadByte(), msg.ReadByte());

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

            avatarData = msg.ReadBytes(fullBodyTracking ? 168 : 84);
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
                new LevelOptionsInfo(BeatmapDifficulty.Hard, new GameplayModifiers(), "Standard").AddToMessage(msg);
            else
                playerLevelOptions.AddToMessage(msg);

            playerFlags.AddToMessage(msg);

            if ((avatarData.Length == 168 && fullBodyTracking) || (avatarData.Length == 84 && !fullBodyTracking))
            {
                msg.Write(avatarData);
            }
            else
            {
                avatarData = new byte[fullBodyTracking ? 168 : 84];
                msg.Write(avatarData);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerUpdate info &&
                   EqualityComparer<Color32>.Default.Equals(playerNameColor, info.playerNameColor) &&
                   playerState == info.playerState &&
                   playerScore == info.playerScore &&
                   playerCutBlocks == info.playerCutBlocks &&
                   playerComboBlocks == info.playerComboBlocks &&
                   playerTotalBlocks == info.playerTotalBlocks &&
                   playerEnergy == info.playerEnergy &&
                   playerProgress == info.playerProgress &&
                   EqualityComparer<LevelOptionsInfo>.Default.Equals(playerLevelOptions, info.playerLevelOptions) &&
                   fullBodyTracking == info.fullBodyTracking;
        }

        public bool Equals(PlayerUpdate info)
        {
            return EqualityComparer<Color32>.Default.Equals(playerNameColor, info.playerNameColor) &&
                   playerState == info.playerState &&
                   playerScore == info.playerScore &&
                   playerCutBlocks == info.playerCutBlocks &&
                   playerComboBlocks == info.playerComboBlocks &&
                   playerTotalBlocks == info.playerTotalBlocks &&
                   playerEnergy == info.playerEnergy &&
                   playerProgress == info.playerProgress &&
                   EqualityComparer<LevelOptionsInfo>.Default.Equals(playerLevelOptions, info.playerLevelOptions) &&
                   fullBodyTracking == info.fullBodyTracking;
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

    public struct HitData
    {
        public int objectId;

        public bool noteWasCut;
        public bool speedOK;
        public bool directionOK;
        public bool saberTypeOK;
        public bool wasCutTooSoon;
        public bool isSaberA;

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
    }

    public class PlayerInfo
    {
        public string playerName;
        [NonSerialized]
        public ulong playerId;
        public string playerIdString;

        public string avatarHash;

        public PlayerUpdate updateInfo;

        public List<HitData> hitsLastUpdate = new List<HitData>();

        public PlayerInfo(string _name, ulong _id)
        {
            playerName = _name;
            playerId = _id;
        }

        public PlayerInfo(NetIncomingMessage msg)
        {
            playerName = msg.ReadString();
            playerId = msg.ReadUInt64();

            updateInfo = new PlayerUpdate(msg);

            avatarHash = BitConverter.ToString(msg.ReadBytes(16)).Replace("-", "");

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

            updateInfo.AddToMessage(msg);

            msg.Write(HexConverter.ConvertHexToBytesX(avatarHash));

            if (hitsLastUpdate != null)
            {
                msg.Write((byte)hitsLastUpdate.Count);

                for (int i = 0; i < (byte)hitsLastUpdate.Count; i++)
                {
                    hitsLastUpdate[i].AddToMessage(msg);
                }

                hitsLastUpdate.Clear();
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
