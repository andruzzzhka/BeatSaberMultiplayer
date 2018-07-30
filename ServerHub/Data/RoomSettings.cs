using System;
using System.Collections.Generic;
using System.Text;

namespace ServerHub.Data
{
    public class RoomSettings
    {
        public string Name;

        public bool UsePassword;
        public string Password;

        public int MaxPlayers;
        public bool NoFail;


        public RoomSettings()
        {

        }

        public RoomSettings(byte[] data)
        {
            if(data.Length > 9)
            {
                int nameLength = BitConverter.ToInt32(data, 0);
                Name = Encoding.UTF8.GetString(data, 4, nameLength);

                UsePassword = (data[4 + nameLength] == 0) ? false : true;
                
                int passLength = BitConverter.ToInt32(data, 5 + nameLength);
                Password = Encoding.UTF8.GetString(data, 9 + nameLength, passLength);

                MaxPlayers = BitConverter.ToInt32(data, 9 + nameLength + passLength);

                NoFail = (data[13 + nameLength + passLength] == 0) ? false : true;
            }
        }

        public byte[] ToBytes()
        {
            List<byte> buffer = new List<byte>();

            byte[] nameStr = Encoding.UTF8.GetBytes(Name);
            buffer.AddRange(BitConverter.GetBytes(nameStr.Length));
            buffer.AddRange(nameStr);

            buffer.Add(UsePassword ? (byte)1 : (byte)0);

            byte[] passStr = Encoding.UTF8.GetBytes(Password);
            buffer.AddRange(BitConverter.GetBytes(passStr.Length));
            buffer.AddRange(passStr);
            
            buffer.AddRange(BitConverter.GetBytes(MaxPlayers));

            buffer.Add(NoFail ? (byte)1 : (byte)0);

            buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));

            return buffer.ToArray();
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
