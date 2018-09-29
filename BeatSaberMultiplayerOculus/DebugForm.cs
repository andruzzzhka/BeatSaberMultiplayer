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

namespace BeatSaberMultiplayer
{
#if DEBUG
    static class DebugForm
    {
        static Form debugForm;
        static private Label packetsLabel;
        static private Label playersLabel;
        static private ListBox playersListBox;
        static private Label visiblePlayersLabel;

        static int packetsReceived;
        static int playersActive;
        static int visiblePlayers;
        private static Scene _currentScene;

        static List<PlayerInfo> playerInfos = new List<PlayerInfo>();

        static System.Timers.Timer timer = new System.Timers.Timer();

        public static void OnLoad()
        {
            Client.ClientCreated += Client_ClientCreated;

            _currentScene = SceneManager.GetActiveScene();
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            debugForm = new Form();

            packetsLabel = new Label();
            playersLabel = new Label();
            playersListBox = new ListBox();
            visiblePlayersLabel = new Label();
            debugForm.SuspendLayout();
            // 
            // packetsLabel
            // 
            packetsLabel.AutoSize = true;
            packetsLabel.Location = new Point(10, 9);
            packetsLabel.Name = "packetsLabel";
            packetsLabel.Size = new Size(102, 13);
            packetsLabel.TabIndex = 0;
            packetsLabel.Text = "Packets received: 0";
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
            debugForm.Controls.Add(packetsLabel);
            debugForm.Name = "DebugForm";
            debugForm.Text = "DebugForm";
            debugForm.ResumeLayout(false);
            debugForm.PerformLayout();

            debugForm.Show();

            //timer.Elapsed += Timer_Elapsed;
            //timer.AutoReset = true;
            //timer.Interval = 50;
            //timer.Start();
        }

        private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        public static void UpdateUI()
        {
            packetsLabel.Text = "Packets received: " + packetsReceived.ToString();
            playersLabel.Text = "Players: " + playersActive.ToString();
            visiblePlayersLabel.Text = "Visible players: " + visiblePlayers.ToString();

            playersListBox.Items.Clear();
            foreach (var playerInfo in playerInfos)
            {
                playersListBox.Items.Add($"{playerInfo.playerName}:{playerInfo.playerId} ({playerInfo.playerState})");
            }
        }

        private static void SceneManager_sceneLoaded(Scene next, LoadSceneMode arg1)
        {
            try
            {
                if (next.name == "StandardLevel" || next.name == "Menu")
                {
                    _currentScene = next;
                }
            }
            catch
            {
            }
        }

        private static void Client_ClientCreated()
        {
            Client.instance.PacketReceived += PacketReceived;
        }

        private static void PacketReceived(BasePacket packet)
        {
            if(packet.commandType == CommandType.UpdatePlayerInfo)
            {
                packetsReceived++;

                playersActive = BitConverter.ToInt32(packet.additionalData, 8);

                Stream byteStream = new MemoryStream(packet.additionalData, 12, packet.additionalData.Length - 12);


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
                        MessageBox.Show($"Unable to parse PlayerInfo! Excpetion: {e}", "Exception");
                    }
                }

                visiblePlayers = playerInfos.Count(x => (x.playerState == PlayerState.Game && _currentScene.name == "StandardLevel") || (x.playerState == PlayerState.Room && _currentScene.name == "Menu") || (x.playerState == PlayerState.DownloadingSongs && _currentScene.name == "Menu"));

                UpdateUI();
            }
        }
    }


#endif
}
