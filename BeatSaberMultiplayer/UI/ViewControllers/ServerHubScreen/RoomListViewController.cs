using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using HMUI;
using ServerHub.Data;
using System;
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
        public event Action<RoomInfo> selectedRoom;

        private Button _pageUpButton;
        private Button _pageDownButton;
        private Button _createRoom;

        TableView _serverTableView;
        StandardLevelListTableCell _serverTableCellInstance;

        List<RoomInfo> availableRooms = new List<RoomInfo>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _serverTableCellInstance = Resources.FindObjectsOfTypeAll<StandardLevelListTableCell>().First(x => (x.name == "StandardLevelListTableCell"));

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _serverTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _serverTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                _createRoom = BeatSaberUI.CreateUIButton(rectTransform, "ApplyButton");
                BeatSaberUI.SetButtonText(_createRoom, "Create room");
                BeatSaberUI.SetButtonTextSize(_createRoom, 3f);
                (_createRoom.transform as RectTransform).sizeDelta = new Vector2(38f, 6f);
                (_createRoom.transform as RectTransform).anchoredPosition = new Vector2(-19f, 73f);
                _createRoom.onClick.RemoveAllListeners();
                _createRoom.onClick.AddListener(delegate ()
                {
                    createRoomButtonPressed?.Invoke();
                });

                _serverTableView = new GameObject().AddComponent<TableView>();
                _serverTableView.transform.SetParent(rectTransform, false);

                Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _serverTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _serverTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_serverTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                (_serverTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                (_serverTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_serverTableView.transform as RectTransform).position = new Vector3(0f, 0f, 2.4f);
                (_serverTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                ReflectionUtil.SetPrivateField(_serverTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_serverTableView, "_pageDownButton", _pageDownButton);

                _serverTableView.didSelectRowEvent += ServerTableView_DidSelectRow;
                _serverTableView.dataSource = this;

            }
            else
            {
                _serverTableView.ReloadData();
            }

        }

        public void SetRooms(List<ServerHubClient> serverHubs)
        {
            if (serverHubs == null)
            {
                availableRooms.Clear();
            }
            else
            {
                availableRooms = serverHubs.SelectMany(x => x.availableRooms).ToList();
            }

            if (_serverTableView.dataSource != this)
            {
                _serverTableView.dataSource = this;
            }
            else
            {
                _serverTableView.ReloadData();
            }

            _serverTableView.ScrollToRow(0, false);
        }

        private void ServerTableView_DidSelectRow(TableView sender, int row)
        {
            selectedRoom?.Invoke(availableRooms[row]);
        }

        public TableCell CellForRow(int row)
        {
            StandardLevelListTableCell cell = Instantiate(_serverTableCellInstance);

            RoomInfo room = availableRooms[row];
            
            if (room.usePassword)
            {
                cell.coverImage = Base64Sprites.lockedRoomIcon;
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
