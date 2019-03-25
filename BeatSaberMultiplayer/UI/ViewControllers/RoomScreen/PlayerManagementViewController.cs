using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.UIElements;
using CustomUI.BeatSaber;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class PlayerManagementViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action gameplayModifiersChanged;

        public GameplayModifiers modifiers { get { return _modifiersPanel.gameplayModifiers; } }

        RectTransform _playersTab;
        RectTransform _modifiersTab;

        TextSegmentedControl _tabControl;

        GameplayModifiersPanelController _modifiersPanel;
        RectTransform _modifiersPanelBlocker;

        Button _pageUpButton;
        Button _pageDownButton;

        TableView _playersTableView;

        List<PlayerInfo> _playersList = new List<PlayerInfo>();
        LeaderboardTableCell _downloadListTableCellInstance;
        List<PlayerListTableCell> _tableCells = new List<PlayerListTableCell>();

        TextMeshProUGUI _pingText;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _downloadListTableCellInstance = Resources.FindObjectsOfTypeAll<LeaderboardTableCell>().First();

                _tabControl = CustomExtensions.CreateTextSegmentedControl(rectTransform, new Vector2(0f, -9f));
                _tabControl.SetTexts(new string[] { "Players", "Modifiers" });
                _tabControl.didSelectCellEvent += _tabControl_didSelectCellEvent;

                #region Modifiers tab

                _modifiersTab = new GameObject("ModifiersTab", typeof(RectTransform)).GetComponent<RectTransform>();
                _modifiersTab.SetParent(rectTransform, false);
                _modifiersTab.anchorMin = new Vector2(0f, 0f);
                _modifiersTab.anchorMax = new Vector2(1f, 1f);
                _modifiersTab.anchoredPosition = new Vector2(0f, 0f);
                _modifiersTab.sizeDelta = new Vector2(0f, 0f);

                _modifiersPanel = Instantiate(Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().First(), rectTransform, false);
                _modifiersPanel.gameObject.SetActive(true);
                _modifiersPanel.transform.SetParent(_modifiersTab, false);
                (_modifiersPanel.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_modifiersPanel.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_modifiersPanel.transform as RectTransform).anchoredPosition = new Vector2(0f, -23f);
                (_modifiersPanel.transform as RectTransform).sizeDelta = new Vector2(120f, -23f);

                _modifiersPanel.Init(GameplayModifiers.defaultModifiers);
                _modifiersPanel.Awake();

                var modifierToggles = _modifiersPanel.GetPrivateField<GameplayModifierToggle[]>("_gameplayModifierToggles");

                foreach (var item in modifierToggles)
                {
                    item.toggle.onValueChanged.AddListener( (enabled) => { gameplayModifiersChanged?.Invoke(); });
                }

                _modifiersPanelBlocker = new GameObject("ModifiersPanelBlocker", typeof(RectTransform)).GetComponent<RectTransform>(); //"If it works it's not stupid"
                _modifiersPanelBlocker.SetParent(_modifiersTab, false);
                _modifiersPanelBlocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0f);
                _modifiersPanelBlocker.anchorMin = new Vector2(0f, 0f);
                _modifiersPanelBlocker.anchorMax = new Vector2(1f, 0f);
                _modifiersPanelBlocker.pivot = new Vector2(0.5f, 0f);
                _modifiersPanelBlocker.sizeDelta = new Vector2(-10f, 62f);
                _modifiersPanelBlocker.anchoredPosition = new Vector2(0f, 0f);

                #endregion

                #region Players tab

                _playersTab = new GameObject("PlayersTab", typeof(RectTransform)).GetComponent<RectTransform>();
                _playersTab.SetParent(rectTransform, false);
                _playersTab.anchorMin = new Vector2(0f, 0f);
                _playersTab.anchorMax = new Vector2(1f, 1f);
                _playersTab.anchoredPosition = new Vector2(0f, 0f);
                _playersTab.sizeDelta = new Vector2(0f, 0f);

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), _playersTab, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -18f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _playersTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), _playersTab, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 6f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _playersTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                RectTransform container = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(_playersTab, false);
                container.anchorMin = new Vector2(0.15f, 0.5f);
                container.anchorMax = new Vector2(0.85f, 0.5f);
                container.sizeDelta = new Vector2(0f, 49f);
                container.anchoredPosition = new Vector2(0f, -3f);

                _playersTableView = new GameObject("CustomTableView").AddComponent<TableView>();
                _playersTableView.gameObject.AddComponent<RectMask2D>();
                _playersTableView.transform.SetParent(container, false);

                _playersTableView.SetPrivateField("_isInitialized", false);
                _playersTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _playersTableView.Init();

                (_playersTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (_playersTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_playersTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (_playersTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                ReflectionUtil.SetPrivateField(_playersTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_playersTableView, "_pageDownButton", _pageDownButton);
                
                _playersTableView.dataSource = this;
                #endregion

                _pingText = this.CreateText("PING: 0", new Vector2(75f, 22.5f));
                _pingText.alignment = TextAlignmentOptions.Left;

                _tabControl_didSelectCellEvent(null, 0);
            }

            if (activationType == ActivationType.AddedToHierarchy)
            {
                SetGameplayModifiers(GameplayModifiers.defaultModifiers);
            }
        }

        private void _tabControl_didSelectCellEvent(SegmentedControl arg1, int selectedIndex)
        {
            _playersTab.gameObject.SetActive(selectedIndex == 0);
            _modifiersTab.gameObject.SetActive(selectedIndex == 1);
        }

        public void Update()
        {

            if(_pingText != null && Client.Instance.NetworkClient != null && Client.Instance.NetworkClient.Connections.Count > 0)
            {
                _pingText.text = "PING: "+ Math.Round(Client.Instance.NetworkClient.Connections[0].AverageRoundtripTime*1000, 2);
            }

        }

        public void UpdateViewController(bool isHost, bool modifiersInteractable)
        {
            _modifiersPanelBlocker.gameObject.SetActive(!isHost || !modifiersInteractable);
        }

        public void UpdatePlayerList(List<PlayerInfo> players, RoomState state)
        {
            try
            {
                int prevCount = _playersList.Count;
                _playersList.Clear();
                if (players != null)
                {
                    _playersList.AddRange(players.OrderBy(x => x.playerId));
                }
                
                if (prevCount != _playersList.Count)
                {
                    for (int i = 0; i < _tableCells.Count; i++)
                    {
                        Destroy(_tableCells[i].gameObject);
                    }
                    _tableCells.Clear();
                    _playersTableView.RefreshTable(false);
                    if(prevCount == 0 && _playersList.Count > 0)
                    {
                        StartCoroutine(ScrollWithDelay());
                    }
                }
                else
                {
                    for (int i = 0; i < _playersList.Count; i++)
                    {
                        if (_tableCells.Count > i)
                        {
                            _tableCells[i].playerName = _playersList[i].playerName;
                            _tableCells[i].progress = state == RoomState.Preparing ? (_playersList[i].playerState == PlayerState.DownloadingSongs ? (_playersList[i].playerProgress/100f) : 1f) : -1f;
                            _tableCells[i].IsTalking = InGameOnlineController.Instance.VoiceChatIsTalking(_playersList[i].playerId);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Misc.Logger.Exception($"Unable to update players list! Exception: {e}");
            }
        }

        IEnumerator ScrollWithDelay()
        {
            yield return null;
            yield return null;

            _playersTableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
        }

        public void SetGameplayModifiers(GameplayModifiers modifiers)
        {
            Misc.Logger.Info("Setting gameplay modifiers");
            if (_modifiersPanel != null)
            {
                _modifiersPanel.Init(modifiers);

                GameplayModifiersModelSO modifiersModel = Resources.FindObjectsOfTypeAll<GameplayModifiersModelSO>().First();

                var modifiersParams = modifiersModel.GetModifierParams(modifiers);

                foreach (GameplayModifierToggle gameplayModifierToggle in _modifiersPanel.GetPrivateField<GameplayModifierToggle[]>("_gameplayModifierToggles"))
                {
                    gameplayModifierToggle.toggle.isOn = modifiersParams.Contains(gameplayModifierToggle.gameplayModifier);
                }
            }
        }

        public float CellSize()
        {
            return 7f;
        }

        public int NumberOfCells()
        {
            return _playersList.Count;
        }

        public TableCell CellForIdx(int row)
        {
            LeaderboardTableCell _originalCell = Instantiate(_downloadListTableCellInstance);

            PlayerListTableCell _tableCell = _originalCell.gameObject.AddComponent<PlayerListTableCell>();

            _tableCell.Init();

            _tableCell.rank = 0;
            _tableCell.showFullCombo = false;
            _tableCell.playerName = _playersList[row].playerName;
            _tableCell.progress = (_playersList[row].playerState == PlayerState.DownloadingSongs ? (_playersList[row].playerProgress/100f) : 1f);

            _tableCells.Add(_tableCell);
            return _tableCell;
        }
    }
}
