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
    public enum SongSelectionType : byte { Manual,  Random, Voting }

    public class RoomInfo
    {
        public uint roomId;

        public string name;
        public bool usePassword;

        [JsonConverter(typeof(StringEnumConverter))]
        public RoomState roomState;
        [JsonConverter(typeof(StringEnumConverter))]
        public SongSelectionType songSelectionType;
        public PlayerInfo roomHost;

        public int players;
        public int maxPlayers;

        public bool noFail;

        public byte selectedDifficulty;
        public SongInfo selectedSong;


        public RoomInfo()
        {

        }

        public RoomInfo(NetIncomingMessage msg)
        {
            roomId = msg.ReadUInt32();
            name = msg.ReadString();
            usePassword = msg.ReadBoolean();
            noFail = msg.ReadBoolean();
            msg.SkipPadBits();
            roomState = (RoomState)msg.ReadByte();
            songSelectionType = (SongSelectionType)msg.ReadByte();
            roomHost = new PlayerInfo(msg);
            players = msg.ReadInt32();
            maxPlayers = msg.ReadInt32();
            if (roomState != RoomState.SelectingSong) {
                selectedDifficulty = msg.ReadByte();
                selectedSong = new SongInfo(msg);
            }
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            List<byte> buffer = new List<byte>();

            msg.Write(roomId);
            msg.Write(name);
            msg.Write(usePassword);
            msg.Write(noFail);
            msg.WritePadBits();
            msg.Write((byte)roomState);
            msg.Write((byte)songSelectionType);

            roomHost.AddToMessage(msg);

            msg.Write(players);
            msg.Write(maxPlayers);

            if (selectedSong != null && roomState != RoomState.SelectingSong)
            {
                msg.Write(selectedDifficulty);
                selectedSong.AddToMessage(msg);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is RoomInfo)
            {
                return (name == ((RoomInfo)obj).name) && (usePassword == ((RoomInfo)obj).usePassword) && (players == ((RoomInfo)obj).players) && (maxPlayers == ((RoomInfo)obj).maxPlayers) && (noFail == ((RoomInfo)obj).noFail) && (roomHost.Equals(((RoomInfo)obj).roomHost));
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
            hashCode = hashCode * -1521134295 + noFail.GetHashCode();
            return hashCode;
        }
    }

}
