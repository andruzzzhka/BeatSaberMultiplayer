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

        public List<Client> roomClients = new List<Client>();
        public RoomSettings roomSettings;
        public RoomState roomState;

        public List<string> availableSongs = new List<string>();

        public PlayerInfo roomHost;

        public Room(uint id, RoomSettings settings, PlayerInfo host)
        {
            roomId = id;
            roomSettings = settings;
            roomHost = host;
        }

        public void StartRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed += RoomLoop;
        }

        public void StopRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed -= RoomLoop;
        }

        public RoomInfo GetRoomInfo()
        {
            return new RoomInfo() { roomId = roomId, name = roomSettings.Name, usePassword = roomSettings.UsePassword, players = roomClients.Count, maxPlayers = roomSettings.MaxPlayers, noFail = roomSettings.NoFail, roomHost = roomHost, roomState = roomState };
        }

        private void RoomLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            //Logger.Instance.Log($"LOOP {e.Delay}");
        }
    }
}
