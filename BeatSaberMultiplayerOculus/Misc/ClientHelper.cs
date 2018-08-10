using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace BeatSaberMultiplayer.Misc
{
    static class ClientHelper
    {

        public static BasePacket ReceiveData(this TcpClient client, bool waitForData = true)
        {
            if (client == null || !client.Connected)
                return null;

            if (!waitForData && client.Available == 0)
                return null;

            byte[] lengthBuffer = new byte[4];
            client.GetStream().Read(lengthBuffer, 0, 4);
            int length = BitConverter.ToInt32(lengthBuffer, 0);

            byte[] dataBuffer = new byte[length];

            int nDataRead = 0;
            int nStartIndex = 0;

            while (nDataRead < length)
            {

                int nBytesRead = client.Client.Receive(dataBuffer, nStartIndex, length - nStartIndex, SocketFlags.None);

                nDataRead += nBytesRead;
                nStartIndex += nBytesRead;
            }

            return new BasePacket(dataBuffer);            
        }

        public static void SendData(this TcpClient client, BasePacket packet)
        {
            if (client == null || !client.Connected)
                return;

            byte[] buffer = packet.ToBytes();
            client.GetStream().Write(buffer, 0, buffer.Length);
        }

    }
}
