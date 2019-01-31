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
using Lidgren.Network;
using System.Threading.Tasks;

namespace ServerHub.Rooms
{
    public enum MessagePosition : byte { Top, Bottom };

    public struct ReadyPlayers
    {
        public int readyPlayers, roomClients;

        public ReadyPlayers(int ready, int clients)
        {
            readyPlayers = ready;
            roomClients = clients;
        }
    }

    public struct DisplayMessage
    {
        float displayTime;
        float fontSize;
        string message;
        MessagePosition position;

        public DisplayMessage(float displayTime, float fontSize, string message, MessagePosition position)
        {
            this.displayTime = displayTime;
            this.fontSize = fontSize;
            this.message = message;
            this.position = position;
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

        public RoomSettings roomSettings;
        public RoomState roomState;

        public bool noHost;
        public PlayerInfo roomHost;

        public SongInfo selectedSong;
        public byte selectedDifficulty;

        private DateTime _songStartTime;
        private DateTime _resultsStartTime;

        public const float resultsShowTime = 15f;

        private static Task<SongInfo> randomSongTask;

        public BaseRoom(uint id, RoomSettings settings, PlayerInfo host)
        {
            roomId = id;
            roomSettings = settings;
            roomHost = host;
        }

        public virtual void StartRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed += RoomLoopAsync;
        }

        public virtual void StopRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed -= RoomLoopAsync;
        }

        public virtual RoomInfo GetRoomInfo()
        {
            return new RoomInfo() { roomId = roomId, name = roomSettings.Name, usePassword = roomSettings.UsePassword, players = roomClients.Count, maxPlayers = roomSettings.MaxPlayers, noFail = roomSettings.NoFail, roomHost = roomHost, roomState = roomState, songSelectionType = roomSettings.SelectionType, selectedSong = selectedSong, selectedDifficulty = selectedDifficulty };
        }

        public virtual void PlayerLeft(Client player)
        {
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

        public virtual async void RoomLoopAsync(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if (randomSongTask != null)
                return;

            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
            switch (roomState)
            {
                case RoomState.InGame:
                    {
                        if (DateTime.Now.Subtract(_songStartTime).TotalSeconds >= selectedSong.songDuration)
                        {
                            roomState = RoomState.Results;
                            _resultsStartTime = DateTime.Now;
                            
                            outMsg.Write((byte)CommandType.GetRoomInfo);
                            GetRoomInfo().AddToMessage(outMsg);
                            
                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);

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

                            outMsg.Write((byte)CommandType.SetSelectedSong);
                            outMsg.Write((int)0);

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                            BroadcastWebSocket(CommandType.SetSelectedSong, null);
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

                                    randomSongTask = BeatSaver.GetRandomSong();
                                    selectedSong = await randomSongTask;
                                    randomSongTask = null;

                                    outMsg.Write((byte)CommandType.SetSelectedSong);
                                    selectedSong.AddToMessage(outMsg);

                                    BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                                    
                                    BroadcastWebSocket(CommandType.SetSelectedSong, selectedSong);
                                    ReadyStateChanged(roomHost, true);
                                }
                                break;
                        }
                    }
                    break;
            }
            
            if(outMsg.LengthBytes > 0)
            {
                outMsg = HubListener.ListenerServer.CreateMessage();
            }

            outMsg.Write((byte)CommandType.UpdatePlayerInfo);

            switch (roomState)
            {
                case RoomState.SelectingSong:
                    {
                        outMsg.Write((float)0);
                        outMsg.Write((float)0);
                    }
                    break;
                case RoomState.Preparing:
                    {
                        outMsg.Write((float)0);
                        outMsg.Write((float)0);
                    }
                    break;
                case RoomState.InGame:
                    {
                        outMsg.Write((float)DateTime.Now.Subtract(_songStartTime).TotalSeconds);
                        outMsg.Write(selectedSong.songDuration);
                    }
                    break;
                case RoomState.Results:
                    {
                        outMsg.Write((float)DateTime.Now.Subtract(_resultsStartTime).TotalSeconds);
                        outMsg.Write(resultsShowTime);
                    }
                    break;
            }

            outMsg.Write(roomClients.Count);

            for(int i = 0; i < roomClients.Count; i++)
            {
                if(i < roomClients.Count)
                {
                    if (roomClients[i] != null)
                    {
                        roomClients[i].playerInfo.AddToMessage(outMsg);
                    }
                } 
            }

            BroadcastPacket(outMsg, NetDeliveryMethod.UnreliableSequenced);

