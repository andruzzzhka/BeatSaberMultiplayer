using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using CustomUI.BeatSaber;
using HMUI;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer
{
    class LeaderboardViewController : VRUIViewController, TableView.IDataSource
    {
        private TableView _leaderboardTableView;
        private LeaderboardTableCell _leaderboardTableCellInstance;
        private TextMeshProUGUI _timerText;

        LevelListTableCell _songTableCell;

        PlayerInfo[] _playerInfos;

        private LevelSO _selectedSong;
        public LevelSO SelectedSong { get { return _selectedSong; } set { SetSong(value); } }

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _songTableCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")));
                (_songTableCell.transform as RectTransform).anchoredPosition = new Vector2(18f, 39f);

                _leaderboardTableCellInstance = Resources.FindObjectsOfTypeAll<LeaderboardTableCell>().First(x => (x.name == "LeaderboardTableCell"));

                _leaderboardTableView = new GameObject().AddComponent<TableView>();

                _leaderboardTableView.transform.SetParent(rectTransform, false);

                _leaderboardTableView.SetPrivateField("_isInitialized", false);
                _leaderboardTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _leaderboardTableView.Init();

                RectMask2D viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<RectMask2D>().First(), _leaderboardTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _leaderboardTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_leaderboardTableView.transform as RectTransform).anchorMin = new Vector2(0.2f, 0.5f);
                (_leaderboardTableView.transform as RectTransform).anchorMax = new Vector2(0.8f, 0.5f);
                (_leaderboardTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_leaderboardTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                _timerText = BeatSaberUI.CreateText(rectTransform, "", new Vector2(0f, 34f));
                _timerText.fontSize = 8f;
                _timerText.alignment = TextAlignmentOptions.Center;
                _timerText.rectTransform.sizeDelta = new Vector2(30f, 6f);
            }

        }

        public void SetLeaderboard(PlayerInfo[] _playerInfos)
        {
            this._playerInfos = _playerInfos.Where(x => x.playerState == PlayerState.Game || (x.playerState == PlayerState.Room && x.playerScore > 0)).OrderByDescending(x => x.playerScore).ToArray();

            if (_leaderboardTableView != null)
            {

                if (_leaderboardTableView.dataSource != this)
                {
                    _leaderboardTableView.dataSource = this;
                }
                else
                {
                    _leaderboardTableView.ReloadData();
                }
            }
        }

        public void SetSong(LevelSO song)
        {
            if (_songTableCell == null)
                return;

            _songTableCell.coverImage = song.coverImage;
            _songTableCell.songName = $"{song.songName}\n<size=80%>{song.songSubName}</size>";
            _songTableCell.author = song.songAuthorName;

            _selectedSong = song;
        }

        public void SetTimer(float time, bool results)
        {
            if (_timerText == null)
                return;

            _timerText.text = results ? $"RESULTS {(int)time}" : SecondsToString(time);
        }

        public string SecondsToString(float time)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time - minutes * 60);
            return minutes.ToString() + ":" + string.Format("{0:00}", seconds);
        }

        public TableCell CellForRow(int row)
        {
            LeaderboardTableCell cell = Instantiate(_leaderboardTableCellInstance);

            cell.reuseIdentifier = "ResultsCell";

            cell.playerName = _playerInfos[row].playerName;
            cell.score = (int)_playerInfos[row].playerScore;
            cell.rank = row + 1;
            cell.showFullCombo = false;

            return cell;
        }

        public int NumberOfRows()
        {
            return Math.Min(_playerInfos.Length, 10);
        }

        public float RowHeight()
        {
            return 7f;
        }
    }
}
