using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using ServerCommons.Misc;

namespace BeatSaberMultiplayerServer
{
    internal class Client
    {
        public string clientIP;
        public TcpClient _client;

        public PlayerInfo playerInfo;
        public int playerScore;
        public ulong playerId;
        public string playerName;

        public CustomSongInfo votedFor = null;
        public ClientState state = ClientState.Disconnected;

        Thread _clientLoopThread;

        public Queue<ServerCommand> sendQueue = new Queue<ServerCommand>();

        public Client(TcpClient client)
        {
            _client = client;

            _clientLoopThread = new Thread(ClientLoop) { IsBackground = true };
            _clientLoopThread.Start();
        }

        void ClientLoop()
        {
            int pingTimer = 0;

            Logger.Instance.Log("Client connected!");

            if(Misc.Settings.Instance.Server.MaxPlayers != 0)
            {
                if(Misc.Settings.Instance.Server.MaxPlayers < ServerMain.clients.Count)
                {
                    KickClient("Too many players");
                    return;
                }
            }

            while (_clientLoopThread.IsAlive)
            {
                try
                {
                    if (_client != null && _client.Connected)
                    {
                        clientIP = ((IPEndPoint)_client.Client.RemoteEndPoint).Address.ToString();

                        pingTimer++;
                        if (pingTimer > 180)
                        {
                            SendToClient(new ServerCommand(ServerCommandType.Ping));
                            pingTimer = 0;
                        }

                        string[] commands = ReceiveFromClient();

                        if (commands != null)
                        {
                            foreach (string data in commands)
                            {
                                ClientCommand command = JsonConvert.DeserializeObject<ClientCommand>(data);

                                Version clientVersion = new Version(command.version);

                                if (clientVersion.Major != ServerMain.serverVersion.Major || clientVersion.Minor != ServerMain.serverVersion.Minor || clientVersion.Build != ServerMain.serverVersion.Build)
                                {
                                    state = ClientState.UpdateRequired;
                                    SendToClient(new ServerCommand(ServerCommandType.UpdateRequired));
                                    return;
                                }

                                if (state != ClientState.Playing && state != ClientState.Connected)
                                {
                                    state = ClientState.Connected;
                                }

                                switch (command.commandType)
                                {
                                    case ClientCommandType.SetPlayerInfo:
                                        {
                                            PlayerInfo receivedPlayerInfo =
                                                JsonConvert.DeserializeObject<PlayerInfo>(command.playerInfo);
                                            if (receivedPlayerInfo != null)
                                            {
                                                if (ServerMain.serverState == ServerState.Playing)
                                                {
                                                    state = ClientState.Playing;
                                                }
                                                if (playerId == 0)
                                                {
                                                    playerId = receivedPlayerInfo.playerId;
                                                    if (!ServerMain.clients.Contains(this))
                                                    {
                                                        ServerMain.clients.Add(this);
                                                        Logger.Instance.Log("New player: " + receivedPlayerInfo.playerName);
                                                    }
                                                }
                                                else if (playerId != receivedPlayerInfo.playerId)
                                                {
                                                    return;
                                                }

                                                if (playerName == null)
                                                {
                                                    playerName = receivedPlayerInfo.playerName;
                                                }
                                                else if (playerName != receivedPlayerInfo.playerName)
                                                {
                                                    return;
                                                }

                                                playerScore = receivedPlayerInfo.playerScore;

                                                playerInfo = receivedPlayerInfo;

                                                if (Misc.Settings.Instance.Access.Blacklist.Contains(receivedPlayerInfo.playerId.ToString()) || Misc.Settings.Instance.Access.Blacklist.Contains(clientIP))
                                                {
                                                    KickClient();
                                                    return;
                                                }

                                                if (Misc.Settings.Instance.Access.WhitelistEnabled && !Misc.Settings.Instance.Access.Whitelist.Contains(receivedPlayerInfo.playerId.ToString()) && !Misc.Settings.Instance.Access.Whitelist.Contains(clientIP))
                                                {
                                                    KickClient();
                                                    return;
                                                }
                                            }
                                        }
                                        ;
                                        break;
                                    case ClientCommandType.GetServerState:
                                        {
                                            if (ServerMain.serverState != ServerState.Playing)
                                            {
                                                SendToClient(new ServerCommand(ServerCommandType.SetServerState));
                                            }
                                            else
                                            {
                                                SendToClient(new ServerCommand(
                                                    ServerCommandType.SetServerState,
                                                    _selectedSongDuration: ServerMain.availableSongs[ServerMain.currentSongIndex].duration.TotalSeconds,
                                                    _selectedSongPlayTime: ServerMain.playTime.TotalSeconds));
                                            }
                                        }
                                        ;
                                        break;
                                    case ClientCommandType.GetAvailableSongs:
                                        {
                                            SendToClient(new ServerCommand(
                                                ServerCommandType.DownloadSongs,
                                                _songs: ServerMain.availableSongs.Select(x => x.levelId).ToArray()));
                                        };
                                        break;
                                    case ClientCommandType.VoteForSong:
                                        {
                                            if(ServerMain.serverState == ServerState.Voting)
                                                votedFor = ServerMain.availableSongs.FirstOrDefault(x => x.levelId.Substring(0, 32) == command.voteForLevelId.Substring(0, 32));
                                        };
                                        break;
                                }
                            }
                        }
                        while (sendQueue.Count != 0)
                        {
                            SendToClient(sendQueue.Dequeue());
                        }

                    }
                    else
                    {
                        if ((ServerMain.serverState == ServerState.Playing && ServerMain.playTime.TotalSeconds <= ServerMain.availableSongs[ServerMain.currentSongIndex].duration.TotalSeconds - 10f) || ServerMain.serverState != ServerState.Playing)
                        {
                            ServerMain.clients.Remove(this);
                            if (_client != null)
                            {
                                _client.Close();
                                _client = null;
                            }
                            state = ClientState.Disconnected;
                            Logger.Instance.Log("Client disconnected!");
                            return;
                        }
                    }
                }catch(Exception e)
                {
                    Logger.Instance.Warning($"CLIENT EXCEPTION: {e}");
                }

                Thread.Sleep(8);
            }
        }

