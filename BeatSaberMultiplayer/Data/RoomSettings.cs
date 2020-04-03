using Lidgren.Network;
using BeatSaberMultiplayer.SimpleJSON;
using System;
using System.Collections.Generic;

namespace BeatSaberMultiplayer.Data
{
    [Serializable]
    public class RoomSettings
    {
        public string Name;

        public bool UsePassword;
        public string Password;

        public SongSelectionType SelectionType;
        public int MaxPlayers;
        public float ResultsShowTime;
        public bool PerPlayerDifficulty;
        
        public RoomSettings()
        {

        }

        public RoomSettings(JSONNode node)
        {
            Name = node["Name"];
            UsePassword = node["UsePassword"];
            Password = node["Password"];
            SelectionType = (SongSelectionType)node["SelectionType"].AsInt;
            MaxPlayers = node["MaxPlayers"];
            ResultsShowTime = node["ResultsShowTime"];
            PerPlayerDifficulty = node["PerPlayerDifficulty"];
        }
        
        public RoomSettings(NetIncomingMessage msg)
        {

            Name = msg.ReadString();

            UsePassword = msg.ReadBoolean();
            PerPlayerDifficulty = msg.ReadBoolean();

            msg.SkipPadBits();

            if (UsePassword)
                Password = msg.ReadString();

            MaxPlayers = msg.ReadInt32();
            ResultsShowTime = msg.ReadSingle();
            SelectionType = (SongSelectionType)msg.ReadByte();

        }
        
        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(Name);

            msg.Write(UsePassword);
            msg.Write(PerPlayerDifficulty);

            msg.WritePadBits();

            if (UsePassword)
                msg.Write(Password);

            msg.Write(MaxPlayers);
            msg.Write(ResultsShowTime);
            msg.Write((byte)SelectionType);
        }

        public override bool Equals(object obj)
        {
            if (obj is RoomSettings)
            {
                return (Name == ((RoomSettings)obj).Name) && (UsePassword == ((RoomSettings)obj).UsePassword) && (Password == ((RoomSettings)obj).Password) && (MaxPlayers == ((RoomSettings)obj).MaxPlayers) && (PerPlayerDifficulty == ((RoomSettings)obj).PerPlayerDifficulty) && (ResultsShowTime == ((RoomSettings)obj).ResultsShowTime);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = -1123100830;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + UsePassword.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Password);
            hashCode = hashCode * -1521134295 + MaxPlayers.GetHashCode();
            hashCode = hashCode * -1521134295 + PerPlayerDifficulty.GetHashCode();
            hashCode = hashCode * -1521134295 + ResultsShowTime.GetHashCode();
            return hashCode;
        }
    }
}
