using ServerHub.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ServerHub.Misc
{
    static class ClientHelper
    {

        public static BasePacket ReceiveData(this TcpClient client, bool waitForData = true)
        {
            if (client == null || !client.Connected)
                return null;

            if (!waitForData && client.Available == 0)
                return null;

            byte[] dataBuffer = null;

            try
            {
                byte[] lengthBuffer = new byte[4];
                client.GetStream().Read(lengthBuffer, 0, 4);
                int length = BitConverter.ToInt32(lengthBuffer, 0);

                dataBuffer = new byte[length];
                client.GetStream().Read(dataBuffer, 0, length);
            }catch(IOException e)
            {
                Logger.Instance.Log($"Lost connection to the client: {e}");
            }

            return new BasePacket(dataBuffer);            
        }

        public static bool SendData(this TcpClient client, BasePacket packet)
        {
            if (client == null || !client.Connected)
                return false;

            byte[] buffer = packet.ToBytes();
            int result = 0;

            try
            {
                result = client.Client.Send(buffer);
            }catch(IOException e)
            {
                Logger.Instance.Log($"Lost connection to the client: {e}");
            }

            return result == buffer.Length;
        }

    }
}
