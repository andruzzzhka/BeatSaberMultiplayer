using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ServerHub.Data
{
    public class RoomSettings
    {
        public string Name;

        public bool UsePassword;
        public string Password;

        public SongSelectionType SelectionType;
        public int MaxPlayers;
        public NoFailType NoFail;

        public RoomSettings()
        {

        }

        public RoomSettings(NetIncomingMessage msg)
        {

            Name = msg.ReadString();

            UsePassword = msg.ReadBoolean();
            NoFail = (NoFailType) msg.ReadByte();

            msg.SkipPadBits();

            if (UsePassword)
                Password = msg.ReadString();

            MaxPlayers = msg.ReadInt32();
            SelectionType = (SongSelectionType)msg.ReadByte();
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(Name);

            msg.Write(UsePassword);
            msg.Write((byte)NoFail);

            msg.WritePadBits();

            if (UsePassword)
                msg.Write(Password);

            msg.Write(MaxPlayers);
            msg.Write((byte)SelectionType);
        }

        public override bool Equals(object obj)
        {
            if(obj is RoomSettings)
            {
                return (Name == ((RoomSettings)obj).Name) && (UsePassword == ((RoomSettings)obj).UsePassword) && (Password == ((RoomSettings)obj).Password) && (MaxPlayers == ((RoomSettings)obj).MaxPlayers) && (NoFail == ((RoomSettings)obj).NoFail);
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
            hashCode = hashCode * -1521134295 + NoFail.GetHashCode();
            return hashCode;
        }
    }
}
