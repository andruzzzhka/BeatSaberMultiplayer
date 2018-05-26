using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer
{
    class MultiplayerLeaderboardViewController : VRUIViewController, TableView.IDataSource
    {
        BSMultiplayerUI ui;
        MultiplayerLobbyViewController _parentMasterViewController;

        public TableView _leaderboardTableView;
        LeaderboardTableCell _leaderboardTableCellInstance;

        PlayerInfo[] playerInfos;

        protected override void DidActivate()
        {
            ui = BSMultiplayerUI._instance;

            _parentMasterViewController = transform.parent.GetComponent<MultiplayerLobbyViewController>();

            _leaderboardTableCellInstance = Resources.FindObjectsOfTypeAll<LeaderboardTableCell>().First(x => (x.name == "LeaderboardTableCell"));

            if (_leaderboardTableView == null)
            {
                _leaderboardTableView = new GameObject().AddComponent<TableView>();

                _leaderboardTableView.transform.SetParent(rectTransform, false);

                Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _leaderboardTableView.transform, false);

                _leaderboardTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_leaderboardTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0.5f);
                (_leaderboardTableView.transform as RectTransform).anchorMax = new Vector2(1f, 0.5f);
                (_leaderboardTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_leaderboardTableView.transform as RectTransform).position = new Vector3(0f, 0f, 2.4f);
                (_leaderboardTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);
                                
            }

        }

        public void SetLeaderboard(PlayerInfo[] _playerInfos)
        {
            playerInfos = _playerInfos;
            if ((object)_leaderboardTableView.dataSource != this)
            {
                _leaderboardTableView.dataSource = this;
            }
            else
            {
                _leaderboardTableView.ReloadData();
            }
        }

        public TableCell CellForRow(int row)
        {
            LeaderboardTableCell cell = Instantiate(_leaderboardTableCellInstance);

            cell.playerName = playerInfos[row].playerName;
            cell.score = playerInfos[row].playerScore;
            cell.rank = row+1;
            cell.showFullCombo = false;

            return cell;
        }

        public int NumberOfRows()
        {
            return Math.Min(playerInfos.Length,10);
        }

        public float RowHeight()
        {
            return 7f;
        }
    }
}
