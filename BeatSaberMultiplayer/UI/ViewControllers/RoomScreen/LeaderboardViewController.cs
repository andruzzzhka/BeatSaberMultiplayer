using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using CustomUI.BeatSaber;
using HMUI;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
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
        private Button _pageUpButton;
        private Button _pageDownButton;

        LevelListTableCell _songTableCell;

        List<PlayerInfo> _playerInfos = new List<PlayerInfo>();
        List<LeaderboardTableCell> _tableCells = new List<LeaderboardTableCell>();

        public LevelSO _selectedSong;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _songTableCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")));
                (_songTableCell.transform as RectTransform).anchoredPosition = new Vector2(18f, 39f);

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -12f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _leaderboardTableView.PageScrollUp();

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
                    _leaderboardTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                _leaderboardTableCellInstance = Resources.FindObjectsOfTypeAll<LeaderboardTableCell>().First(x => (x.name == "LeaderboardTableCell"));

                RectTransform container = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.anchorMin = new Vector2(0.15f, 0.5f);
                container.anchorMax = new Vector2(0.85f, 0.5f);
                container.sizeDelta = new Vector2(0f, 56f);
                container.anchoredPosition = new Vector2(0f, -3f);

                _leaderboardTableView = new GameObject("CustomTableView").AddComponent<TableView>();
                _leaderboardTableView.gameObject.AddComponent<RectMask2D>();
                _leaderboardTableView.transform.SetParent(container, false);

                _leaderboardTableView.SetPrivateField("_isInitialized", false);
                _leaderboardTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _leaderboardTableView.Init();

                RectMask2D viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<RectMask2D>().First(), _leaderboardTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _leaderboardTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_leaderboardTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (_leaderboardTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_leaderboardTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (_leaderboardTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                ReflectionUtil.SetPrivateField(_leaderboardTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_leaderboardTableView, "_pageDownButton", _pageDownButton);

                _leaderboardTableView.dataSource = this;

                _timerText = BeatSaberUI.CreateText(rectTransform, "", new Vector2(0f, 34f));
                _timerText.fontSize = 8f;
                _timerText.alignment = TextAlignmentOptions.Center;
                _timerText.rectTransform.sizeDelta = new Vector2(30f, 6f);
            }

        }

        public void SetLeaderboard(List<PlayerInfo> players)
        {
            int prevCount = _playerInfos.Count;
            _playerInfos.Clear();
            if (players != null)
            {
                _playerInfos.AddRange(players.Where(x => x.playerState == PlayerState.Game || (x.playerState == PlayerState.Room && x.playerScore > 0)).OrderByDescending(x => x.playerScore));
            }
            
            if (prevCount != _playerInfos.Count)
            {
                for (int i = 0; i < _tableCells.Count; i++)
                {
                    Destroy(_tableCells[i].gameObject);
                }
                _tableCells.Clear();
                _leaderboardTableView.RefreshTable(false);
                if (prevCount == 0 && _playerInfos.Count > 0)
                {
                    StartCoroutine(ScrollWithDelay());
                }
            }
            else
            {
                for (int i = 0; i < _playerInfos.Count; i++)
                {
                    if (_tableCells.Count > i)
                    {
                        _tableCells[i].playerName = _playerInfos[i].playerName;
                        _tableCells[i].score = (int)_playerInfos[i].playerScore;
                        _tableCells[i].rank = i + 1;
                        _tableCells[i].showFullCombo = false;
                    }
                }
            }
        }

        IEnumerator ScrollWithDelay()
        {
            yield return null;
            yield return null;

            _leaderboardTableView.ScrollToRow(0, false);
        }

        public void SetSong(SongInfo info)
        {
            if (_songTableCell == null)
                return;
            
            _selectedSong = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(info.levelId));

            if (_selectedSong != null)
            {
                _songTableCell.songName = _selectedSong.songName + "\n<size=80%>" + _selectedSong.songSubName + "</size>";
                _songTableCell.author = _selectedSong.songAuthorName;
                _songTableCell.coverImage = _selectedSong.coverImage;
            }
            else
            {
                _songTableCell.songName = info.songName;
                _songTableCell.author = "Loading info...";
                SongDownloader.Instance.RequestSongByLevelID(info.levelId, (song) =>
                {
                    _songTableCell.songName = $"{song.songName}\n<size=80%>{song.songSubName}</size>";
                    _songTableCell.author = song.authorName;
                    StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverUrl, (cover) => { _songTableCell.coverImage = cover; }));

                });
            }
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

            _tableCells.Add(cell);
            return cell;
        }

        public int NumberOfRows()
        {
            return _playerInfos.Count;
        }

        public float RowHeight()
        {
            return 7f;
        }
    }
}
