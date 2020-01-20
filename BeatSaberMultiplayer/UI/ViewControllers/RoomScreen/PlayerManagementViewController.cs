using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BS_Utils.Utilities;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    public interface IPlayerManagementButtons
    {
        void MuteButtonWasPressed(PlayerInfo player);
        void TransferHostButtonWasPressed(PlayerInfo player);
    }

    class PlayerManagementViewController : BSMLResourceViewController, IPlayerManagementButtons
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action gameplayModifiersChanged;
        public event Action<PlayerInfo> transferHostButtonPressed;

        public GameplayModifiers modifiers { get { return modifiersPanel.gameplayModifiers; } }

        [UIComponent("ping-text")]
        public TextMeshProUGUI pingText;

        [UIComponent("modifiers-rect")]
        public RectTransform modifiersTab;

        public GameplayModifiersPanelController modifiersPanel;

        [UIComponent("modifiers-panel-blocker")]
        public Image modifiersPanelBlocker;
        
        [UIComponent("modifiers-rect")]
        public TableView playersTableView;

        [UIComponent("players-list")]
        public CustomCellListTableData playersList;

        [UIValue("players")]
        List<object> players = new List<object>();

        [UIAction("#post-parse")]
        protected void SetupViewController()
        {
            modifiersPanelBlocker.type = Image.Type.Sliced;
            modifiersPanelBlocker.color = new Color(0f, 0f, 0f, 0.75f);

            modifiersPanel = Instantiate(Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().First(), rectTransform, false);
            modifiersPanel.gameObject.SetActive(true);
            modifiersPanel.transform.SetParent(modifiersTab, false);
            (modifiersPanel.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
            (modifiersPanel.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
            (modifiersPanel.transform as RectTransform).anchoredPosition = new Vector2(0f, -23f);
            (modifiersPanel.transform as RectTransform).sizeDelta = new Vector2(120f, -23f);

            HoverHintController hoverHintController = Resources.FindObjectsOfTypeAll<HoverHintController>().First();

            foreach (var hint in modifiersPanel.GetComponentsInChildren<HoverHint>())
            {
                hint.SetPrivateField("_hoverHintController", hoverHintController);
            }

            modifiersPanel.Awake();

            modifiersPanel.SetData(GameplayModifiers.defaultModifiers);
            modifiersPanel.Refresh();

            var modifierToggles = modifiersPanel.GetPrivateField<GameplayModifierToggle[]>("_gameplayModifierToggles");

            foreach (var item in modifierToggles)
            {
                item.toggle.onValueChanged.AddListener((enabled) => { gameplayModifiersChanged?.Invoke(); });
            }

            playersList.tableView.ReloadData();
        }

        public void Update()
        {
            if(Time.frameCount % 45 == 0 && pingText != null && Client.Instance.networkClient != null && Client.Instance.networkClient.Connections.Count > 0)
                pingText.text = "PING: "+ Math.Round(Client.Instance.networkClient.Connections[0].AverageRoundtripTime*1000, 2).ToString();
        }

        public void UpdateViewController(bool isHost, bool modifiersInteractable)
        {
            if(modifiersPanelBlocker != null)
                modifiersPanelBlocker.gameObject.SetActive(!isHost || !modifiersInteractable);
        }

        public void UpdatePlayerList(RoomState state)
        {
            
            var playersDict = InGameOnlineController.Instance.players;
                
            if (playersDict.Count != players.Count)
            {
                while(playersDict.Count > players.Count)
                {
                    players.Add(new PlayerListObject(null, this));
                }
                if (playersDict.Count < players.Count)
                    players.RemoveRange(playersDict.Count, players.Count - playersDict.Count);

                playersList.tableView.ReloadData();
            }

            int index = 0;
            foreach(var playerPair in playersDict)
            {
                (players[index] as PlayerListObject).Update(playerPair.Value.playerInfo, state);
                index++;
            }            
        }

        public void SetGameplayModifiers(GameplayModifiers modifiers)
        {
            
            if (modifiersPanel != null)
            {
                modifiersPanel.SetData(modifiers);
                modifiersPanel.Refresh();
            }
            
        }

        public void MuteButtonWasPressed(PlayerInfo player)
        {
            if (player == null)
                return;

            if (InGameOnlineController.Instance.mutedPlayers.Contains(player.playerId))
            {
                InGameOnlineController.Instance.mutedPlayers.Remove(player.playerId);
            }
            else
            {
                InGameOnlineController.Instance.mutedPlayers.Add(player.playerId);
            }
        }

        public void TransferHostButtonWasPressed(PlayerInfo player)
        {
            if (player == null)
                return;

            if (Client.Instance.connected && Client.Instance.isHost)
            {
                transferHostButtonPressed?.Invoke(player);
            }
        }

        public class PlayerListObject
        {
            [UIComponent("speaker-icon")]
            public Image speakerIcon;

            [UIComponent("control-buttons")]
            public RectTransform controlButtonsRect;

            [UIComponent("pass-host-button")]
            public Button passHostButton;

            [UIComponent("mute-button")]
            public Button muteButton;

            [UIComponent("progress-text")]
            public TextMeshProUGUI progressText;

            [UIComponent("player-name")]
            public TextMeshProUGUI playerName;

            public PlayerInfo playerInfo;

            private IPlayerManagementButtons _buttonsInterface;
            private bool _isMuted;

            private bool _isInitialized;

            public PlayerListObject(PlayerInfo info, IPlayerManagementButtons buttons)
            {
                playerInfo = info;
                _buttonsInterface = buttons;
            }

            [UIAction("refresh-visuals")]
            public void Refresh(bool selected, bool highlighted)
            {
                if (playerInfo != null)
                {
                    playerName.text = playerInfo.playerName;
                    playerName.color = playerInfo.updateInfo.playerNameColor;

                    passHostButton.onClick.RemoveAllListeners();
                    passHostButton.onClick.AddListener(() => _buttonsInterface.TransferHostButtonWasPressed(playerInfo));
                    muteButton.onClick.RemoveAllListeners();
                    muteButton.onClick.AddListener(() => _buttonsInterface.MuteButtonWasPressed(playerInfo));
                }
                _isInitialized = true;
            }

            public void Update(PlayerInfo info, RoomState state)
            {
                if (!_isInitialized)
                    return;

                if (info.playerId != playerInfo?.playerId)
                {
                    playerInfo = info;
                    Refresh(false, false);
                }
                else
                {
                    playerInfo.updateInfo = info.updateInfo;
                }

                speakerIcon.enabled = InGameOnlineController.Instance.VoiceChatIsTalking(playerInfo.playerId);

                controlButtonsRect.gameObject.SetActive(( state == RoomState.SelectingSong || state == RoomState.Results) && !playerInfo.Equals(Client.Instance.playerInfo));
                passHostButton.interactable = Client.Instance.isHost && !playerInfo.Equals(Client.Instance.playerInfo);

                if (_isMuted && !InGameOnlineController.Instance.mutedPlayers.Contains(playerInfo.playerId))
                {
                    _isMuted = false;
                    muteButton.SetButtonText("MUTE");
                }
                else if (!_isMuted && InGameOnlineController.Instance.mutedPlayers.Contains(playerInfo.playerId))
                {
                    _isMuted = true;
                    muteButton.SetButtonText("UNMUTE");
                }

                progressText.gameObject.SetActive(state == RoomState.Preparing);

                if(playerInfo.updateInfo.playerProgress < 0f)
                {
                    progressText.text = "ERROR";
                }
                else if (playerInfo.updateInfo.playerState == PlayerState.DownloadingSongs && playerInfo.updateInfo.playerProgress < 100f)
                {
                    progressText.text = (playerInfo.updateInfo.playerProgress / 100f).ToString("P");
                }
                else
                {
                    progressText.text = "DOWNLOADED";
                }
            }
        }
    }


}
