using System;
using System.Net.Sockets;

namespace BeatSaberMultiplayer.Data {
    [Serializable]
    public class Data {
        [NonSerialized]
        public TcpClient TcpClient;

        public int ID;
        public bool FirstConnect;
        public string Name;
        public string IPv4;
        public int Port;

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