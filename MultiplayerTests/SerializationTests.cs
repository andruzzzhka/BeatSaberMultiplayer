using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServerHub.Data;

namespace MultiplayerTests
{
    [TestClass]
    public class SerializationTests
    {
        [TestMethod]
        public void PlayerInfoSerializationTest()
        {
            for (int i = 0; i < 100000; i++)
            {
                PlayerInfo initial = new PlayerInfo("andruzzzhka", 76561198047255564) { playerScore = 14000, playerComboBlocks = 15, playerCutBlocks = 150, playerEnergy = 0.6f };

                byte[] converted = initial.ToBytes();

                PlayerInfo deserialized = new PlayerInfo(converted);

                Assert.AreEqual(initial, deserialized);
            }
        }

        [TestMethod]
        public void RoomSettingsSerializationTest()
        {
            for (int i = 0; i < 100000; i++)
            {
                RoomSettings initial = new RoomSettings() { Name = "Debug Server", UsePassword = true, Password = "testtesttest", MaxPlayers = 4, NoFail = true };

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
                RoomInfo initial = new RoomInfo() { name = "Debug Server", usePassword = true, players = 2, maxPlayers = 4, noFail = true };

                byte[] converted = initial.ToBytes().Skip(4).ToArray();

                RoomInfo deserialized = new RoomInfo(converted);

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
    }
}
