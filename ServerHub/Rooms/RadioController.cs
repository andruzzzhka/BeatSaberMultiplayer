using Lidgren.Network;
using Newtonsoft.Json;
using ServerHub.Data;
using ServerHub.Hub;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ServerHub.Rooms
{
    public static class RadioController
    {
        public static bool radioStarted;

        public static List<Client> radioClients = new List<Client>();

        public static ChannelInfo channelInfo;

        public static Queue<SongInfo> radioQueue = new Queue<SongInfo>();

        public static Dictionary<Client, float> songDurationResponses = new Dictionary<Client, float>();
        public static bool requestingSongDuration;

        private static DateTime _songStartTime;
        private static DateTime _resultsStartTime;
        private static DateTime _nextSongScreenStartTime;

        private static Task<SongInfo> randomSongTask;

        public const float resultsShowTime = 15f;
        public const float nextSongShowTime = 90f;

        public static async Task StartRadioAsync()
        {
            if (!radioStarted)
            {
                channelInfo = new ChannelInfo() { name = Settings.Instance.Radio.ChannelName, currentSong = null, preferredDifficulty = Settings.Instance.Radio.PreferredDifficulty, playerCount = 0, iconUrl = Settings.Instance.Radio.ChannelIconUrl, state = ChannelState.NextSong, ip = "", port = 0 };

                if (File.Exists("RadioQueue.json"))
                {
                    try
                    {
                        Queue<SongInfo> queue = JsonConvert.DeserializeObject<Queue<SongInfo>>(File.ReadAllText("RadioQueue.json"));
                        if (queue != null)
                            radioQueue = queue;
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Warning("Unable to load radio queue! Exception: " + e);
                    }
                }

                if (radioQueue.Count > 0)
                {
                    channelInfo.currentSong = radioQueue.Dequeue();
                    try
                    {
                        File.WriteAllText("RadioQueue.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
                    }
                    catch
                    {

                    }
                }
                else
                {
                    channelInfo.currentSong = await BeatSaver.GetRandomSong();
                }

                HighResolutionTimer.LoopTimer.Elapsed += RadioLoop;

                radioStarted = true;
            }
        }

        public static void StopRadio(string reason)
        {
            if (radioStarted)
            {
                HighResolutionTimer.LoopTimer.Elapsed -= RadioLoop;

                for (int i = 0; i < radioClients.Count; i++)
                {
                    if (radioClients.Count > i && radioClients[i] != null)
                    {
                        NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
                        outMsg.Write((byte)CommandType.Disconnect);
                        outMsg.Write(reason);
                        radioClients[i].playerConnection.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered, 0);
                        Program.networkBytesOutNow += outMsg.LengthBytes;
                    }
                }
                radioStarted = false;
                File.WriteAllText("RadioQueue.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
            }
        }

        public static async void AddSongToQueueByKey(string songKey)
        {
            SongInfo info = await BeatSaver.InfoFromID(songKey);
            if (info != null)
            {
                radioQueue.Enqueue(info);
                Logger.Instance.Log("Successfully added songs to the queue!");
                File.WriteAllText("RadioQueue.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
            }
        }

        public static async void AddSongToQueueByHash(string hash)
        {
            SongInfo info = await BeatSaver.InfoFromHash(hash);
            if (info != null)
            {
                radioQueue.Enqueue(info);
                Logger.Instance.Log("Successfully added songs to the queue!");
                File.WriteAllText("RadioQueue.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
            }
        }

        public static async void AddPlaylistToQueue(string path)
        {
            bool remotePlaylist = path.ToLower().Contains("http://") || path.ToLower().Contains("https://");
            Playlist playlist = null;
            if (remotePlaylist)
            {
                using (WebClient w = new WebClient())
                {
                    try
                    {
                        string response = await w.DownloadStringTaskAsync(path);
                        playlist = JsonConvert.DeserializeObject<Playlist>(response);
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error("Unable to add playlist to queue! Exception: "+e);
                        return;
                    }
                }
            }
            else
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        string content = await File.ReadAllTextAsync(fullPath);
                        playlist = JsonConvert.DeserializeObject<Playlist>(content);
                    }
                    else
                    {
                        Logger.Instance.Error("Unable to add playlist to queue! File does not exist!");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.Instance.Error("Unable to add playlist to queue! Exception: " + e);
                    return;
                }
            }

            try
            {
                foreach(PlaylistSong song in playlist.songs)
                {
                    if (!string.IsNullOrEmpty(song.hash))
                    {
                        if (song.levelId.Length >= 32)
                        {
                            radioQueue.Enqueue(new SongInfo() { levelId = song.hash.ToUpper().Substring(0, 32), songName = song.songName, key = song.key });
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(song.levelId))
                    {
                        if (song.levelId.Length >= 32)
                        {
                            radioQueue.Enqueue(new SongInfo() { levelId = song.levelId.ToUpper().Substring(0, 32), songName = song.songName, key = song.key });
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(song.key))
                    {
                        SongInfo info = await BeatSaver.InfoFromID(song.key);
                        if(info != null)
                            radioQueue.Enqueue(info);
                        continue;
                    }
                }
                Logger.Instance.Log("Successfully added all songs from playlist to the queue!");
                File.WriteAllText("RadioQueue.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Instance.Error("Unable to add playlist to queue! Exception: " + e);
                return;
            }
        }

        public static bool ClientJoinedChannel(Client client)
        {
            if (Settings.Instance.Radio.EnableRadioChannel && radioStarted)
            {
                radioClients.Add(client);
                if (radioClients.Count == 1 && channelInfo.state == ChannelState.NextSong)
                    _nextSongScreenStartTime = DateTime.Now;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void ClientLeftChannel(Client client)
        {
            if(radioClients.Contains(client))
                radioClients.Remove(client);
        }

        public static async void RadioLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if (randomSongTask != null)
                return;

            channelInfo.playerCount = radioClients.Count;
            channelInfo.name = Settings.Instance.Radio.ChannelName;
            channelInfo.iconUrl = Settings.Instance.Radio.ChannelIconUrl;
            channelInfo.preferredDifficulty = Settings.Instance.Radio.PreferredDifficulty;

            if (radioClients.Count == 0)
            {
                channelInfo.state = ChannelState.NextSong;
                return;
            }

            NetOutgoingMessage outMsg = HubListener.ListenerServer.CreateMessage();
            switch (channelInfo.state)
            {
                case ChannelState.InGame:  //--> Results
                    {
                        if (DateTime.Now.Subtract(_songStartTime).TotalSeconds >= channelInfo.currentSong.songDuration)
                        {
                            channelInfo.state = ChannelState.Results;
                            _resultsStartTime = DateTime.Now;

                            outMsg.Write((byte)CommandType.GetChannelInfo);

                            outMsg.Write((byte)0);
                            channelInfo.AddToMessage(outMsg);

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                            Logger.Instance.Log("Radio: Going to Results");

                        }
                    }
                    break;
                case ChannelState.Results: //--> NextSong
                    {
                        if (DateTime.Now.Subtract(_resultsStartTime).TotalSeconds >= resultsShowTime)
                        {
                            channelInfo.state = ChannelState.NextSong;

                            if (radioQueue.Count > 0)
                            {
                                channelInfo.currentSong = radioQueue.Dequeue();
                                try
                                {
                                    File.WriteAllText("RadioQueue.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
                                }
                                catch
                                {

                                }
                            }
                            else
                            {
                                randomSongTask = BeatSaver.GetRandomSong();
                                channelInfo.currentSong = await randomSongTask;
                                randomSongTask = null;
                            }

                            outMsg.Write((byte)CommandType.SetSelectedSong);
                            channelInfo.currentSong.AddToMessage(outMsg);

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                            _nextSongScreenStartTime = DateTime.Now;
                            Logger.Instance.Log("Radio: Going to NextSongs");
                        }
                    }
                    break;
                case ChannelState.NextSong:  //--> InGame
                    {
                        if (DateTime.Now.Subtract(_nextSongScreenStartTime).TotalSeconds >= nextSongShowTime)
                        {
                            channelInfo.state = ChannelState.InGame;

                            outMsg.Write((byte)CommandType.StartLevel);
                            outMsg.Write((byte)Settings.Instance.Radio.PreferredDifficulty);

                            channelInfo.currentSong.songDuration = Misc.Math.Median(songDurationResponses.Values.ToArray());
                            songDurationResponses.Clear();
                            requestingSongDuration = false;

                            channelInfo.currentSong.AddToMessage(outMsg);

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                            _songStartTime = DateTime.Now;
                            Logger.Instance.Log("Radio: Going to InGame");
                        }
                        else if (DateTime.Now.Subtract(_nextSongScreenStartTime).TotalSeconds >= nextSongShowTime*0.75 && !requestingSongDuration)
                        {

                            outMsg.Write((byte)CommandType.GetSongDuration);
                            channelInfo.currentSong.AddToMessage(outMsg);
                            songDurationResponses.Clear();
                            requestingSongDuration = true;

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                            Logger.Instance.Log("Radio: Requested song duration");
                        }

                    }
                    break;
            }

            if (outMsg.LengthBytes > 0)
            {
                outMsg = HubListener.ListenerServer.CreateMessage();
            }

            outMsg.Write((byte)CommandType.UpdatePlayerInfo);

            switch (channelInfo.state)
            {
                case ChannelState.NextSong:
                    {
                        outMsg.Write((float)DateTime.Now.Subtract(_nextSongScreenStartTime).TotalSeconds);
                        outMsg.Write(nextSongShowTime);
                    }
                    break;
                case ChannelState.InGame:
                    {
                        outMsg.Write((float)DateTime.Now.Subtract(_songStartTime).TotalSeconds);
                        outMsg.Write(channelInfo.currentSong.songDuration);
                    }
                    break;
                case ChannelState.Results:
                    {
                        outMsg.Write((float)DateTime.Now.Subtract(_resultsStartTime).TotalSeconds);
                        outMsg.Write(resultsShowTime);
                    }
                    break;
            }

            outMsg.Write(radioClients.Count);

            radioClients.ForEach(x => x.playerInfo.AddToMessage(outMsg));

            BroadcastPacket(outMsg, NetDeliveryMethod.UnreliableSequenced);
        }

        public static void BroadcastPacket(NetOutgoingMessage msg, NetDeliveryMethod deliveryMethod)
        {
            for (int i = 0; i < radioClients.Count; i++)
            {
                try
                {
                    radioClients[i].playerConnection.SendMessage(msg, deliveryMethod, (deliveryMethod == NetDeliveryMethod.UnreliableSequenced ? 1 : 0));
                    Program.networkBytesOutNow += msg.LengthBytes;
                }
                catch (Exception e)
                {
                    Logger.Instance.Warning($"Unable to send packet to {radioClients[i].playerInfo.playerName}! Exception: {e}");
                }
            }
        }
    }
}