        public void KickClient()
        {
            SendToClient(new ServerCommand(ServerCommandType.Kicked));
            DestroyClient();
            Logger.Instance.Log($"Kicked player \"{playerName}\" : {playerId}");
        }

        public void KickClient(string reason)
        {
            SendToClient(new ServerCommand(ServerCommandType.Kicked, _kickReason: reason));
            DestroyClient();
            Logger.Instance.Log($"Kicked player \"{playerName}\" : {playerId} with reason \"{reason}\"");
        }

        public string[] ReceiveFromClient(bool waitIfNoData = false)
        {
            if (_client.Available == 0)
            {
                if (waitIfNoData)
                {
                    try {
                        while (_client.Available == 0 && _client.Connected) {
                            Thread.Sleep(8);
                        }
                    }
                    catch (SocketException ex) {
                        Logger.Instance.Exception(ex.Message);
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }


            if (_client == null || !_client.Connected)
            {
                return null;
            }

            NetworkStream stream = _client.GetStream();

            string receivedJson;
            byte[] buffer = new byte[_client.ReceiveBufferSize];
            int length;

            length = stream.Read(buffer, 0, buffer.Length);

            receivedJson = Encoding.UTF8.GetString(buffer).Trim('\0');
            
            while (!receivedJson.EndsWith("}"))
            {
                Logger.Instance.Log("Received message is splitted, waiting for another part...");
                receivedJson += string.Join("", ReceiveFromClient(true));
            }

            string[] strBuffer = receivedJson.Replace("}{", "}#{").Split('#');

            return strBuffer;
        }

        public bool SendToClient(ServerCommand command)
        {
            if (_client == null || !_client.Connected)
            {
                return false;
            }

            string message = JsonConvert.SerializeObject(command, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore });
            
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            try
            {
                _client.GetStream().Write(buffer, 0, buffer.Length);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }


        public void DestroyClient()
        {
            if (_client != null)
            {
                ServerMain.clients.Remove(this);
                Thread.Sleep(150);
                _client.Close();
            }
        }


    }
}