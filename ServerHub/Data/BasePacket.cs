using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerHub.Data
{
    public enum CommandType : byte { Connect, Disconnect, GetRooms, CreateRoom, JoinRoom, GetRoomInfo, LeaveRoom, DestroyRoom, TransferHost, SetSelectedSong, StartLevel, UpdatePlayerInfo }

    public class BasePacket
    {
        public BasePacket(CommandType command, byte[] data)
        {
            commandType = command;
            additionalData = data;
        }

        public BasePacket(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                commandType = (CommandType)data[0];

                if (data.Length > 1)
                {
                    additionalData = data.Skip(1).Take(data.Length - 1).ToArray();
                }
                else
                {
                    additionalData = new byte[0];
                }

            }
        }

        public CommandType commandType;
        public byte[] additionalData;

        public byte[] ToBytes()
        {
            List<byte> buffer = new List<byte>();
            buffer.AddRange(BitConverter.GetBytes(additionalData.Length + 1));
            buffer.Add((byte)commandType);
            buffer.AddRange(additionalData);
            return buffer.ToArray();
        }

        public override bool Equals(object obj)
        {
            if(obj is BasePacket)
            {
                return ((obj as BasePacket).commandType == commandType) && ((obj as BasePacket).additionalData.SequenceEqual(additionalData));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashCode = 1611545263;
            hashCode = hashCode * -1521134295 + commandType.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(additionalData);
            return hashCode;
        }
    }
}
