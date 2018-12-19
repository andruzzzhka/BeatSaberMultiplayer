using ServerHub.Hub;
using ServerHub.Data;
using ServerHub.Misc;
using Logger = ServerHub.Misc.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Timers;
using WebSocketSharp;
using Newtonsoft.Json;

namespace ServerHub.Rooms
{
    public struct WebSocketPacket
    {
        public string commandType;
        public object data;

        public WebSocketPacket(CommandType command, object d)
        {
            commandType = command.ToString();
            data = d;
        }
    }

    public struct ReadyPlayers
    {
        public int readyPlayers, roomClients;

        public ReadyPlayers(int ready, int clients)
        {
            readyPlayers = ready;
            roomClients = clients;
        }
    }

    public struct SongWithDifficulty
    {
        public SongInfo song;
        public byte difficulty;

        public SongWithDifficulty(SongInfo songInfo, byte diff)
        {
            song = songInfo;
            difficulty = diff;
        }
    }

    public struct DisplayMessage
    {
        float displayTime;
        float fontSize;
        string message;

        public DisplayMessage(float displayTime, float fontSize, string message)
        {
            this.displayTime = displayTime;
            this.fontSize = fontSize;
            this.message = message;
        }
    }

    public struct EventMessage
    {
        string header;
        string data;

        public EventMessage(string header, string data)
        {
            this.header = header;
            this.data = data;
        }
    }

    public class BaseRoom
    {
        public uint roomId;

        public List<Client> roomClients = new List<Client>();

        private List<PlayerInfo> _readyPlayers = new List<PlayerInfo>();
        private Dictionary<PlayerInfo, SongInfo> _votes = new Dictionary<PlayerInfo, SongInfo>();

        public RoomSettings roomSettings;
        public RoomState roomState;

        public bool noHost;
        public PlayerInfo roomHost;

        public SongInfo selectedSong;
        public byte selectedDifficulty;

        private DateTime _songStartTime;
        private DateTime _resultsStartTime;
        private DateTime _votingStartTime;

        public const float resultsShowTime = 15f;
        public const float votingTime = 30f;

        public BaseRoom(uint id, RoomSettings settings, PlayerInfo host)
        {
            roomId = id;
            roomSettings = settings;
            roomHost = host;
        }

        public virtual void StartRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed += RoomLoop;
            if (roomSettings.SelectionType == SongSelectionType.Voting)
            {
                _votingStartTime = DateTime.Now;
            }
        }

