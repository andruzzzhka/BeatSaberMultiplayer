using System.Text;
using Newtonsoft.Json;

namespace BeatSaberMultiplayerServer.Misc {
    [JsonObject(MemberSerialization.OptIn)]
    public class DataPacket {
        private string _name;
        [JsonProperty]
        public string IPv4 { get; set; }
        [JsonProperty]
        public int Port { get; set; }
        public const int MAX_BYTE_LENGTH = 256;

        [JsonProperty]
        public string Name {
            get => _name;
            set => _name = value.Length>50 ? value.Substring(0,50) : value.Length == 0 ? "INVALID_NAME" : value;
        }

        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static DataPacket ToPacket(byte[] bytes) {
            var b2s = Encoding.Unicode.GetString(bytes).Trim('\0');
            return JsonConvert.DeserializeObject<DataPacket>(b2s);
        }
        
        public byte[] ToBytes() {
            return new UnicodeEncoding().GetBytes(this.ToString());
        }
    }
}