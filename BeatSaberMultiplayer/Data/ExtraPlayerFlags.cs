using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
using System;
using System.Collections;

namespace BeatSaberMultiplayer.Data
{
    public struct ExtraPlayerFlags : IEquatable<ExtraPlayerFlags>
    {
        public bool rainbowName;
        public bool unused1;
        public bool unused2;
        public bool unused3;
        public bool unused4;
        public bool unused5;
        public bool unused6;
        public bool unused7;

        public ExtraPlayerFlags(bool rainbowName)
        {
            this.rainbowName = rainbowName;
            unused1 = false;
            unused2 = false;
            unused3 = false;
            unused4 = false;
            unused5 = false;
            unused6 = false;
            unused7 = false;
        }

        public ExtraPlayerFlags(NetIncomingMessage msg)
        {
            rainbowName = msg.ReadBoolean();
            unused1 = msg.ReadBoolean();
            unused2 = msg.ReadBoolean();
            unused3 = msg.ReadBoolean();
            unused4 = msg.ReadBoolean();
            unused5 = msg.ReadBoolean();
            unused6 = msg.ReadBoolean();
            unused7 = msg.ReadBoolean();
        }

        public BitArray GetBitArray()
        {
            BitArray flags = new BitArray(8);

            flags[0] = rainbowName;
            flags[1] = unused1;
            flags[2] = unused2;
            flags[3] = unused3;
            flags[4] = unused4;
            flags[5] = unused5;
            flags[6] = unused6;
            flags[7] = unused7;

            return flags;
        }

        public byte[] ToBytes()
        {
            return GetBitArray().ToBytes();
        }

        public void AddToMessage(NetOutgoingMessage outMsg)
        {
            outMsg.Write(rainbowName);
            outMsg.Write(unused1);
            outMsg.Write(unused2);
            outMsg.Write(unused3);
            outMsg.Write(unused4);
            outMsg.Write(unused5);
            outMsg.Write(unused6);
            outMsg.Write(unused7);
        }

        public override bool Equals(object obj)
        {
            return obj is ExtraPlayerFlags flags &&
                   rainbowName == flags.rainbowName &&
                   unused1 == flags.unused1 &&
                   unused2 == flags.unused2 &&
                   unused3 == flags.unused3 &&
                   unused4 == flags.unused4 &&
                   unused5 == flags.unused5 &&
                   unused6 == flags.unused6 &&
                   unused7 == flags.unused7;
        }

        public override int GetHashCode()
        {
            var hashCode = 2127499901;
            hashCode = hashCode * -1521134295 + rainbowName.GetHashCode();
            hashCode = hashCode * -1521134295 + unused1.GetHashCode();
            hashCode = hashCode * -1521134295 + unused2.GetHashCode();
            hashCode = hashCode * -1521134295 + unused3.GetHashCode();
            hashCode = hashCode * -1521134295 + unused4.GetHashCode();
            hashCode = hashCode * -1521134295 + unused5.GetHashCode();
            hashCode = hashCode * -1521134295 + unused6.GetHashCode();
            hashCode = hashCode * -1521134295 + unused7.GetHashCode();
            return hashCode;
        }

        public bool Equals(ExtraPlayerFlags other)
        {
            return rainbowName == other.rainbowName &&
                   unused1 == other.unused1 &&
                   unused2 == other.unused2 &&
                   unused3 == other.unused3 &&
                   unused4 == other.unused4 &&
                   unused5 == other.unused5 &&
                   unused6 == other.unused6 &&
                   unused7 == other.unused7;
        }

        public static bool operator ==(ExtraPlayerFlags c1, ExtraPlayerFlags c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(ExtraPlayerFlags c1, ExtraPlayerFlags c2)
        {
            return !c1.Equals(c2);
        }
    }
}
