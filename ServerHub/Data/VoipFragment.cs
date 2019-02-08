using Lidgren.Network;

namespace UnityVOIP
{
    public struct VoipFragment
    {
        public ulong playerId;
        public readonly byte[] data;
        public readonly int index;
        public readonly byte mode;
                
        public VoipFragment(NetIncomingMessage msg)
        {
            playerId = msg.ReadUInt64();

            index = msg.ReadInt32();
            mode = msg.ReadByte();

            int voipSize = msg.ReadInt32();
            data = msg.ReadBytes(voipSize);
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(playerId);

            msg.Write(index);
            msg.Write(mode);

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