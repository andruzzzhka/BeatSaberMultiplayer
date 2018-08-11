using ServerHub.Data;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Timers;

namespace ServerHub.Rooms
{
    class Room
    {
        public uint roomId;

        public List<Client> roomClients = new List<Client>();
        public RoomSettings roomSettings;
        public RoomState roomState;

        public PlayerInfo roomHost;

        public SongInfo selectedSong;
        public byte selectedDifficulty;

        private DateTime _songStartTime;
        private DateTime _resultsStartTime;

        public const float resultsShowTime = 15f;

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
            return new RoomInfo() { roomId = roomId, name = roomSettings.Name, usePassword = roomSettings.UsePassword, players = roomClients.Count, maxPlayers = roomSettings.MaxPlayers, noFail = roomSettings.NoFail, roomHost = roomHost, roomState = roomState, songSelectionType=roomSettings.SelectionType, selectedSong = selectedSong, selectedDifficulty = selectedDifficulty };
        }

        public byte[] GetSongsLevelIDs()
        {
            List<byte> buffer = new List<byte>();

            buffer.AddRange(BitConverter.GetBytes(roomSettings.AvailableSongs.Count));

            roomSettings.AvailableSongs.ForEach(x => buffer.AddRange(HexConverter.ConvertHexToBytesX(x.levelId)));

            return buffer.ToArray();
        }

        private void RoomLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if (roomState == RoomState.InGame)
            {
                if (DateTime.Now.Subtract(_songStartTime).TotalSeconds >= selectedSong.songDuration)
                {
                    roomState = RoomState.Results;
                    _resultsStartTime = DateTime.Now;
                }
            }

            if(roomState == RoomState.Results)
            {
                if (DateTime.Now.Subtract(_resultsStartTime).TotalSeconds >= resultsShowTime)
                {
                    roomState = RoomState.SelectingSong;
                    selectedSong = null;
                    BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, new byte[0]));
                }
            }

            if (roomSettings.SelectionType == SongSelectionType.Random && roomState == RoomState.SelectingSong)
            {
                roomState = RoomState.Preparing;
                Random rand = new Random();
                selectedSong = roomSettings.AvailableSongs[rand.Next(0, roomSettings.AvailableSongs.Count-1)];
                BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, selectedSong.ToBytes(false)));
            }

            List<byte> buffer = new List<byte>();
            switch (roomState)
            {
                case RoomState.SelectingSong:
                case RoomState.Preparing:
                    {
                        buffer.AddRange(new byte[8]);
                    }
                    break;
                case RoomState.InGame:
                    {
                        buffer.AddRange(BitConverter.GetBytes((float)DateTime.Now.Subtract(_songStartTime).TotalSeconds));
                        buffer.AddRange(BitConverter.GetBytes(selectedSong.songDuration));
                    }
                    break;
                case RoomState.Results:
                    {
                        buffer.AddRange(BitConverter.GetBytes((float)DateTime.Now.Subtract(_resultsStartTime).TotalSeconds));
                        buffer.AddRange(BitConverter.GetBytes(resultsShowTime));
                    }
                    break;
            }
            buffer.AddRange(BitConverter.GetBytes(roomClients.Count));
            roomClients.ForEach(x => buffer.AddRange(x.playerInfo.ToBytes()));
            BroadcastPacket(new BasePacket(CommandType.UpdatePlayerInfo, buffer.ToArray()));
        }

        public void BroadcastPacket(BasePacket packet)
        {
            foreach (Client client in roomClients)
            {
                try
                {
                    client.SendData(packet);
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"Can't send packet to {client.playerInfo.playerName}! Exception: {e}");
                }
            }
        }

        public void SetSelectedSong(PlayerInfo sender, SongInfo song)
        {
            if (sender.Equals(roomHost))
            {
                selectedSong = song;
                if (selectedSong == null)
                {
                    switch (roomSettings.SelectionType)
                    {
                        case SongSelectionType.Manual:
                            {

                                roomState = RoomState.SelectingSong;
                                BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, new byte[0]));
                            }
                            break;
                        case SongSelectionType.Random:
                            {
                                roomState = RoomState.Preparing;
                                Random rand = new Random();
                                selectedSong = roomSettings.AvailableSongs[rand.Next(0, roomSettings.AvailableSongs.Count - 1)];
                                BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, selectedSong.ToBytes(false)));
                            }
                            break;
                    }
                }
                else
                {
                    roomState = RoomState.Preparing;
                    BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, selectedSong.ToBytes(false)));
                }
            }
        }

        public void StartLevel(PlayerInfo sender, byte difficulty, SongInfo song)
        {
            if (sender.Equals(roomHost))
            {
                selectedSong = song;
                selectedDifficulty = difficulty;

                List<byte> buffer = new List<byte>();
                buffer.Add(selectedDifficulty);
                buffer.AddRange(selectedSong.ToBytes(false));

                BroadcastPacket(new BasePacket(CommandType.StartLevel, buffer.ToArray()));

                roomState = RoomState.InGame;
                _songStartTime = DateTime.Now;
            }
        }

        public void TransferHost(PlayerInfo sender, PlayerInfo newHost)
        {
            if (sender.Equals(roomHost))
            {
                roomHost = newHost;
                BroadcastPacket(new BasePacket(CommandType.TransferHost, roomHost.ToBytes(false)));
            }
        }
    }
}