        public virtual void StopRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed -= RoomLoop;
        }

        public virtual RoomInfo GetRoomInfo()
        {
            return new RoomInfo() { roomId = roomId, name = roomSettings.Name, usePassword = roomSettings.UsePassword, players = roomClients.Count, maxPlayers = roomSettings.MaxPlayers, noFail = roomSettings.NoFail, roomHost = roomHost, roomState = roomState, songSelectionType = roomSettings.SelectionType, selectedSong = selectedSong, selectedDifficulty = selectedDifficulty };
        }

        public byte[] GetSongInfos()
        {
            List<byte> buffer = new List<byte>();

            buffer.AddRange(BitConverter.GetBytes(roomSettings.AvailableSongs.Count));

            roomSettings.AvailableSongs.ForEach(x => buffer.AddRange(x.ToBytes()));

            return buffer.ToArray();
        }

        public virtual void PlayerLeft(Client player)
        {
            _votes.Remove(player.playerInfo);
            _readyPlayers.Remove(player.playerInfo);
            if(roomState == RoomState.Preparing)
                UpdatePlayersReady();

            roomClients.Remove(player);
            if (roomClients.Count > 0)
            {
                if(player.playerInfo.Equals(roomHost))
                        TransferHost(player.playerInfo, roomClients.Random().playerInfo);
            }
            else
            {
                if (!Settings.Instance.TournamentMode.Enabled)
                    DestroyRoom(player.playerInfo);
            }
        }

        public virtual void RoomLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            List<byte> buffer = new List<byte>();
            switch (roomState)
            {
                case RoomState.InGame:
                    {
                        if (DateTime.Now.Subtract(_songStartTime).TotalSeconds >= selectedSong.songDuration)
                        {
                            roomState = RoomState.Results;
                            _resultsStartTime = DateTime.Now;

                            buffer.Add(0);
                            buffer.AddRange(GetRoomInfo().ToBytes());
                            BroadcastPacket(new BasePacket(CommandType.GetRoomInfo, buffer.ToArray()));

                            if (roomClients.Count > 0)
                                BroadcastWebSocket(CommandType.GetRoomInfo, GetRoomInfo());
                        }
                    }
                    break;
                case RoomState.Results:
                    {
                        if (DateTime.Now.Subtract(_resultsStartTime).TotalSeconds >= resultsShowTime)
                        {
                            roomState = RoomState.SelectingSong;
                            selectedSong = null;
                            BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, new byte[0]));
                            BroadcastWebSocket(CommandType.SetSelectedSong, null);
                            if(roomSettings.SelectionType == SongSelectionType.Voting)
                            {
                                _votingStartTime = DateTime.Now;
                            }
                        }
                    }
                    break;
                case RoomState.SelectingSong:
                    {
                        switch (roomSettings.SelectionType)
                        {
                            case SongSelectionType.Random:
                                {
                                    roomState = RoomState.Preparing;
                                    Random rand = new Random();
                                    selectedSong = roomSettings.AvailableSongs[rand.Next(roomSettings.AvailableSongs.Count)];
                                    BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, selectedSong.ToBytes(false)));
                                    BroadcastWebSocket(CommandType.SetSelectedSong, selectedSong);
                                    ReadyStateChanged(roomHost, true);
                                }
                                break;
                            case SongSelectionType.Voting:
                                {
                                    if (DateTime.Now.Subtract(_votingStartTime).TotalSeconds >= votingTime)
                                    {
                                        roomState = RoomState.Preparing;
                                        if (_votes.Count > 0)
                                        {
                                            selectedSong = _votes.GroupBy(x => x.Value).OrderByDescending(y => y.Count()).First().Key;
                                        }
                                        else
                                        {
                                            Random rand = new Random();
                                            selectedSong = roomSettings.AvailableSongs[rand.Next(roomSettings.AvailableSongs.Count)];
                                        }
                                        BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, selectedSong.ToBytes(false)));
                                        ReadyStateChanged(roomHost, true);
                                        _votes.Clear();
                                    }
                                }
                                break;
                        }
                    }
                    break;
            }
            buffer.Clear();
            switch (roomState)
            {
                case RoomState.SelectingSong:
                    {
                        if (roomSettings.SelectionType == SongSelectionType.Voting)
                        {
                            buffer.AddRange(BitConverter.GetBytes((float)DateTime.Now.Subtract(_votingStartTime).TotalSeconds));
                            buffer.AddRange(BitConverter.GetBytes(votingTime));
                        }
                        else
                        {
                            buffer.AddRange(new byte[8]);
                        }
                    }
                    break;
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

            if (roomClients.Count > 0)
                BroadcastWebSocket(CommandType.UpdatePlayerInfo, roomClients.Select(x => x.playerInfo).ToArray());
        }

        public virtual void BroadcastPacket(BasePacket packet)
        {
            for (int i = 0; i < roomClients.Count; i++)
            {
                try
                {
                    roomClients[i].SendData(packet);
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"Unable to send packet to {roomClients[i].playerInfo.playerName}! Exception: {e}");
                }
            }
        }

        public virtual void BroadcastEventMessage(string header, string data, List<Client> excludeClients = null)
        {
            List<byte> buffer = new List<byte>();

            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            buffer.AddRange(BitConverter.GetBytes(headerBytes.Length));
            buffer.AddRange(headerBytes);
            buffer.AddRange(dataBytes);

            BasePacket packet = new BasePacket(CommandType.SendEventMessage, buffer.ToArray());

            for (int i = 0; i < roomClients.Count; i++)
            {
                try
                {
                    if((excludeClients != null && !excludeClients.Contains(roomClients[i])) || excludeClients == null)
                        roomClients[i].SendData(packet);
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"Unable to send packet to {roomClients[i].playerInfo.playerName}! Exception: {e}");
                }
            }
        }

        public virtual void OnOpenWebSocket()
        {
            if (WebSocketListener.Server == null || !Settings.Instance.Server.EnableWebSocketRoomInfo)
                return;

            var service = WebSocketListener.Server.WebSocketServices[$"/room/{roomId}"];

            try
            {
                RoomInfo roomInfo = GetRoomInfo();

                WebSocketPacket packet = new WebSocketPacket(CommandType.GetRoomInfo, roomInfo);
                string serialized = JsonConvert.SerializeObject(packet);
                service.Sessions.BroadcastAsync(serialized, null);

                if (roomInfo.roomState == RoomState.Preparing)
                {
                    ReadyPlayers readyPlayers = new ReadyPlayers(_readyPlayers.Count, roomClients.Count);

                    packet = new WebSocketPacket(CommandType.PlayerReady, readyPlayers);
                    serialized = JsonConvert.SerializeObject(packet);
                    service.Sessions.BroadcastAsync(serialized, null);
                }
            }catch(Exception e)
            {
                Logger.Instance.Warning("Unable to send RoomInfo to WebSocket client! Exception: " + e);
            }
        }

        public virtual void BroadcastWebSocket(CommandType commandType, object data)
        {
            if (WebSocketListener.Server == null || !Settings.Instance.Server.EnableWebSocketRoomInfo)
                return;

            WebSocketPacket packet = new WebSocketPacket(commandType, data);
            string serialized = JsonConvert.SerializeObject(packet);

            var service = WebSocketListener.Server.WebSocketServices[$"/room/{roomId}"];
            service.Sessions.BroadcastAsync(serialized, null);
        }

        public virtual void SetSelectedSong(PlayerInfo sender, SongInfo song)
        {
            if (sender.Equals(roomHost))
            {
                selectedSong = song;
                if (selectedSong == null)
                {
                    switch (roomSettings.SelectionType)
                    {
                        case SongSelectionType.Voting:
                        case SongSelectionType.Manual:
                            {

                                roomState = RoomState.SelectingSong;
                                BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, new byte[0]));
                                BroadcastWebSocket(CommandType.SetSelectedSong, null);
                                if (roomSettings.SelectionType == SongSelectionType.Voting)
                                {
                                    _votingStartTime = DateTime.Now;
                                }
                            }
                            break;
                        case SongSelectionType.Random:
                            {
                                roomState = RoomState.Preparing;
                                Random rand = new Random();
                                selectedSong = roomSettings.AvailableSongs[rand.Next(0, roomSettings.AvailableSongs.Count - 1)];
                                BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, selectedSong.ToBytes(false)));
                                BroadcastWebSocket(CommandType.SetSelectedSong, selectedSong);
                                ReadyStateChanged(roomHost, true);
                            }
                            break;
                    }
                }
                else
                {
                    if (roomSettings.SelectionType == SongSelectionType.Voting)
                    {
                        _votes.Remove(sender);
                        _votes.Add(sender, song);
                    }
                    else
                    {
                        roomState = RoomState.Preparing;
                        BroadcastPacket(new BasePacket(CommandType.SetSelectedSong, selectedSong.ToBytes(false)));
                        BroadcastWebSocket(CommandType.SetSelectedSong, selectedSong);
                        ReadyStateChanged(roomHost, true);
                    }
                }
            }
            else
            {
                if (roomSettings.SelectionType == SongSelectionType.Voting)
                {
                    _votes.Remove(sender);
                    _votes.Add(sender, song);
                }
                else
                {
                    Logger.Instance.Warning($"{sender.playerName}:{sender.playerId} tried to select song, but he is not the host");
                }
            }
        }

        public virtual void StartLevel(PlayerInfo sender, byte difficulty, SongInfo song)
        {
            if (sender.Equals(roomHost))
            {
                selectedSong = song;
                selectedDifficulty = difficulty;

                SongWithDifficulty songWithDifficulty = new SongWithDifficulty(song, difficulty);

                List<byte> buffer = new List<byte>();
                buffer.Add(selectedDifficulty);
                buffer.AddRange(selectedSong.ToBytes(false));

                BroadcastPacket(new BasePacket(CommandType.StartLevel, buffer.ToArray()));
                BroadcastWebSocket(CommandType.StartLevel, songWithDifficulty);

                roomState = RoomState.InGame;
                _songStartTime = DateTime.Now;
                _readyPlayers.Clear();
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}:{sender.playerId} tried to start the level, but he is not the host");
            }
        }

        public virtual void TransferHost(PlayerInfo sender, PlayerInfo newHost)
        {
            if (sender.Equals(roomHost))
            {
                roomHost = newHost;
                BroadcastPacket(new BasePacket(CommandType.TransferHost, roomHost.ToBytes(false)));
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}:{sender.playerId} tried to transfer host, but he is not the host");
            }
        }

        public virtual void ForceTransferHost(PlayerInfo newHost)
        {
            roomHost = newHost;
            BroadcastPacket(new BasePacket(CommandType.TransferHost, roomHost.ToBytes(false)));
        }

        public virtual void ReadyStateChanged(PlayerInfo sender, bool ready)
        {
            if (ready)
            {
                if (!_readyPlayers.Contains(sender))
                {
                    _readyPlayers.Add(sender);
                }
            }
            else
            {
                _readyPlayers.Remove(sender);
            }

            UpdatePlayersReady();
        }

        public virtual void UpdatePlayersReady()
        {
            List<byte> buffer = new List<byte>();
            buffer.AddRange(BitConverter.GetBytes(_readyPlayers.Count));
            buffer.AddRange(BitConverter.GetBytes(roomClients.Count));
            BroadcastPacket(new BasePacket(CommandType.PlayerReady, buffer.ToArray()));
            BroadcastWebSocket(CommandType.PlayerReady, new ReadyPlayers(_readyPlayers.Count, roomClients.Count));
        }

        public virtual void DestroyRoom(PlayerInfo sender)
        {
            if (roomHost.Equals(sender))
            {
                RoomsController.DestroyRoom(roomId);
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}:{sender.playerId} tried to destroy the room, but he is not the host");
            }
        }
    }
}
