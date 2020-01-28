using ServerHub.Hub;
using ServerHub.Data;
using ServerHub.Misc;
using Logger = ServerHub.Misc.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Lidgren.Network;

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
        public LevelOptionsInfo startLevelInfo;

        public bool requestedRandomSong;

        public  DateTime roomStartedDateTime;

        public bool spectatorInRoom;

        private DateTime _songStartTime;
        private DateTime _resultsStartTime;

        public BaseRoom(uint id, RoomSettings settings, PlayerInfo host)
        {
            roomId = id;
            roomSettings = settings;
            roomHost = host;
        }

        public virtual void StartRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed += RoomLoop;
            HighResolutionTimer.VoIPTimer.Elapsed += BroadcastVoIPData;
            roomStartedDateTime = DateTime.Now;
        }

        public virtual void StopRoom()
        {
            HighResolutionTimer.LoopTimer.Elapsed -= RoomLoop;
            HighResolutionTimer.VoIPTimer.Elapsed -= BroadcastVoIPData;
        }

        public virtual RoomInfo GetRoomInfo()
        {
            return new RoomInfo() { roomId = roomId, name = roomSettings.Name, usePassword = roomSettings.UsePassword, noHost = noHost, players = roomClients.Count, maxPlayers = roomSettings.MaxPlayers, roomHost = roomHost, roomState = roomState, songSelectionType = roomSettings.SelectionType, selectedSong = selectedSong, startLevelInfo = startLevelInfo, perPlayerDifficulty = roomSettings.PerPlayerDifficulty };
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

        public virtual void RoomLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
            switch (roomState)
            {
                case RoomState.InGame:
                    {
                        if ((DateTime.Now.Subtract(_songStartTime).TotalSeconds >= selectedSong.songDuration) ||
                            (DateTime.Now.Subtract(_songStartTime).TotalSeconds >= 10f && roomClients.All(x => x.playerInfo.updateInfo.playerState != PlayerState.Game)))
                        {
                            roomState = RoomState.Results;
                            _resultsStartTime = DateTime.Now;
                            
                            outMsg.Write((byte)CommandType.GetRoomInfo);
                            GetRoomInfo().AddToMessage(outMsg);
                            
                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);

                            if (roomClients.Count > 0)
                                BroadcastWebSocket(CommandType.GetRoomInfo, GetRoomInfo());
                        }
                    }
                    break;
                case RoomState.Results:
                    {
                        if (DateTime.Now.Subtract(_resultsStartTime).TotalSeconds >= roomSettings.ResultsShowTime)
                        {
                            roomState = RoomState.SelectingSong;
                            selectedSong = null;

                            outMsg.Write((byte)CommandType.SetSelectedSong);
                            outMsg.Write((int)0);

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
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
                                    if (!requestedRandomSong && roomClients.Count > 0 && roomClients.Any(x => roomHost.Equals(x.playerInfo)))
                                    {
                                        requestedRandomSong = true;

                                        outMsg.Write((byte)CommandType.GetRandomSongInfo);

                                        roomClients.First(x => roomHost.Equals(x.playerInfo)).playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);

                                        BroadcastWebSocket(CommandType.GetRandomSongInfo, null);
                                    }
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
                        outMsg.Write(roomSettings.ResultsShowTime);
                    }
                    break;
            }


            bool fullUpdate = false;

            int i = 0;

            for (i = 0; i < roomClients.Count; i++)
                if(i < roomClients.Count)
                    if (roomClients[i] != null)
                    {
                        if (roomClients[i].playerInfo.Equals(roomHost) && roomClients[i].playerInfo.updateInfo.playerNameColor.Equals(Color32.defaultColor))
                            roomClients[i].playerInfo.updateInfo.playerNameColor = Color32.roomHostColor;
                        fullUpdate |= roomClients[i].lastUpdateIsFull;
                        roomClients[i].lastUpdateIsFull = false;
                    }

            outMsg.Write(fullUpdate ? (byte)1 : (byte)0);

            outMsg.Write(roomClients.Count);

            if (fullUpdate)
                for (i = 0; i < roomClients.Count; i++)
                {
                    if (i < roomClients.Count)
                        if (roomClients[i] != null)
                            outMsg.Write(roomClients[i].playerInfo.playerId);
                            roomClients[i].playerInfo.AddToMessage(outMsg);
                }
            else
                for (i = 0; i < roomClients.Count; i++)
                    if (i < roomClients.Count)
                        if (roomClients[i] != null)
                        {
                            outMsg.Write(roomClients[i].playerInfo.playerId);
                            roomClients[i].playerInfo.updateInfo.AddToMessage(outMsg);
                            outMsg.Write((byte)roomClients[i].playerInfo.hitsLastUpdate.Count);
                            foreach (var hit in roomClients[i].playerInfo.hitsLastUpdate)
                                hit.AddToMessage(outMsg);
                            roomClients[i].playerInfo.hitsLastUpdate.Clear();
                        }

            if (roomClients.Count > 0)
            {
                BroadcastPacket(outMsg, NetDeliveryMethod.UnreliableSequenced, 1);

                BroadcastWebSocket(CommandType.UpdatePlayerInfo, roomClients.Select(x => x.playerInfo).ToArray());

                spectatorInRoom = roomClients.Any(x => x.playerInfo.updateInfo.playerState == PlayerState.Spectating);
                if (spectatorInRoom)
                {
                    outMsg = HubListener.ListenerServer.CreateMessage();

                    outMsg.Write((byte)CommandType.GetPlayerUpdates);

                    outMsg.Write(roomClients.Count(x => x.playerInfo.updateInfo.playerState == PlayerState.Game && x.playerUpdateHistory.Count > 0));

                    for (i = 0; i < roomClients.Count; i++)
                    {
                        if (i < roomClients.Count)
                        {
                            if (roomClients[i] != null && roomClients[i].playerInfo.updateInfo.playerState == PlayerState.Game && roomClients[i].playerUpdateHistory.Count > 0)
                            {
                                outMsg.Write(roomClients[i].playerInfo.playerId);

                                int historyLength = roomClients[i].playerUpdateHistory.Count;
                                outMsg.Write((byte)historyLength);

                                for (int j = 0; j < historyLength; j++)
                                {
                                    roomClients[i].playerUpdateHistory.Dequeue().AddToMessage(outMsg);
                                }
                                roomClients[i].playerUpdateHistory.Clear();

                                int hitCount = roomClients[i].playerHitsHistory.Count;
                                outMsg.Write((byte)hitCount);

                                for (int j = 0; j < hitCount; j++)
                                {
                                    roomClients[i].playerHitsHistory[i].AddToMessage(outMsg);
                                }
                                roomClients[i].playerHitsHistory.Clear();
                            }
                        }
                    }

                    BroadcastPacket(outMsg, NetDeliveryMethod.UnreliableSequenced, 2, roomClients.Where(x => x.playerInfo.updateInfo.playerState != PlayerState.Spectating).ToList());
                }
            }
        }

        public virtual void BroadcastVoIPData(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if (!Settings.Instance.Server.AllowVoiceChat)
                return;

            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

            outMsg.Write((byte)CommandType.UpdateVoIPData);
            outMsg.Write(roomClients.Count(x => x != null && x.playerVoIPQueue != null && x.playerVoIPQueue.Count > 0));
            
            for(int i = 0; i < roomClients.Count; i++)
            {
                if (roomClients.Count > i && roomClients[i] != null && roomClients[i].playerVoIPQueue != null && roomClients[i].playerVoIPQueue.Count > 0)
                {
                    while (roomClients[i].playerVoIPQueue.Count > 0)
                    {
                        roomClients[i].playerVoIPQueue.Dequeue().AddToMessage(outMsg);
                    }
                }
            }

            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableSequenced, 2);
        }

        public virtual void BroadcastPacket(NetOutgoingMessage msg, NetDeliveryMethod deliveryMethod, int seqChannel, List<Client> excludeClients = null)
        {
            if (roomClients.Count == 0 || roomClients.Count(x => excludeClients == null || !excludeClients.Contains(x)) == 0)
                return;

            try
            {
                HubListener.ListenerServer.SendMessage(msg, roomClients.Where(x => excludeClients == null || !excludeClients.Contains(x)).Select(x => x.playerConnection).ToList(), deliveryMethod, seqChannel);
                Program.networkBytesOutNow += msg.LengthBytes * roomClients.Where(x => excludeClients == null || !excludeClients.Contains(x)).Count();
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

            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0, excludeClients);
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

        public virtual void SetSelectedSong(PlayerInfo sender, SongInfo song)
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

                                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                                BroadcastWebSocket(CommandType.SetSelectedSong, null);
                            }
                            break;
                        case SongSelectionType.Random:
                            {
                                if (!requestedRandomSong && roomClients.Count > 0 && roomClients.Any(x => roomHost.Equals(x.playerInfo)))
                                {
                                    requestedRandomSong = true;

                                    outMsg.Write((byte)CommandType.GetRandomSongInfo);

                                    roomClients.First(x => roomHost.Equals(x.playerInfo)).playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);

                                    BroadcastWebSocket(CommandType.GetRandomSongInfo, null);
                                }
                            }
                            break;
                    }
                }
                else
                {
                    requestedRandomSong = false;

                    roomState = RoomState.Preparing;

                    outMsg.Write((byte)CommandType.SetSelectedSong);
                    selectedSong.AddToMessage(outMsg);

                    BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                    BroadcastWebSocket(CommandType.SetSelectedSong, selectedSong);
                    ReadyStateChanged(roomHost, true);
                }
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}({sender.playerId}) tried to select song, but he is not the host");
            }
        }

        public virtual void SetLevelOptions(PlayerInfo sender, LevelOptionsInfo info)
        {
            if (sender.Equals(roomHost))
            {
                startLevelInfo = info;

                NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
                
                outMsg.Write((byte)CommandType.SetLevelOptions);
                startLevelInfo.AddToMessage(outMsg);

                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0, new List<Client>() { roomClients.First(x => x.playerInfo.Equals(sender)) });
                BroadcastWebSocket(CommandType.SetLevelOptions, startLevelInfo);
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}({sender.playerId}) tried to change level options, but he is not the host");
            }
        }

        public virtual void StartLevel(PlayerInfo sender, LevelOptionsInfo options, SongInfo song)
        {
            if (sender.Equals(roomHost))
            {
                selectedSong = song;
                startLevelInfo = options;

                SongWithOptions songWithDifficulty = new SongWithOptions(selectedSong, startLevelInfo);
                
                NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

                outMsg.Write((byte)CommandType.StartLevel);
                startLevelInfo.AddToMessage(outMsg);
                selectedSong.AddToMessage(outMsg);
                
                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                BroadcastWebSocket(CommandType.StartLevel, songWithDifficulty);

                roomState = RoomState.InGame;
                _songStartTime = DateTime.Now;
                _readyPlayers.Clear();
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}({sender.playerId}) tried to start the level, but he is not the host");
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

                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
            else
            {
                Logger.Instance.Warning($"{sender.playerName}({sender.playerId}) tried to transfer host, but he is not the host");
            }
        }

        public virtual void ForceTransferHost(PlayerInfo newHost)
        {
            roomHost = newHost;

            if (roomClients.Count > 0)
            {
                NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();

                outMsg.Write((byte)CommandType.TransferHost);
                roomHost.AddToMessage(outMsg);

                BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
            }
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

            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
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
                Logger.Instance.Warning($"{sender.playerName}({sender.playerId}) tried to destroy the room, but he is not the host");
            }
        }
    }
}
