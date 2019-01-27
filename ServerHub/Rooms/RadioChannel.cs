using Lidgren.Network;
using Newtonsoft.Json;
using ServerHub.Data;
using ServerHub.Hub;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerHub.Rooms
{
    public class RadioChannel
    {
        public int channelId;

        public List<Client> radioClients = new List<Client>();

        public ChannelInfo channelInfo;

        public Queue<SongInfo> radioQueue = new Queue<SongInfo>();

        public Dictionary<Client, float> songDurationResponses = new Dictionary<Client, float>();
        public bool requestingSongDuration;

        public DateTime songStartTime;
        public DateTime resultsStartTime;
        public DateTime nextSongScreenStartTime;

        private Task<SongInfo> randomSongTask;

        public async void StartChannel(int newChannelId)
        {
            channelId = newChannelId;

            channelInfo = new ChannelInfo() { channelId = channelId, name = Settings.Instance.Radio.RadioChannels[channelId].ChannelName, currentSong = null, preferredDifficulty = Settings.Instance.Radio.RadioChannels[channelId].PreferredDifficulty, playerCount = 0, iconUrl = Settings.Instance.Radio.RadioChannels[channelId].ChannelIconUrl, state = ChannelState.NextSong, ip = "", port = 0 };

            if (File.Exists($"RadioQueue{channelId}.json"))
            {
                try
                {
                    Queue<SongInfo> queue = JsonConvert.DeserializeObject<Queue<SongInfo>>(File.ReadAllText($"RadioQueue{channelId}.json"));
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
                    File.WriteAllText($"RadioQueue{channelId}.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
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
        }

        public void StopChannel(string reason = "")
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
            File.WriteAllText($"RadioQueue{channelId}.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
        }

        public async void RadioLoop(object sender, HighResolutionTimerElapsedEventArgs e)
        {
            if (randomSongTask != null)
                return;

            channelInfo.playerCount = radioClients.Count;
            channelInfo.name = Settings.Instance.Radio.RadioChannels[channelId].ChannelName;
            channelInfo.iconUrl = Settings.Instance.Radio.RadioChannels[channelId].ChannelIconUrl;
            channelInfo.preferredDifficulty = Settings.Instance.Radio.RadioChannels[channelId].PreferredDifficulty;

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
                        if (DateTime.Now.Subtract(songStartTime).TotalSeconds >= channelInfo.currentSong.songDuration)
                        {
                            channelInfo.state = ChannelState.Results;
                            resultsStartTime = DateTime.Now;

                            outMsg.Write((byte)CommandType.GetChannelInfo);

                            outMsg.Write((byte)0);
                            channelInfo.AddToMessage(outMsg);

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                        }
                    }
                    break;
                case ChannelState.Results: //--> NextSong
                    {
                        if (DateTime.Now.Subtract(resultsStartTime).TotalSeconds >= Settings.Instance.Radio.ResultsShowTime)
                        {
                            channelInfo.state = ChannelState.NextSong;

                            if (radioQueue.Count > 0)
                            {
                                channelInfo.currentSong = radioQueue.Dequeue();
                                try
                                {
                                    File.WriteAllText($"RadioQueue{channelId}.json", JsonConvert.SerializeObject(radioQueue, Formatting.Indented));
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
                            nextSongScreenStartTime = DateTime.Now;
                        }
                    }
                    break;
                case ChannelState.NextSong:  //--> InGame
                    {
                        if (DateTime.Now.Subtract(nextSongScreenStartTime).TotalSeconds >= Settings.Instance.Radio.NextSongPrepareTime)
                        {
                            channelInfo.state = ChannelState.InGame;

                            outMsg.Write((byte)CommandType.StartLevel);
                            outMsg.Write((byte)Settings.Instance.Radio.RadioChannels[channelId].PreferredDifficulty);

                            channelInfo.currentSong.songDuration = Misc.Math.Median(songDurationResponses.Values.ToArray());
                            songDurationResponses.Clear();
                            requestingSongDuration = false;

                            channelInfo.currentSong.AddToMessage(outMsg);

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
                            songStartTime = DateTime.Now;
                        }
                        else if (DateTime.Now.Subtract(nextSongScreenStartTime).TotalSeconds >= Settings.Instance.Radio.NextSongPrepareTime * 0.75 && !requestingSongDuration)
                        {

                            outMsg.Write((byte)CommandType.GetSongDuration);
                            channelInfo.currentSong.AddToMessage(outMsg);
                            songDurationResponses.Clear();
                            requestingSongDuration = true;

                            BroadcastPacket(outMsg, NetDeliveryMethod.ReliableOrdered);
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
                        outMsg.Write((float)DateTime.Now.Subtract(nextSongScreenStartTime).TotalSeconds);
                        outMsg.Write(Settings.Instance.Radio.NextSongPrepareTime);
                    }
                    break;
                case ChannelState.InGame:
                    {
                        outMsg.Write((float)DateTime.Now.Subtract(songStartTime).TotalSeconds);
                        outMsg.Write(channelInfo.currentSong.songDuration);
                    }
                    break;
                case ChannelState.Results:
                    {
                        outMsg.Write((float)DateTime.Now.Subtract(resultsStartTime).TotalSeconds);
                        outMsg.Write(Settings.Instance.Radio.ResultsShowTime);
                    }
                    break;
            }

            outMsg.Write(radioClients.Count);

            radioClients.ForEach(x => x.playerInfo.AddToMessage(outMsg));

            BroadcastPacket(outMsg, NetDeliveryMethod.UnreliableSequenced);
        }

        public void BroadcastPacket(NetOutgoingMessage msg, NetDeliveryMethod deliveryMethod)
        {
            if (radioClients.Count == 0)
                return;

            try
            {
                HubListener.ListenerServer.SendMessage(msg, radioClients.Select(x => x.playerConnection).ToList(), deliveryMethod, (deliveryMethod == NetDeliveryMethod.UnreliableSequenced ? 1 : 0));
                Program.networkBytesOutNow += msg.LengthBytes * radioClients.Count;
            }
            catch (Exception e)
            {
                Logger.Instance.Warning($"Unable to send packet to players! Exception: {e}");
            }
        }
    }
}
