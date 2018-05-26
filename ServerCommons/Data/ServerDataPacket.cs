using System.Text;
using Newtonsoft.Json;

namespace ServerCommons.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class ServerDataPacket : Packet, IDataPacket {
        [JsonProperty]
        public ConnectionType ConnectionType { get; set; }
        private string _name;
        [JsonProperty]
        public string IPv4 { get; set; }
        [JsonProperty]
        public int Port { get; set; }
        [JsonProperty]
        public bool RemoveFromCollection { get; set; }

        [JsonProperty]
        public string Name {
            get => _name;
            set => _name = value.Length>50 ? value.Substring(0,50) : value.Length == 0 ? "INVALID_NAME" : value;
        }
    }
}