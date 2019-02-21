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
    class RoomManagementViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action DestroyRoomPressed;

        Button _destroyRoomButton;

        Button _pageUpButton;
        Button _pageDownButton;

        TableView _downloadProgressTableView;

        List<PlayerInfo> _playersList = new List<PlayerInfo>();
        LeaderboardTableCell _downloadListTableCellInstance;
        List<PlayerListTableCell> _tableCells = new List<PlayerListTableCell>();

        TextMeshProUGUI _pingText;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _downloadListTableCellInstance = Resources.FindObjectsOfTypeAll<LeaderboardTableCell>().First();

                _destroyRoomButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(44f, 33f), new Vector2(26f, 10f), () => { DestroyRoomPressed?.Invoke(); }, "Destroy\nroom");
                _destroyRoomButton.ToggleWordWrapping(false);

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -12f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _downloadProgressTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 6f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _downloadProgressTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                RectTransform container = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.anchorMin = new Vector2(0.15f, 0.5f);
                container.anchorMax = new Vector2(0.85f, 0.5f);
                container.sizeDelta = new Vector2(0f, 56f);
                container.anchoredPosition = new Vector2(0f, -3f);

                _downloadProgressTableView = new GameObject("CustomTableView").AddComponent<TableView>();
                _downloadProgressTableView.gameObject.AddComponent<RectMask2D>();
                _downloadProgressTableView.transform.SetParent(container, false);

                _downloadProgressTableView.SetPrivateField("_isInitialized", false);
                _downloadProgressTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _downloadProgressTableView.Init();

                (_downloadProgressTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (_downloadProgressTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_downloadProgressTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (_downloadProgressTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                ReflectionUtil.SetPrivateField(_downloadProgressTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_downloadProgressTableView, "_pageDownButton", _pageDownButton);
                
                _downloadProgressTableView.dataSource = this;
                
                _pingText = this.CreateText("PING: 0", new Vector2(75f, 22.5f));
                _pingText.alignment = TextAlignmentOptions.Left;
            }

        }

        public void Update()
        {

            if(_pingText != null && Client.Instance.NetworkClient != null && Client.Instance.NetworkClient.Connections.Count > 0)
            {
                _pingText.text = "PING: "+ Math.Round(Client.Instance.NetworkClient.Connections[0].AverageRoundtripTime*1000, 2);
            }

        }

        public void UpdateViewController(bool isHost)
        {
            _destroyRoomButton.gameObject.SetActive(isHost);
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
                    _downloadProgressTableView.RefreshTable(false);
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

            _downloadProgressTableView.ScrollToRow(0, false);
        }

        public float RowHeight()
        {
            return 7f;
        }

        public int NumberOfRows()
        {
            return _playersList.Count;
        }

        public TableCell CellForRow(int row)
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
