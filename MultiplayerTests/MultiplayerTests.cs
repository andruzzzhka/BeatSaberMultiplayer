using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerHub.Data;

namespace MultiplayerTests
{
    [TestClass]
    public class MultiplayerTests
    {
        [TestMethod]
        public void PlayerInfoSerializationTest()
        {
            for (int i = 0; i < 100000; i++)
            {
                PlayerInfo initial = new PlayerInfo("andruzzzhka", 76561198047255564) { playerScore = 14000, playerComboBlocks = 15, playerState = PlayerState.Spectating, playerCutBlocks = 150, playerEnergy = 0.6f };

                byte[] converted = initial.ToBytes().Skip(4).ToArray();

                PlayerInfo deserialized = new PlayerInfo(converted);

                Assert.AreEqual(initial, deserialized);
            }
        }

        [TestMethod]
        public void RoomSettingsSerializationTest()
        {
            for (int i = 0; i < 100000; i++)
            {
                RoomSettings initial = new RoomSettings() { Name = "Debug Server", UsePassword = true, Password = "testtesttest", MaxPlayers = 4, NoFail = true, AvailableSongs = new List<SongInfo>() { new SongInfo() { songName = "TEST SONG", levelId = "CC773C754F14B6290B5D2CB196EB1BF4", songDuration = 230f } } };

                byte[] converted = initial.ToBytes().Skip(4).ToArray();

                RoomSettings deserialized = new RoomSettings(converted);

                Assert.AreEqual(initial, deserialized);
            }
        }

        [TestMethod]
        public void RoomInfoSerializationTest()
        {
            for (int i = 0; i < 100000; i++)
            {
                RoomInfo initial = new RoomInfo() { name = "Debug Server", usePassword = true, players = 2, maxPlayers = 4, noFail = true, roomHost = new PlayerInfo("andruzzzhka", 76561198047255564) { playerScore = 14000, playerComboBlocks = 15, playerState = PlayerState.Spectating, playerCutBlocks = 150, playerEnergy = 0.6f }, roomState = RoomState.SelectingSong, selectedDifficulty = 3, selectedSong = new SongInfo() { songName = "Test song", songDuration = 350f, levelId = "281C6C8588C5B1F72F7584BAAE98ADFC" }, songSelectionType = SongSelectionType.Random };

                byte[] converted = initial.ToBytes().Skip(4).ToArray();

                RoomInfo deserialized = new RoomInfo(converted);

                Assert.AreEqual(initial, deserialized);
            }
        }

        [TestMethod]
        public void RoomInfoListSerializationTest()
        {
            for (int i = 0; i < 100000; i++)
            {
                List<RoomInfo> initial = new List<RoomInfo>() { new RoomInfo() { name = "Debug Server1", usePassword = true, roomHost = new PlayerInfo("andruzzzhka", 76561198047255564) }, new RoomInfo() { name = "Debug Server2", usePassword = true, roomHost = new PlayerInfo("someoneElse", 76561198047255564) }, new RoomInfo() { name = "Debug Server3", usePassword = true, roomHost = new PlayerInfo("someoneElse2", 76561198047255564) } };

                byte[] serialized = GetRoomsListInBytes(initial);


                int roomsCount = BitConverter.ToInt32(serialized, 0);

                Stream byteStream = new MemoryStream(serialized, 4, serialized.Length - 4);

                List<RoomInfo> deserialized = new List<RoomInfo>();
                for (int j = 0; j < roomsCount; j++)
                {
                    byte[] sizeBytes = new byte[4];
                    byteStream.Read(sizeBytes, 0, 4);

                    int roomInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                    byte[] roomInfoBytes = new byte[roomInfoSize];
                    byteStream.Read(roomInfoBytes, 0, roomInfoSize);

                    deserialized.Add(new RoomInfo(roomInfoBytes));
                }

                Assert.IsTrue(initial.SequenceEqual(deserialized), "RoomInfos are not equal!");
            }

            
        }

        [TestMethod]
        public void SongInfoSerializationTest()
        {
            for (int i = 0; i < 100000; i++)
            {
                SongInfo initial = new SongInfo() { songName = "TEST SONG", levelId = "CC773C754F14B6290B5D2CB196EB1BF4", songDuration = 230f };

                byte[] converted = initial.ToBytes().Skip(4).ToArray();

                SongInfo deserialized = new SongInfo(converted);

                Assert.AreEqual(initial, deserialized);
            }
        }

        [TestMethod]
        public void BasePacketSerializationTest()
        {

            for (int i = 0; i < 100000; i++)
            {
                BasePacket initial = new BasePacket(new byte[] { 127, 255, 63, 31, 15 });

                byte[] converted = initial.ToBytes().Skip(4).ToArray();

                BasePacket deserialized = new BasePacket(converted);

                Assert.AreEqual(initial, deserialized);
            }
        }

        [TestMethod]
        public void VotingTest()
        {
            Dictionary<PlayerInfo, SongInfo> _votes = new Dictionary<PlayerInfo, SongInfo>();
            _votes.Add(new PlayerInfo("andruzzzhka1", 12341), new SongInfo() { songName = "TEST SONG 1", levelId = "07da4b5bc7795b08b87888b035760db7".ToUpper() });
            _votes.Add(new PlayerInfo("andruzzzhka2", 12342), new SongInfo() { songName = "TEST SONG 1", levelId = "07da4b5bc7795b08b87888b035760db7".ToUpper() });
            _votes.Add(new PlayerInfo("andruzzzhka3", 12343), new SongInfo() { songName = "TEST SONG 1", levelId = "07da4b5bc7795b08b87888b035760db7".ToUpper() });
            _votes.Add(new PlayerInfo("andruzzzhkb1", 12341), new SongInfo() { songName = "TEST SONG 2", levelId = "37da4b5bc7795b08b87888b035760db7".ToUpper() });
            _votes.Add(new PlayerInfo("andruzzzhkb2", 12342), new SongInfo() { songName = "TEST SONG 2", levelId = "37da4b5bc7795b08b87888b035760db7".ToUpper() });
            _votes.Add(new PlayerInfo("andruzzzhkc1", 12343), new SongInfo() { songName = "TEST SONG 3", levelId = "32da4b5bc7795b08b88888b035760db7".ToUpper() });

            SongInfo result = _votes.GroupBy(x => x.Value).OrderByDescending(y => y.Count()).First().Key;

            Assert.AreEqual(new SongInfo() { songName = "TEST SONG 1", levelId = "07da4b5bc7795b08b87888b035760db7".ToUpper() }, result);
        }

        public static byte[] GetRoomsListInBytes(List<RoomInfo> rooms)
        {
            List<byte> buffer = new List<byte>();

            buffer.AddRange(BitConverter.GetBytes(rooms.Count));

            rooms.ForEach(x => buffer.AddRange(x.ToBytes()));

            return buffer.ToArray();
        }
    }
}
