using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace ServerHub.Room
{
    class Room
    {
        public uint roomId;

        List<Client> roomClients = new List<Client>();
        RoomSettings roomSettings;
        RoomState roomState;

        List<string> availableSongs = new List<string>();

        PlayerInfo roomHost;

        Timer roomLoopTimer = new Timer(50);

        public Room(RoomSettings settings, PlayerInfo host)
        {
            roomSettings = settings;
            roomHost = host;
        }

        public void StartRoom()
        {
            roomLoopTimer.Elapsed += RoomLoop;
            roomLoopTimer.AutoReset = true;
            roomLoopTimer.Start();
        }

        public void StopRoom()
        {

        }

        public RoomInfo GetRoomInfo()
        {
            return new RoomInfo() { roomId = roomId, name = roomSettings.Name, usePassword = roomSettings.UsePassword, players = roomClients.Count, maxPlayers = roomSettings.MaxPlayers, noFail = roomSettings.NoFail };
        }

        private void RoomLoop(object sender, ElapsedEventArgs e)
        {
            Logger.Instance.Log("LOOP");
        }
    }
}
