using System.Collections.Generic;
using Newtonsoft.Json;

namespace ServerCommons.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class ClientDataPacket : Packet, IDataPacket {
        [JsonProperty]
        public ConnectionType ConnectionType { get; set; }
        [JsonProperty]
        public int Offset { get; set; }
        [JsonProperty]
        public List<Data> Servers { get; set; }
    }
}