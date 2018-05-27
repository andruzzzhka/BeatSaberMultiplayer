using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServerCommons.Misc;

namespace ServerCommons.Data {
    public abstract class Packet {
        public const int MAX_BYTE_LENGTH = 512;
        
        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static IDataPacket ToPacket(byte[] bytes) {
            var b2s = Encoding.Unicode.GetString(bytes).Trim('\0');
            Logger.Instance.Warning($"TO PACKET: {b2s}");
            var obj = JObject.Parse(b2s);
            switch (Enum.Parse(typeof(ConnectionType), obj["ConnectionType"].Value<string>())) {
                case ConnectionType.Client:
                    return obj.ToObject<ClientDataPacket>();
                case ConnectionType.Hub:
                    break;
                case ConnectionType.Server:
                    return obj.ToObject<ServerDataPacket>();
            }

            return obj.ToObject<IDataPacket>();
        }
        
        public byte[] ToBytes() {
            return new UnicodeEncoding().GetBytes(ToString());
        }
    }
}