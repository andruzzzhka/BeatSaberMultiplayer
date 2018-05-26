using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ServerHub.Misc;

namespace ServerHub.Handlers {
    [JsonObject(MemberSerialization.OptIn)]
    public class DataPacket {
        private string _name;
        [JsonProperty]
        public string IPv4 { get; set; }
        [JsonProperty]
        public int Port { get; set; }
        public const int MAX_BYTE_LENGTH = 255;

        [JsonProperty]
        public string Name {
            get => _name;
            set => _name = value.Length>50 ? value.Substring(0,50) : value.Length == 0 ? "INVALID_NAME" : value;
        }

        /*public static DataPacket ToPacket(byte[] bytes) {
            var parts = Encoding.Unicode.GetString(bytes).Split(Special);
            return new DataPacket{Name = parts[0], IPv4 = parts[1], Port = int.Parse(parts[2])};
        }*/

        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static DataPacket ToPacket(byte[] bytes) {
            string s = "[";
            foreach (var b in bytes) {
                s += $"{((int) b).ToString()}, ";
            }
            s= s.TrimEnd(", ".ToCharArray()) + "]";
            
            Logger.Instance.Log($"Given Bytes: {s}");
            var b2s = Encoding.Unicode.GetString(bytes);
            Logger.Instance.Log($"Bytes to String: {b2s}");
            return JsonConvert.DeserializeObject<DataPacket>(b2s);
        }

        //private const char Special = '�';
        
        /*public byte[] ToBytes() {
            return new UnicodeEncoding().GetBytes($"{Name}{Special}{IPv4}{Special}{Port}");
        }*/
        
        public byte[] ToBytes() {
            return new UnicodeEncoding().GetBytes(this.ToString());
        }
    }
}