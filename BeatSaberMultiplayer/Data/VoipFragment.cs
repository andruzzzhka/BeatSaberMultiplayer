using Lidgren.Network;
using NSpeex;

namespace BeatSaberMultiplayer.VOIP
{
    //https://github.com/DwayneBull/UnityVOIP/blob/master/VoipFragment.cs
    public struct VoipFragment
    {
        public ulong playerId;
        public readonly byte[] data;
        public readonly int index;
        public readonly BandMode mode;

        public VoipFragment(ulong playerId, int index, byte[] data, BandMode mode)
        {
            this.playerId = playerId;
            this.index = index;
            this.data = data;
            this.mode = mode;
        }
        
        public VoipFragment(NetIncomingMessage msg)
        {
            playerId = msg.ReadUInt64();

            index = msg.ReadInt32();
            mode = (BandMode)msg.ReadByte();

            int voipSize = msg.ReadInt32();
            data = msg.ReadBytes(voipSize);
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(playerId);

            msg.Write(index);
            msg.Write((byte)mode);

            if (data != null && data.Length > 0)
            {
                msg.Write(data.Length);
                msg.Write(data);
            }
            else
            {
                msg.Write(0);
            }
        }
    }
}