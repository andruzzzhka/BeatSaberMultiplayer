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
        public static event Action<Client> LostConnection;

        public static BasePacket ReceiveData(this Client client, bool waitForData = true)
        {
            TcpClient tcpClient = client.tcpClient;

            if (tcpClient == null || !tcpClient.Connected)
                return null;

            if (!waitForData && tcpClient.Available == 0)
                return null;

            byte[] dataBuffer = null;

            try
            {
                byte[] lengthBuffer = new byte[4];
                tcpClient.GetStream().Read(lengthBuffer, 0, 4);
                int length = BitConverter.ToInt32(lengthBuffer, 0);

                dataBuffer = new byte[length];
                tcpClient.GetStream().Read(dataBuffer, 0, length);
            }catch(IOException e)
            {
#if DEBUG
                Logger.Instance.Log($"Lost connection to the {client.playerInfo.playerName}: {e}");
#else
                Logger.Instance.Log($"Lost connection to the {client.playerInfo.playerName}.");
#endif
                LostConnection?.Invoke(client);
            }
            catch (Exception)
            {

            }

            return new BasePacket(dataBuffer);            
        }

        public static void SendData(this Client client, BasePacket packet)
        {
            TcpClient tcpClient = client.tcpClient;

            if (tcpClient == null || !tcpClient.Connected)
                return;

            byte[] buffer = packet.ToBytes();

            try
            {
                tcpClient.Client.Send(buffer);
            }catch(IOException e)
            {
#if DEBUG
                Logger.Instance.Log($"Lost connection to the {client.playerInfo.playerName}: {e}");
#else
                Logger.Instance.Log($"Lost connection to the {client.playerInfo.playerName}.");
#endif
                LostConnection?.Invoke(client);
            }
            
        }

    }
}
