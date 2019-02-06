using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.Data
{
    public class VoIPData
    {
        public ulong playerId;
        public byte[] voipSamples = new byte[0];

        public VoIPData(ulong playerId, byte[] voipSamples)
        {
            this.playerId = playerId;
            this.voipSamples = voipSamples;
        }

        public VoIPData(NetIncomingMessage msg)
        {
            playerId = msg.ReadUInt64();

            int voipSize = msg.ReadInt32();
            voipSamples = msg.ReadBytes(voipSize);
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(playerId);

            if (voipSamples != null && voipSamples.Length > 0)
            {
                msg.Write(voipSamples.Length);
                msg.Write(voipSamples);
            }
            else
            {
                msg.Write(0);
            }
        }
    }
}
