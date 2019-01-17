using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine.SceneManagement;
using System.Timers;
using Lidgren.Network;

namespace BeatSaberMultiplayer
{
#if DEBUG
    static class DebugForm
    {
        static Form debugForm;
        static private Label tickRateLabel;
        static private Label playersLabel;
        static private ListBox playersListBox;
        static private Label visiblePlayersLabel;

        static int packetsReceived;
        static int playersActive;
        static int visiblePlayers;
        static RoomInfo roomInfo;
        private static Scene _currentScene;

        static List<PlayerInfo> playerInfos = new List<PlayerInfo>();

        public static void OnLoad()
        {
            Client.ClientCreated += Client_ClientCreated;

            _currentScene = SceneManager.GetActiveScene();
            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;

            debugForm = new Form();

            tickRateLabel = new Label();
            playersLabel = new Label();
            playersListBox = new ListBox();
            visiblePlayersLabel = new Label();
            debugForm.SuspendLayout();
            // 
            // packetsLabel
            // 
            tickRateLabel.AutoSize = true;
            tickRateLabel.Location = new Point(10, 9);
            tickRateLabel.Name = "tickRateLabel";
            tickRateLabel.Size = new Size(102, 13);
            tickRateLabel.TabIndex = 0;
            tickRateLabel.Text = "Tickrate: Unknown";
            // 
            // playersLabel
            // 
            playersLabel.AutoSize = true;
            playersLabel.Location = new Point(10, 22);
            playersLabel.Name = "playersLabel";
            playersLabel.Size = new Size(53, 13);
            playersLabel.TabIndex = 1;
            playersLabel.Text = "Players: 0";
            // 
            // playersListBox
            // 
            playersListBox.FormattingEnabled = true;
            playersListBox.Location = new Point(15, 51);
            playersListBox.Name = "playersListBox";
            playersListBox.Size = new Size(450, 95);
            playersListBox.TabIndex = 3;
            // 
            // visiblePlayersLabel
            // 
            visiblePlayersLabel.AutoSize = true;
            visiblePlayersLabel.Location = new Point(12, 35);
            visiblePlayersLabel.Name = "visiblePlayersLabel";
            visiblePlayersLabel.Size = new Size(85, 13);
            visiblePlayersLabel.TabIndex = 4;
            visiblePlayersLabel.Text = "Visible players: 0";
            // 
            // DebugForm
            // 
            debugForm.AutoScaleDimensions = new SizeF(6F, 13F);
            debugForm.AutoScaleMode = AutoScaleMode.Font;
            debugForm.ClientSize = new Size(475, 161);
            debugForm.Controls.Add(visiblePlayersLabel);
            debugForm.Controls.Add(playersListBox);
            debugForm.Controls.Add(playersLabel);
            debugForm.Controls.Add(tickRateLabel);
            debugForm.Name = "DebugForm";
            debugForm.Text = "DebugForm";
            debugForm.ResumeLayout(false);
            debugForm.PerformLayout();

            debugForm.Show();
        }

        private static void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            try
            {
                if (to.name == "GameCore" || to.name == "Menu")
                {
                    _currentScene = to;
                }
            }
            catch
            {
            }
        }

        private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
        }

        private static void Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        public static void UpdateUI()
        {
            try
            {
                tickRateLabel.Text = "Tickrate: " + Client.Instance.Tickrate.ToString();
                playersLabel.Text = "Players: " + playersActive.ToString();
                visiblePlayersLabel.Text = "Visible players: " + visiblePlayers.ToString();

                playersListBox.Items.Clear();
                foreach (var playerInfo in playerInfos)
                {
                    playersListBox.Items.Add($"{playerInfo.playerName}:{playerInfo.playerId} ({playerInfo.playerState}) Score: {playerInfo.playerScore}");
                }
            }
            catch
            {

            }
        }

        private static void Client_ClientCreated()
        {
            Client.Instance.MessageReceived += PacketReceived;
        }

        private static void PacketReceived(NetIncomingMessage msg)
        {
            UpdateUI();
            /*
            try
            {
                CommandType commandType = (CommandType)msg.ReadByte();

                if (commandType == CommandType.UpdatePlayerInfo)
                {
                    packetsReceived++;

                    playersActive = msg.ReadInt32();

                    Stream byteStream = new MemoryStream(msg.additionalData, 12, msg.additionalData.Length - 12);


                    playersListBox.Items.Clear();

                    playerInfos.Clear();
                    for (int j = 0; j < playersActive; j++)
                    {
                        byte[] sizeBytes = new byte[4];
                        byteStream.Read(sizeBytes, 0, 4);

                        int playerInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                        byte[] playerInfoBytes = new byte[playerInfoSize];
                        byteStream.Read(playerInfoBytes, 0, playerInfoSize);

                        try
                        {
                            PlayerInfo playerInfo = new PlayerInfo(playerInfoBytes);
                            playerInfos.Add(playerInfo);
                        }
                        catch (Exception e)
                        {
                            //MessageBox.Show($"Unable to parse PlayerInfo! Excpetion: {e}", "Exception");
                        }
                    }

                    visiblePlayers = playerInfos.Count(x => (x.playerState == PlayerState.Game && _currentScene.name == "GameCore") || (x.playerState == PlayerState.Room && _currentScene.name == "Menu") || (x.playerState == PlayerState.DownloadingSongs && _currentScene.name == "Menu"));

                    UpdateUI();
                }
                else if (commandType == CommandType.GetRoomInfo)
                {
                    if (msg.additionalData[0] == 1)
                    {
                        int songsCount = BitConverter.ToInt32(msg.additionalData, 1);

                        Stream byteStream = new MemoryStream(msg.additionalData, 5, msg.additionalData.Length - 5);

                        for (int j = 0; j < songsCount; j++)
                        {
                            byte[] sizeBytes = new byte[4];
                            byteStream.Read(sizeBytes, 0, 4);

                            int songInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                            byte[] songInfoBytes = new byte[songInfoSize];
                            byteStream.Read(songInfoBytes, 0, songInfoSize);
                        }

                        byte[] roomInfoSizeBytes = new byte[4];
                        byteStream.Read(roomInfoSizeBytes, 0, 4);

                        int roomInfoSize = BitConverter.ToInt32(roomInfoSizeBytes, 0);

                        byte[] roomInfoBytes = new byte[roomInfoSize];
                        byteStream.Read(roomInfoBytes, 0, roomInfoSize);

                        roomInfo = new RoomInfo(roomInfoBytes);
                    }
                    else
                    {
                        if (BitConverter.ToInt32(msg.additionalData, 1) == msg.additionalData.Length - 5)
                        {
                            roomInfo = new RoomInfo(msg.additionalData.Skip(5).ToArray());
                        }
                        else
                        {
                            roomInfo = new RoomInfo(msg.additionalData.Skip(1).ToArray());
                        }
                    }
                }
            }
            catch
            {

            }
            */
        }
    }


#endif
}
