using ServerHub.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServerHub.Hub
{
    class RCONStructs
    {

        public struct OutgoingMessage
        {
            public string Message { get; set; }
            public int Identifier { get; set; }
            public string Type { get; set; }
            public string Stacktrace { get; set; }
        }

        public struct IncomingMessage
        {
            public int Identifier { get; set; }
            public string Message { get; set; }
            public string Name { get; set; }
        }

        public struct ServerInfo
        {
            public string Hostname { get; set; }
            public int MaxPlayers { get; set; }
            public int Players { get; set; }
            public int EntityCount { get; set; }
            public int Uptime { get; set; }
            public double Framerate { get; set; }
            public int Memory { get; set; }
            public int NetworkIn { get; set; }
            public int NetworkOut { get; set; }
        }

        public struct RCONPlayerInfo
        {
            public string SteamID;
            public string DisplayName;
            public int Ping;

            public RCONPlayerInfo(PlayerInfo player)
            {
                SteamID = player.playerId.ToString();
                DisplayName = player.playerName;
                Ping = 0;
            }
        }
    }
}
