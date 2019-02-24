using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using CustomUI.BeatSaber;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers
{
    class RoomListViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action createRoomButtonPressed;
        public event Action<ServerHubRoom> selectedRoom;

        private Button _pageUpButton;
        private Button _pageDownButton;
        private Button _createRoom;

        TableView _serverTableView;
        LevelListTableCell _serverTableCellInstance;

        List<ServerHubRoom> availableRooms = new List<ServerHubRoom>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _serverTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _serverTableView.PageScrollUp();

                });

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _serverTableView.PageScrollDown();

                });

                _createRoom = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                _createRoom.SetButtonText("Create room");
                _createRoom.SetButtonTextSize(3f);
                (_createRoom.transform as RectTransform).sizeDelta = new Vector2(38f, 6f);
                (_createRoom.transform as RectTransform).anchoredPosition = new Vector2(0f, 36f);
                _createRoom.onClick.RemoveAllListeners();
                _createRoom.onClick.AddListener(delegate ()
                {
                    createRoomButtonPressed?.Invoke();
                });

                RectTransform container = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.anchorMin = new Vector2(0.3f, 0.5f);
                container.anchorMax = new Vector2(0.7f, 0.5f);
                container.sizeDelta = new Vector2(0f, 60f);
                container.anchoredPosition = new Vector2(0f, -3f);

                _serverTableView = new GameObject("CustomTableView").AddComponent<TableView>();
                _serverTableView.gameObject.AddComponent<RectMask2D>();
                _serverTableView.transform.SetParent(container, false);

                _serverTableView.SetPrivateField("_isInitialized", false);
                _serverTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _serverTableView.Init();

                (_serverTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (_serverTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_serverTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (_serverTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                //ReflectionUtil.SetPrivateField(_serverTableView, "_pageUpButton", _pageUpButton);
                //ReflectionUtil.SetPrivateField(_serverTableView, "_pageDownButton", _pageDownButton);

                _serverTableView.didSelectRowEvent += ServerTableView_DidSelectRow;
                _serverTableView.dataSource = this;

            }
            else
            {
                _serverTableView.ReloadData();
            }

        }

        public void SetRooms(List<ServerHubRoom> rooms)
        {
            int prevCount = availableRooms.Count;
            if (rooms == null)
            {
                availableRooms.Clear();
            }
            else
            {
                availableRooms = rooms.OrderByDescending(y => y.roomInfo.players).ToList();
            }

            if (_serverTableView.dataSource != this)
            {
                _serverTableView.dataSource = this;
            }
            else
            {
                _serverTableView.RefreshTable(false);
                if (prevCount == 0 && availableRooms.Count > 0)
                {
                    StartCoroutine(ScrollWithDelay());
                }
            }

        }

        IEnumerator ScrollWithDelay()
        {
            yield return null;
            yield return null;

            _serverTableView.ScrollToRow(0, false);
        }

        private void ServerTableView_DidSelectRow(TableView sender, int row)
        {
            selectedRoom?.Invoke(availableRooms[row]);
        }

        public TableCell CellForRow(int row)
        {
            LevelListTableCell cell = Instantiate(_serverTableCellInstance);
            cell.reuseIdentifier = "ServerTableCell";

            RoomInfo room = availableRooms[row].roomInfo;
            
            if (room.usePassword)
            {
                cell.coverImage = Sprites.lockedRoomIcon;
            }
            else
            {
                cell.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(x => x.name == "CoverImage").enabled = false;
            }
            cell.songName = $"({room.players}/{((room.maxPlayers == 0)? "INF":room.maxPlayers.ToString())})" + room.name;
            cell.author = $"{room.roomState.ToString()}{(room.noFail ? ", No Fail" : "")}";

            return cell;
        }

        public int NumberOfRows()
        {
            return availableRooms.Count;
        }

        public float RowHeight()
        {
            return 10f;
        }

    }
}
