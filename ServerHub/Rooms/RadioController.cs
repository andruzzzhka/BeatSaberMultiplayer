using Newtonsoft.Json;
using ServerHub.Data;
using ServerHub.Hub;
using ServerHub.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace ServerHub.Rooms
{
    public static class RadioController
    {
        public static bool radioStarted;

        public static List<RadioChannel> radioChannels = new List<RadioChannel>();

        public static void StartRadio()
        {
            if (!radioStarted)
            {
                radioChannels.Clear();
                for(int i = 0; i < Settings.Instance.Radio.RadioChannels.Count; i++)
                {
                    RadioChannel channel = new RadioChannel();
                    radioChannels.Add(channel);

                    channel.StartChannel(i);
                    WebSocketListener.AddChannel(channel);
                }
                radioStarted = true;
            }
        }

        public static void StopRadio(string reason)
        {
            if (radioStarted)
            {
                for (int i = 0; i < radioChannels.Count; i++)
                {
                    if(i < radioChannels.Count && radioChannels[i] != null)
                        radioChannels[i].StopChannel(reason);
                }
                radioStarted = false;
            }
        }

        public static async void AddSongToQueueByKey(string songKey, int channelId)
        {
            SongInfo info = await BeatSaver.InfoFromID(songKey);
            if (info != null)
            {
                radioChannels[channelId].radioQueue.Enqueue(info);
                Logger.Instance.Log("Successfully added songs to the queue!");
                File.WriteAllText($"RadioQueue{channelId}.json", JsonConvert.SerializeObject(radioChannels[channelId].radioQueue, Formatting.Indented));
            }
        }

        public static async void AddSongToQueueByHash(string hash, int channelId)
        {
            SongInfo info = await BeatSaver.InfoFromHash(hash);
            if (info != null)
            {
                radioChannels[channelId].radioQueue.Enqueue(info);
                Logger.Instance.Log("Successfully added songs to the queue!");
                File.WriteAllText($"RadioQueue{channelId}.json", JsonConvert.SerializeObject(radioChannels[channelId].radioQueue, Formatting.Indented));
            }
        }

        public static async void AddPlaylistToQueue(string path, int channelId)
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
                        Logger.Instance.Error($"Unable to add playlist to queue! File \"{path}\" does not exist!");
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
                    if (song == null)
                        continue;

                    if (!string.IsNullOrEmpty(song.hash))
                    {
                        if (song.hash.Length >= 40)
                        {
                            radioChannels[channelId].radioQueue.Enqueue(new SongInfo() { levelId = song.hash.ToUpper().Substring((song.hash.Length - 40), 40), songName = song.songName, key = song.key });
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(song.levelId))
                    {
                        if (song.levelId.Length >= 40)
                        {
                            radioChannels[channelId].radioQueue.Enqueue(new SongInfo() { levelId = song.levelId.ToUpper().Substring((song.hash.Length - 40), 40), songName = song.songName, key = song.key });
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(song.key))
                    {
                        SongInfo info = await BeatSaver.InfoFromID(song.key);
                        if(info != null)
                            radioChannels[channelId].radioQueue.Enqueue(info);
                        continue;
                    }
                }
                Logger.Instance.Log("Successfully added all songs from playlist to the queue!");
                File.WriteAllText($"RadioQueue{channelId}.json", JsonConvert.SerializeObject(radioChannels[channelId].radioQueue, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Instance.Error("Unable to add playlist to queue! Exception: " + e);
                return;
            }
        }

        public static bool ClientJoinedChannel(Client client, int channelId)
        {
            if (Settings.Instance.Radio.EnableRadio && radioStarted)
            {
                if (radioChannels.Count > channelId)
                {
                    radioChannels[channelId].ClientJoined(client);
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }

        public static void ClientLeftChannel(Client client)
        {
            foreach(RadioChannel channel in radioChannels)
            {
                if (channel.radioClients.Contains(client))
                {
                    channel.radioClients.Remove(client);
                }
            }
        }
    }
}
