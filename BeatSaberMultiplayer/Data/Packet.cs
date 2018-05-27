using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BeatSaberMultiplayer.Data {
    [Serializable]
    public abstract class Packet {
        public const int MAX_BYTE_LENGTH = 2048;
        
        public override string ToString() {
            return JsonUtility.ToJson(this,true);
        }

        public static ClientDataPacket ToClientDataPacket(byte[] bytes) {
            var b2s = Encoding.Unicode.GetString(bytes).Trim('\0');
            var obj = JSON.Parse(b2s);
            Console.WriteLine($"TO CLIENT DATA PACKET: {Environment.NewLine}{obj.ToString(4)}");
            if ((ConnectionType)Enum.Parse(typeof(ConnectionType), obj["ConnectionType"].Value) == ConnectionType.Client)
            {
                List<Data> dataList = new List<Data>();
                foreach(JSONNode node in obj["Servers"].Children)
                {
                    dataList.Add(new Data { Name = node["Name"], IPv4 = node["IPv4"], Port = node["Port"] });
                }
                return new ClientDataPacket { ConnectionType = ConnectionType.Client, Offset = obj["Offset"].AsInt, Servers = dataList };
            }
            Console.WriteLine("Can't parse ClientDataPacket!");
            return null;            
        }
        
        public byte[] ToBytes() {
            Console.WriteLine("Packet: "+ToString());
            return new UnicodeEncoding().GetBytes(ToString());
        }
    }
}