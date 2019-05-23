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
        public event Action playNowButtonPressed;

        private TableView _leaderboardTableView;
        private LeaderboardTableCell _leaderboardTableCellInstance;
        private TextMeshProUGUI _timerText;
        private TextMeshProUGUI _progressText;
        private Button _pageUpButton;
        private Button _pageDownButton;
        private Button _playNowButton;

        LevelListTableCell _songTableCell;

        List<PlayerInfo> _playerInfos = new List<PlayerInfo>();
        List<LeaderboardTableCell> _tableCells = new List<LeaderboardTableCell>();

        public BeatmapLevelSO _selectedSong;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _songTableCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")), rectTransform);
                (_songTableCell.transform as RectTransform).anchoredPosition = new Vector2(100f, -1.5f);
                _songTableCell.SetPrivateField("_beatmapCharacteristicAlphas", new float[0]);
                _songTableCell.SetPrivateField("_beatmapCharacteristicImages", new UnityEngine.UI.Image[0]);
                _songTableCell.SetPrivateField("_bought", true);
                foreach (var icon in _songTableCell.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name.StartsWith("LevelTypeIcon")))
                {
                    Destroy(icon.gameObject);
                }

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -12.5f);
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
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 7f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _leaderboardTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                _playNowButton = this.CreateUIButton("CancelButton", new Vector2(-39f, 34.5f), new Vector2(28f, 8.8f), () => { playNowButtonPressed?.Invoke(); }, "Play now");
                _playNowButton.ToggleWordWrapping(false);
                _progressText = BeatSaberUI.CreateText(rectTransform, "0.0%", new Vector2(8f, 32f));
                _progressText.gameObject.SetActive(false);

                _leaderboardTableCellInstance = Resources.FindObjectsOfTypeAll<LeaderboardTableCell>().First(x => (x.name == "LeaderboardTableCell"));

                RectTransform container = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.anchorMin = new Vector2(0.15f, 0.5f);
                container.anchorMax = new Vector2(0.85f, 0.5f);
                container.sizeDelta = new Vector2(0f, 56f);
                container.anchoredPosition = new Vector2(0f, -3f);

                var tableGameObject = new GameObject("CustomTableView");
                tableGameObject.SetActive(false);
                _leaderboardTableView = tableGameObject.AddComponent<TableView>();
                _leaderboardTableView.gameObject.AddComponent<RectMask2D>();
                _leaderboardTableView.transform.SetParent(container, false);

                _leaderboardTableView.SetPrivateField("_isInitialized", false);
                _leaderboardTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                tableGameObject.SetActive(true);

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

            _leaderboardTableView.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
        }

        public void SetSong(SongInfo info)
        {
            if (_songTableCell == null)
                return;
            
            _selectedSong = SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(info.levelId)) as BeatmapLevelSO;

            if (_selectedSong != null)
            {
                _songTableCell.SetText(_selectedSong.songName + " <size=80%>" + _selectedSong.songSubName + "</size>");
                _songTableCell.SetSubText(_selectedSong.songAuthorName);

                if (_selectedSong is CustomLevel)
                {
                    CustomLevel customLevel = _selectedSong as CustomLevel;
                    if (customLevel.coverImageTexture2D == CustomExtensions.songLoaderDefaultImage.texture)
                    {
                        StartCoroutine(LoadScripts.LoadSpriteCoroutine(customLevel.customSongInfo.path + "/" + customLevel.customSongInfo.coverImagePath, (sprite) => {
                            (_selectedSong as CustomLevel).SetCoverImage(sprite);
                            _songTableCell.SetIcon(sprite);
                        }));
                    }
                }

                _songTableCell.SetIcon(_selectedSong.coverImageTexture2D);
            }
            else
            {
                _songTableCell.SetText(info.songName);
                _songTableCell.SetSubText("Loading info...");
                SongDownloader.Instance.RequestSongByLevelID(info.levelId, (song) =>
                {
                    _songTableCell.SetText( $"{song.songName} <size=80%>{song.songSubName}</size>");
                    _songTableCell.SetSubText(song.authorName);
                    StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverUrl, (cover) => { _songTableCell.SetIcon(cover); }));

                });
            }
        }
        
        public void SetProgressBarState(bool enabled, float progress)
        {
            _progressText.gameObject.SetActive(enabled);
            _progressText.text = progress.ToString("P");
            _playNowButton.interactable = !enabled;
        }

        public void SetTimer(float time, bool results)
        {
            if (_timerText == null || _playNowButton == null)
                return;

            _timerText.text = results ? $"RESULTS {(int)time}" : SecondsToString(time);
            _playNowButton.interactable = (time > 15f);
        }

        public string SecondsToString(float time)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time - minutes * 60);
            return minutes.ToString() + ":" + string.Format("{0:00}", seconds);
        }

        public TableCell CellForIdx(int row)
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

        public int NumberOfCells()
        {
            return _playerInfos.Count;
        }

        public float CellSize()
        {
            return 7f;
        }
    }
}
