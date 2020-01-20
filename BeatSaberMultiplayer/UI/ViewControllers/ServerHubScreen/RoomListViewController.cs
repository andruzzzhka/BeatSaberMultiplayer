using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.ViewControllers.ServerHubScreen
{
    class RoomListViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action createRoomButtonPressed;
        public event Action<ServerHubRoom> selectedRoom;
        public event Action refreshPressed;

        [UIComponent("refresh-btn")]
        private Button _refreshButton;

        [UIComponent("rooms-list")]
        public CustomCellListTableData roomsList;

        [UIValue("rooms")]
        public List<object> roomInfosList = new List<object>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);

            roomsList.tableView.ClearSelection();
        }

        public void SetRooms(List<ServerHubRoom> rooms)
        {
            roomInfosList.Clear();

            if (rooms != null)
            {
                var availableRooms = rooms.OrderByDescending(y => y.roomInfo.players);

                foreach (ServerHubRoom room in availableRooms)
                {
                    roomInfosList.Add(new RoomListObject(room));
                }
            }

            roomsList.tableView.ReloadData();
        }

        public void SetRefreshButtonState(bool enabled)
        {
            _refreshButton.interactable = enabled;
        }

        [UIAction("room-selected")]
        private void RoomSelected(TableView sender, RoomListObject obj)
        {
            selectedRoom?.Invoke(obj.room);
        }

        [UIAction("create-room-btn-pressed")]
        private void CreateRoomBtnPressed()
        {
            createRoomButtonPressed?.Invoke();
        }

        [UIAction("refresh-btn-pressed")]
        private void RefreshBtnPressed()
        {
            refreshPressed?.Invoke();
        }

        public class RoomListObject
        {
            public ServerHubRoom room;

            [UIValue("room-name")]
            private string roomName;

            [UIValue("room-state")]
            private string roomStateString;

            [UIComponent("locked-icon")]
            private RawImage lockedIcon;
            private bool locked;

            [UIComponent("bg")]
            private RawImage background;

            [UIComponent("room-state-text")]
            private TextMeshProUGUI roomStateText;

            public RoomListObject(ServerHubRoom room)
            {
                this.room = room;
                roomName = $"({room.roomInfo.players}/{((room.roomInfo.maxPlayers == 0) ? "INF" : room.roomInfo.maxPlayers.ToString())}) {room.roomInfo.name}";
                switch (room.roomInfo.roomState)
                {
                    case RoomState.InGame:
                        roomStateString = "In game";
                        break;
                    case RoomState.Preparing:
                        roomStateString = "Preparing";
                        break;
                    case RoomState.Results:
                        roomStateString = "Results";
                        break;
                    case RoomState.SelectingSong:
                        roomStateString = "Selecting song";
                        break;
                    default:
                        roomStateString = room.roomInfo.roomState.ToString();
                        break;
                }
                locked = room.roomInfo.usePassword;
            }

            [UIAction("refresh-visuals")]
            public void Refresh(bool selected, bool highlighted)
            {
                lockedIcon.texture = Sprites.lockedRoomIcon.texture;
                lockedIcon.enabled = locked;
                background.texture = Sprites.whitePixel.texture;
                background.color = new Color(1f, 1f, 1f, 0.125f);
                roomStateText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            }
        }
    }
}
