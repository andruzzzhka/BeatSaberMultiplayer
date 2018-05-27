using System.Net.Sockets;
using Newtonsoft.Json;

namespace ServerCommons.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class Data {
        public TcpClient TcpClient { get; set; }
        public int ID { get; }
        public bool FirstConnect { get; set; }
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public string IPv4 { get; set; }
        [JsonProperty]
        public int Port { get; set; }

        public Data() {
            ID = -1;
            TcpClient = null;
            Name = "";
            IPv4 = "";
            Port = 0;
        }

        public Data(int id) {
            ID = id;
            TcpClient = null;
            Name = "";
            IPv4 = "";
            Port = 0;
        }
    }
}