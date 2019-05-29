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
        private static string _currentScene;

        static List<PlayerInfo> playerInfos = new List<PlayerInfo>();

        public static void OnLoad()
        {
            Client.Instance.MessageReceived += PacketReceived;

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

        public static void MenuLoaded()
        {
            _currentScene = "MenuCore";
        }

        public static void GameLoaded()
        {
            _currentScene = "GameCore";
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
                tickRateLabel.Text = "Tickrate: " + Client.Instance.tickrate.ToString();
                playersLabel.Text = "Players: " + playersActive.ToString();
                visiblePlayersLabel.Text = "Visible players: " + visiblePlayers.ToString();

                playersListBox.Items.Clear();
                foreach (var playerInfo in playerInfos)
                {
                    playersListBox.Items.Add($"{playerInfo.playerName}:{playerInfo.playerId} ({playerInfo.playerState}) {playerInfo.avatarHash}");
                }
            }
            catch
            {

            }
        }

        private static void PacketReceived(NetIncomingMessage msg)
        {
            UpdateUI();

            if (msg == null)
                return;

            try
            {
                CommandType commandType = (CommandType)msg.ReadByte();
                
                if (commandType == CommandType.UpdatePlayerInfo)
                {
                    packetsReceived++;

                    msg.ReadFloat();
                    msg.ReadFloat();

                    playersActive = msg.ReadInt32();

                    playerInfos.Clear();
                    for (int j = 0; j < playersActive; j++)
                    {
                        try
                        {
                            PlayerInfo playerInfo = new PlayerInfo(msg);
                            playerInfos.Add(playerInfo);
                        }
                        catch (Exception e)
                        {
                            Plugin.log.Info($"Unable to parse PlayerInfo! Excpetion: {e}");
                        }
                    }

                    visiblePlayers = playerInfos.Count(x => (x.playerState == PlayerState.Game && _currentScene == "GameCore") || (x.playerState == PlayerState.Room && _currentScene == "MenuCore") || (x.playerState == PlayerState.DownloadingSongs && _currentScene == "MenuCore"));

                    UpdateUI();
                }
            }
            catch(Exception e)
            {
                Plugin.log.Info("Unable to process packet in debug form! Exception: "+e);
            }
            
        }
    }


#endif
}