            if (roomClients.Count > 0)
                BroadcastWebSocket(CommandType.UpdatePlayerInfo, roomClients.Select(x => x.playerInfo).ToArray());
        }

        public virtual void BroadcastPacket(NetOutgoingMessage msg, NetDeliveryMethod deliveryMethod)
        {
            if (roomClients.Count == 0)
                return;

            try
            {
                HubListener.ListenerServer.SendMessage(msg, roomClients.Select(x => x.playerConnection).ToList(), deliveryMethod, (deliveryMethod == NetDeliveryMethod.UnreliableSequenced ? 1 : 0));
                Program.networkBytesOutNow += msg.LengthBytes * roomClients.Count;
            }
            catch (Exception e)
            {
                Logger.Instance.Warning($"Unable to send packet to players! Exception: {e}");
            }
        }

        public virtual void BroadcastEventMessage(string header, string data, List<Client> excludeClients = null)
        {
            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

            outMsg.Write((byte)CommandType.SendEventMessage);
            outMsg.Write(header);
            outMsg.Write(data);

            for (int i = 0; i < roomClients.Count; i++)
            {
                try
                {
                    if ((excludeClients != null && !excludeClients.Contains(roomClients[i])) || excludeClients == null)
                    {
                        roomClients[i].playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                        Program.networkBytesOutNow += outMsg.LengthBytes;
                    }
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
                service?.Sessions.BroadcastAsync(serialized, null);

                if (roomInfo.roomState == RoomState.Preparing)
                {
                    ReadyPlayers readyPlayers = new ReadyPlayers(_readyPlayers.Count, roomClients.Count);

                    packet = new WebSocketPacket(CommandType.PlayerReady, readyPlayers);
                    serialized = JsonConvert.SerializeObject(packet);
                    service?.Sessions.BroadcastAsync(serialized, null);
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
            service?.Sessions.BroadcastAsync(serialized, null);
        }

        public virtual async void SetSelectedSongAsync(PlayerInfo sender, SongInfo song)
        {
            if (sender.Equals(roomHost))
            {
                selectedSong = song;

                NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

                if (selectedSong == null)
                {
                    switch (roomSettings.SelectionType)
                    {
                        case SongSelectionType.Manual:
                            {

                                roomState = RoomState.SelectingSong;

                                outMsg.Write((byte)CommandType.SetSelectedSong);
                                outMsg.Write((int)0);

                                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                                BroadcastWebSocket(CommandType.SetSelectedSong, null);
                            }
                            break;
                        case SongSelectionType.Random:
                            {
                                roomState = RoomState.Preparing;
                                Random rand = new Random();

                                randomSongTask = BeatSaver.GetRandomSong();
                                selectedSong = await randomSongTask;
                                randomSongTask = null;

                                outMsg.Write((byte)CommandType.SetSelectedSong);
                                selectedSong.AddToMessage(outMsg);

                                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                                BroadcastWebSocket(CommandType.SetSelectedSong, selectedSong);
                                ReadyStateChanged(roomHost, true);
                            }
                            break;
                    }
                }
                else
                {
                    roomState = RoomState.Preparing;

                    outMsg.Write((byte)CommandType.SetSelectedSong);
                    selectedSong.AddToMessage(outMsg);

                    BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                    BroadcastWebSocket(CommandType.SetSelectedSong, selectedSong);
                    ReadyStateChanged(roomHost, true);
                }
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}:{sender.playerId} tried to select song, but he is not the host");
            }
        }

        public virtual void StartLevel(PlayerInfo sender, byte difficulty, SongInfo song)
        {
            if (sender.Equals(roomHost))
            {
                selectedSong = song;
                selectedDifficulty = difficulty;

                SongWithDifficulty songWithDifficulty = new SongWithDifficulty(song, difficulty);
                
                NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

                outMsg.Write((byte)CommandType.StartLevel);
                outMsg.Write(selectedDifficulty);
                selectedSong.AddToMessage(outMsg);
                
                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
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
                
                NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

                outMsg.Write((byte)CommandType.TransferHost);
                roomHost.AddToMessage(outMsg);

                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}:{sender.playerId} tried to transfer host, but he is not the host");
            }
        }

        public virtual void ForceTransferHost(PlayerInfo newHost)
        {
            roomHost = newHost;

            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

            outMsg.Write((byte)CommandType.TransferHost);
            roomHost.AddToMessage(outMsg);

            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
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
            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

            outMsg.Write((byte)CommandType.PlayerReady);
            outMsg.Write(_readyPlayers.Count);
            outMsg.Write(roomClients.Count);

            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
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
