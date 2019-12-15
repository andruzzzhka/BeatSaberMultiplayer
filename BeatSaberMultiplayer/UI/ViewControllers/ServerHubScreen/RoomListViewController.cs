using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BeatSaberMarkupLanguage.Components.CustomListTableData;

namespace BeatSaberMultiplayer.UI.ViewControllers.ServerHubScreen
{
    class RoomListViewController : BSMLResourceViewController
    {
        public override string ResourceName => "BeatSaberMultiplayer.UI.ViewControllers.ServerHubScreen.RoomListViewController";

        public event Action createRoomButtonPressed;
        public event Action<ServerHubRoom> selectedRoom;
        public event Action refreshPressed;

        [UIComponent("refresh-btn")]
        private Button _refreshButton;

        [UIComponent("rooms-list")]
        public CustomListTableData roomsList;

        [UIValue("rooms")]
        public List<CustomCellInfo> roomInfosList = new List<CustomCellInfo>();

        private Dictionary<CustomCellInfo, ServerHubRoom> roomInfoDict = new Dictionary<CustomCellInfo, ServerHubRoom>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);

            roomsList.tableView.ClearSelection();
        }

        public void SetRooms(List<ServerHubRoom> rooms)
        {
            roomInfosList.Clear();
            roomInfoDict.Clear();

            if (rooms != null)
            { 
                var availableRooms = rooms.OrderByDescending(y => y.roomInfo.players).ToList();

                foreach (ServerHubRoom room in availableRooms)
                {
                    CustomCellInfo cellInfo = GetRoomInfo(room);
                    roomInfosList.Add(cellInfo);
                    roomInfoDict.Add(cellInfo, room);
                }
            }

            roomsList.tableView.ReloadData();
        }

        private CustomCellInfo GetRoomInfo(ServerHubRoom room)
        {
            CustomCellInfo cell = new CustomCellInfo($"({room.roomInfo.players}/{((room.roomInfo.maxPlayers == 0) ? "INF" : room.roomInfo.maxPlayers.ToString())}) {room.roomInfo.name}");
            
            switch (room.roomInfo.roomState)
            {
                case RoomState.InGame:
                    cell.subtext = "In game";
                    break;
                case RoomState.Preparing:
                    cell.subtext = "Preparing";
                    break;
                case RoomState.Results:
                    cell.subtext = "Results";
                    break;
                case RoomState.SelectingSong:
                    cell.subtext = "Selecting song";
                    break;
                default:
                    cell.subtext = room.roomInfo.roomState.ToString();
                    break;
            }

            if (room.roomInfo.usePassword)
                cell.icon = Sprites.lockedRoomIcon.texture;

            return cell;
        }

        public void SetRefreshButtonState(bool enabled)
        {
            _refreshButton.interactable = enabled;
        }

        [UIAction("room-selected")]
        private void RoomSelected(TableView sender, CustomCellInfo obj)
        {
            if(roomInfoDict.TryGetValue(obj, out ServerHubRoom room))
                selectedRoom?.Invoke(room);
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
    }
}
