using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerHub.Data
{
    public enum ChannelState : byte { InGame, Voting, NextSong, Results };
    public enum BeatmapDifficulty { Easy, Normal, Hard, Expert, ExpertPlus };

    public class ChannelInfo
    {
        public int channelId;
        public string name;
        public string iconUrl;
        public ChannelState state;
        public SongInfo currentSong;
        public LevelOptionsInfo currentLevelOptions;
        public int playerCount;
        public string ip;
        public int port;

        public ChannelInfo()
        {

        }

        public ChannelInfo(NetIncomingMessage msg)
        {
            channelId = msg.ReadInt32();
            name = msg.ReadString();
            iconUrl = msg.ReadString();
            state = (ChannelState)msg.ReadByte();

            if (state != ChannelState.Voting)
            {
                currentSong = new SongInfo(msg);
                currentLevelOptions = new LevelOptionsInfo(msg);

                if (currentSong.songName == "Selecting song..." && currentSong.levelId == "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")
                {
                    currentSong = null;
                    currentLevelOptions = null;
                }
            }
            playerCount = msg.ReadInt32();
        }

        public void AddToMessage(NetOutgoingMessage msg)
        {
            msg.Write(channelId);
            msg.Write(name);
            msg.Write(iconUrl);
            msg.Write((byte)state);

            if (state != ChannelState.Voting)
            {
                if (currentSong != null)
                {
                    currentSong.AddToMessage(msg);
                    currentLevelOptions.AddToMessage(msg);
                }
                else
                {
                    new SongInfo() { songName = "Selecting song...", levelId = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF" }.AddToMessage(msg);
                    new LevelOptionsInfo(BeatmapDifficulty.Hard, new GameplayModifiers(), "Standard").AddToMessage(msg);
                }
            }

            msg.Write(playerCount);
        }
    }
}
