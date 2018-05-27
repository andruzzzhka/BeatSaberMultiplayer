using System;
using System.Collections.Generic;

namespace BeatSaberMultiplayer.Data {

    [Serializable]
    public class ClientDataPacket : Packet {

        public ConnectionType ConnectionType;
        public int Offset;
        public List<Data> Servers;
    }
}