using System.Linq;
using System.Text;

namespace BeatSaberMultiplayerServer.Misc {
    public class DataPacket {
        private string _name;
        public string IPv4 { get; set; }
        public int Port { get; set; }
        public const int MAX_BYTE_LENGTH = 138;

        public string Name {
            get => _name;
            set => _name = value.Length>50 ? value.Substring(0,50) : value.Length == 0 ? "INVALID_NAME" : value;
        }

        public static DataPacket ToPacket(byte[] bytes) {
            var parts = Encoding.Unicode.GetString(bytes).Split(Special);
            return new DataPacket{Name = parts[0], IPv4 = parts[1], Port = int.Parse(parts[2])};
        }

        private const char Special = '�';
        
        public byte[] ToBytes() {
            return new UnicodeEncoding().GetBytes($"{Name}{Special}{IPv4}{Special}{Port}");
        }
    }
}