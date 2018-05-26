using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using ServerCommons.Misc;

namespace BeatSaberMultiplayerServer {
    internal class Client {
        TcpClient _client;
        public PlayerInfo playerInfo;

        int playerScore;
        string playerId;
        string playerName;

        Thread _clientLoopThread;

        public Client(TcpClient client) {
            _client = client;

            _clientLoopThread = new Thread(ClientLoop);
            _clientLoopThread.Start();
        }

        void ClientLoop() {
            int pingTimer = 0;

            Logger.Instance.Log("Client connected!");

            while (true) {
                if (_client != null && _client.Connected) {
                    pingTimer++;
                    if (pingTimer > 180) {
                        SendToClient(JsonConvert.SerializeObject(new ServerCommand(ServerCommandType.Ping)));
                        pingTimer = 0;
                    }

                    string[] commands = ReceiveFromClient(true);

                    if (commands != null) {
                        foreach (string data in commands) {
                            ClientCommand command = JsonConvert.DeserializeObject<ClientCommand>(data);
                            switch (command.commandType) {
                                case ClientCommandType.SetPlayerInfo: {
                                    PlayerInfo receivedPlayerInfo =
                                        JsonConvert.DeserializeObject<PlayerInfo>(command.playerInfo);

                                    if (receivedPlayerInfo != null) {
                                        if (playerId == null) {
                                            playerId = receivedPlayerInfo.playerId;
                                            if (!ServerMain.clients.Contains(this)) {
                                                ServerMain.clients.Add(this);
                                                Logger.Instance.Log("New player: " + receivedPlayerInfo.playerName);
                                            }
                                        }
                                        else if (playerId != receivedPlayerInfo.playerId) {
                                            return;
                                        }

                                        playerInfo = receivedPlayerInfo;

                                        if (playerName == null) {
                                            playerName = receivedPlayerInfo.playerName;
                                        }
                                        else if (playerName != receivedPlayerInfo.playerName) {
                                            return;
                                        }

                                        playerScore = receivedPlayerInfo.playerScore;
                                    }
                                }
                                    ;
                                    break;
                                case ClientCommandType.GetServerState: {
                                    if (ServerMain.serverState != ServerState.Playing) {
                                        SendToClient(
                                            JsonConvert.SerializeObject(
                                                new ServerCommand(ServerCommandType.SetServerState)));
                                    }
                                    else {
                                        Logger.Instance.Log(ServerMain.playTime);
                                        SendToClient(JsonConvert.SerializeObject(new ServerCommand(
                                            ServerCommandType.SetServerState,
                                            _selectedSongDuration: ServerMain
                                                .availableSongs[ServerMain.currentSongIndex].duration.TotalSeconds,
                                            _selectedSongPlayTime: ServerMain.playTime.TotalSeconds)));
                                    }
                                }
                                    ;
                                    break;
                                case ClientCommandType.GetAvailableSongs: {
                                    SendToClient(JsonConvert.SerializeObject(new ServerCommand(
                                        ServerCommandType.DownloadSongs,
                                        _songs: ServerMain.availableSongs.Select(x => x.levelId).ToArray())));
                                }
                                    ;
                                    break;
                            }
                        }
                    }
                }
                else {
                    ServerMain.clients.Remove(this);
                    if (_client != null) {
                        _client.Close();
                        _client = null;
                    }

                    Logger.Instance.Log("Client disconnected!");
                    return;
                }

                Thread.Sleep(16);
            }
        }

        public string[] ReceiveFromClient(bool waitIfNoData = true) {
            if (_client.Available == 0) {
                if (waitIfNoData) {
                    while (_client.Available == 0 && _client.Connected) {
                        Thread.Sleep(16);
                    }
                }
                else {
                    return null;
                }
            }


            if (_client == null || !_client.Connected) {
                return null;
            }

            NetworkStream stream = _client.GetStream();

            string receivedJson;
            byte[] buffer = new byte[_client.ReceiveBufferSize];
            int length;

            length = stream.Read(buffer, 0, buffer.Length);

            receivedJson = Encoding.Unicode.GetString(buffer).Trim('\0');

            string[] strBuffer = receivedJson.Trim('\0').Replace("}{", "}#{").Split('#');

            //Console.WriteLine("Received from client: " + receivedJson);

            return strBuffer;
        }

        public bool SendToClient(string message) {
            if (_client == null || !_client.Connected) {
                return false;
            }

            //Console.WriteLine("Sending to client: "+message);

            byte[] buffer = Encoding.Unicode.GetBytes(message);
            try {
                _client.GetStream().Write(buffer, 0, buffer.Length);
            }
            catch (Exception e) {
                return false;
            }

            return true;
        }


        void DestroyClient() {
            if (_client != null) {
                ServerMain.clients.Remove(this);
                _client.Close();
            }
        }
        
        
    }
}