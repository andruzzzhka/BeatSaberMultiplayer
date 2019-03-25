using Lidgren.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Data
{
    public enum RoomState: byte {SelectingSong, Preparing, InGame, Results }
    public enum SongSelectionType : byte { Manual,  Random }

    public class RoomInfo
    {
        public uint roomId;

        public string name;
        public bool usePassword;
        public bool perPlayerDifficulty;

        [JsonConverter(typeof(StringEnumConverter))]
        public RoomState roomState;
        [JsonConverter(typeof(StringEnumConverter))]
        public SongSelectionType songSelectionType;
        public PlayerInfo roomHost;

        public int players;
        public int maxPlayers;

        public bool songSelected;

        public StartLevelInfo startLevelInfo;
        public SongInfo selectedSong;


        public RoomInfo()
        {

        }

        public RoomInfo(NetIncomingMessage msg)
        {
            roomId = msg.ReadUInt32();
            name = msg.ReadString();
            usePassword = msg.ReadBoolean();
            perPlayerDifficulty = msg.ReadBoolean();
            songSelected = msg.ReadBoolean();
            msg.SkipPadBits();
            roomState = (RoomState)msg.ReadByte();
            songSelectionType = (SongSelectionType)msg.ReadByte();
            roomHost = new PlayerInfo(msg);
            players = msg.ReadInt32();
            maxPlayers = msg.ReadInt32();
            try
            {
                if (songSelected)
                {
                    startLevelInfo = new StartLevelInfo(msg);
                    selectedSong = new SongInfo(msg);
                }
                else
                {
                    startLevelInfo = null;
                    selectedSong = null;
                }
            }
            catch
            {

            }
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            songSelected = selectedSong != null && roomState != RoomState.SelectingSong;

            msg.Write(roomId);
            msg.Write(name);
            msg.Write(usePassword);
            msg.Write(perPlayerDifficulty);
            msg.Write(songSelected);
            msg.WritePadBits();
            msg.Write((byte)roomState);
            msg.Write((byte)songSelectionType);

            roomHost.AddToMessage(msg);

            msg.Write(players);
            msg.Write(maxPlayers);

            if (songSelected)
            {
                if (startLevelInfo == null)
                    startLevelInfo = new StartLevelInfo(BeatmapDifficulty.Hard, new GameplayModifiers(), "Standard");

                startLevelInfo.AddToMessage(msg);
                selectedSong.AddToMessage(msg);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is RoomInfo)
            {
                return (name == ((RoomInfo)obj).name) && (usePassword == ((RoomInfo)obj).usePassword) && (players == ((RoomInfo)obj).players) && (maxPlayers == ((RoomInfo)obj).maxPlayers) && (roomHost.Equals(((RoomInfo)obj).roomHost));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = 1133574822;
            hashCode = hashCode * -1521134295 + roomId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name);
            hashCode = hashCode * -1521134295 + usePassword.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<PlayerInfo>.Default.GetHashCode(roomHost);
            hashCode = hashCode * -1521134295 + players.GetHashCode();
            hashCode = hashCode * -1521134295 + maxPlayers.GetHashCode();
            return hashCode;
        }
    }

}
