using Lidgren.Network;
using System.Collections.Generic;

namespace BeatSaberMultiplayer.Data
{
    public enum RoomState: byte {SelectingSong, Preparing, InGame, Results }
    public enum SongSelectionType : byte { Manual,  Random }

    public class RoomInfo
    {
        public uint roomId;

        public string name;
        public bool usePassword;
        public bool perPlayerDifficulty;
        public bool noHost;

        public RoomState roomState;
        public SongSelectionType songSelectionType;
        public PlayerInfo roomHost;

        public int players;
        public int maxPlayers;

        public bool songSelected;
        
        public LevelOptionsInfo startLevelInfo;
        public SongInfo selectedSong
        {
            get { return _selectedSong; }
            set { _selectedSong = value; songSelected = (selectedSong != null && roomState != RoomState.SelectingSong); }
        }
        private SongInfo _selectedSong;


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
            noHost = msg.ReadBoolean();
            msg.SkipPadBits();
            roomState = (RoomState)msg.ReadByte();
            songSelectionType = (SongSelectionType)msg.ReadByte();

            if(!noHost)
                roomHost = new PlayerInfo(msg);

            players = msg.ReadInt32();
            maxPlayers = msg.ReadInt32();
            startLevelInfo = new LevelOptionsInfo(msg);

            if (songSelected)
            {
                selectedSong = new SongInfo(msg);
            }
            else
            {
                selectedSong = null;
            }
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(roomId);
            msg.Write(name);
            msg.Write(usePassword);
            msg.Write(perPlayerDifficulty);
            msg.Write(songSelected);
            msg.Write(noHost);
            msg.WritePadBits();
            msg.Write((byte)roomState);
            msg.Write((byte)songSelectionType);

            if(!noHost)
                roomHost.AddToMessage(msg);

            msg.Write(players);
            msg.Write(maxPlayers);
            startLevelInfo.AddToMessage(msg);

            if (songSelected)
            {
                selectedSong.AddToMessage(msg);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is RoomInfo)
            {
                return (name == ((RoomInfo)obj).name) && (usePassword == ((RoomInfo)obj).usePassword) && (players == ((RoomInfo)obj).players) && (maxPlayers == ((RoomInfo)obj).maxPlayers);
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

        public static string StateToActivityState(RoomState state)
        {
            switch (state)
            {
                case RoomState.InGame: return "Playing";
                case RoomState.Preparing: return "Preparing";
                case RoomState.Results: return "Viewing results";
                case RoomState.SelectingSong: return "Selecting song";
            }
            return "Unknown";
        }
    }

}
