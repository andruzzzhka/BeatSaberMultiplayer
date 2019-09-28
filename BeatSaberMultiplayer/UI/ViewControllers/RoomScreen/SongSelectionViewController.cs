using BeatSaberMultiplayer.Misc;
using CustomUI.BeatSaber;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using VRUI;
using BeatSaberMultiplayer.UI.FlowCoordinators;
using System.Threading;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    public enum TopButtonsState { Select, SortBy, Search, Mode };

    class SongSelectionViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action<IPreviewBeatmapLevel> SongSelected;
        public event Action SearchPressed;
        public event Action<SortMode> SortPressed;

        private Button _pageUpButton;
        private Button _pageDownButton;

        private Button _fastPageUpButton;
        private Button _fastPageDownButton;

        private Button _sortByButton;
        private Button _searchButton;

        private Button _defButton;
        private Button _newButton;
        private Button _diffButton;

        TextMeshProUGUI _hostIsSelectingSongText;

        TableView _songsTableView;
        LevelListTableCell _songTableCellInstance;
        List<IPreviewBeatmapLevel> availableSongs = new List<IPreviewBeatmapLevel>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _songTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                _searchButton = this.CreateUIButton("CancelButton", new Vector2(-15f, 36.5f), new Vector2(30f, 6f), () => { SearchPressed?.Invoke(); }, "Search");
                _searchButton.SetButtonTextSize(3f);
                _searchButton.ToggleWordWrapping(false);

                _sortByButton = this.CreateUIButton("CancelButton", new Vector2(15f, 36.5f), new Vector2(30f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.SortBy);
                }, "Sort By");
                _sortByButton.SetButtonTextSize(3f);
                _sortByButton.ToggleWordWrapping(false);

                _defButton = this.CreateUIButton("CancelButton", new Vector2(-20f, 36.5f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    SortPressed?.Invoke(SortMode.Default);
                },
                "Default");

                _defButton.SetButtonTextSize(3f);
                _defButton.ToggleWordWrapping(false);
                _defButton.gameObject.SetActive(false);

                _newButton = this.CreateUIButton("CancelButton", new Vector2(0f, 36.5f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    SortPressed?.Invoke(SortMode.Newest);
                }, "Newest");

                _newButton.SetButtonTextSize(3f);
                _newButton.ToggleWordWrapping(false);
                _newButton.gameObject.SetActive(false);


                _diffButton = this.CreateUIButton("CancelButton", new Vector2(20f, 36.5f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    SortPressed?.Invoke(SortMode.Difficulty);
                }, "Difficulty");

                _diffButton.SetButtonTextSize(3f);
                _diffButton.ToggleWordWrapping(false);
                _diffButton.gameObject.SetActive(false);

                _fastPageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), rectTransform, false);
                (_fastPageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_fastPageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_fastPageUpButton.transform as RectTransform).anchoredPosition = new Vector2(-26f, -6.5f);
                (_fastPageUpButton.transform as RectTransform).sizeDelta = new Vector2(8f, 6f);
                _fastPageUpButton.GetComponentsInChildren<RectTransform>().First(x => x.name == "BG").sizeDelta = new Vector2(8f, 6f);
                _fastPageUpButton.GetComponentsInChildren<Image>().First(x => x.name == "Arrow").sprite = Sprites.doubleArrow;
                _fastPageUpButton.onClick.AddListener(delegate ()
                {
                    FastScrollUp(_songsTableView, 4);
                });
                _fastPageUpButton.interactable = true;

                _fastPageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_fastPageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_fastPageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_fastPageDownButton.transform as RectTransform).anchoredPosition = new Vector2(-26f, 1f);
                (_fastPageDownButton.transform as RectTransform).sizeDelta = new Vector2(8f, 6f);
                _fastPageDownButton.GetComponentsInChildren<RectTransform>().First(x => x.name == "BG").sizeDelta = new Vector2(8f, 6f);
                _fastPageDownButton.GetComponentsInChildren<Image>().First(x => x.name == "Arrow").sprite = Sprites.doubleArrow;
                _fastPageDownButton.onClick.AddListener(delegate ()
                {
                    FastScrollDown(_songsTableView, 4);
                });
                _fastPageDownButton.interactable = true;

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -6.5f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.GetPrivateField<TableViewScroller>("_scroller").PageScrollUp();

                });
                _pageUpButton.interactable = true;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 1f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.GetPrivateField<TableViewScroller>("_scroller").PageScrollDown();

                });
                _pageDownButton.interactable = true;
                
                RectTransform container = new GameObject("Container", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.anchorMin = new Vector2(0.3f, 0.5f);
                container.anchorMax = new Vector2(0.7f, 0.5f);
                container.sizeDelta = new Vector2(0f, 60f);
                container.anchoredPosition = new Vector2(0f, -3f);

                var tableGameObject = new GameObject("CustomTableView");
                tableGameObject.SetActive(false);
                _songsTableView = tableGameObject.AddComponent<TableView>();
                _songsTableView.gameObject.AddComponent<RectMask2D>();
                _songsTableView.transform.SetParent(container, false);

                _songsTableView.SetPrivateField("_isInitialized", false);
                _songsTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);

                RectTransform viewport = new GameObject("Viewport").AddComponent<RectTransform>();
                viewport.SetParent(_songsTableView.transform, false);
                (viewport.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (viewport.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (viewport.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                _songsTableView.GetComponent<ScrollRect>().viewport = viewport;
                _songsTableView.Init();

                (_songsTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (_songsTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_songsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (_songsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                tableGameObject.SetActive(true);

                //ReflectionUtil.SetPrivateField(_songsTableView, "_pageUpButton", _pageUpButton);
                //ReflectionUtil.SetPrivateField(_songsTableView, "_pageDownButton", _pageDownButton);

                _songsTableView.didSelectCellWithIdxEvent += SongsTableView_DidSelectRow;
                _songsTableView.dataSource = this;

                _hostIsSelectingSongText = BeatSaberUI.CreateText(rectTransform, "Host is selecting song...", new Vector2(0f, 0f));
                _hostIsSelectingSongText.fontSize = 8f;
                _hostIsSelectingSongText.alignment = TextAlignmentOptions.Center;
                _hostIsSelectingSongText.rectTransform.sizeDelta = new Vector2(120f, 6f);

                SelectTopButtons(TopButtonsState.Select);
            }
            else
            {
                _songsTableView.ReloadData();
            }
        }

        private void SongsTableView_DidSelectRow(TableView sender, int row)
        {
            SongSelected?.Invoke(availableSongs[row]);
        }

        public void SetSongs(List<IPreviewBeatmapLevel> levels)
        {
            availableSongs = levels;

            if (_songsTableView != null)
            {
                if (_songsTableView.dataSource != this)
                {
                    _songsTableView.dataSource = this;
                }
                else
                {
                    _songsTableView.ReloadData();
                }

                _songsTableView.ScrollToCellWithIdx(0, TableViewScroller.ScrollPositionType.Beginning, false);
            }
        }

        public void ScrollToLevel(string levelId)
        {
            if (availableSongs.Any(x => x.levelID == levelId))
            {
                int row = availableSongs.FindIndex(x => x.levelID == levelId);
                _songsTableView.ScrollToCellWithIdx(row, TableViewScroller.ScrollPositionType.Beginning, false);
            }
        }

        public void UpdateViewController(bool isHost)
        {
            _songsTableView.gameObject.SetActive(isHost);
            _pageUpButton.gameObject.SetActive(isHost);
            _pageDownButton.gameObject.SetActive(isHost);
            _fastPageUpButton.gameObject.SetActive(isHost);
            _fastPageDownButton.gameObject.SetActive(isHost);

            _hostIsSelectingSongText.gameObject.SetActive(!isHost);
            SelectTopButtons(TopButtonsState.Select);

            _sortByButton.gameObject.SetActive(isHost);
            _searchButton.gameObject.SetActive(isHost);

            _fastPageUpButton.gameObject.SetActive(isHost);
            _fastPageDownButton.gameObject.SetActive(isHost);
        }

        public void SelectTopButtons(TopButtonsState _newState)
        {
            switch (_newState)
            {
                case TopButtonsState.Select:
                    {
                        _sortByButton.gameObject.SetActive(true);
                        _searchButton.gameObject.SetActive(true);

                        _defButton.gameObject.SetActive(false);
                        _newButton.gameObject.SetActive(false);
                        _diffButton.gameObject.SetActive(false);
                    }; break;
                case TopButtonsState.SortBy:
                    {
                        _sortByButton.gameObject.SetActive(false);
                        _searchButton.gameObject.SetActive(false);

                        _defButton.gameObject.SetActive(true);
                        _newButton.gameObject.SetActive(true);
                        _diffButton.gameObject.SetActive(true);
                        _diffButton.interactable = ScrappedData.Downloaded;
                    }; break;
                case TopButtonsState.Search:
                    {
                        _sortByButton.gameObject.SetActive(false);
                        _searchButton.gameObject.SetActive(false);

                        _defButton.gameObject.SetActive(false);
                        _newButton.gameObject.SetActive(false);
                        _diffButton.gameObject.SetActive(false);
                    }; break;
                case TopButtonsState.Mode:
                    {
                        _sortByButton.gameObject.SetActive(false);
                        _searchButton.gameObject.SetActive(false);

                        _defButton.gameObject.SetActive(false);
                        _newButton.gameObject.SetActive(false);
                        _diffButton.gameObject.SetActive(false);
                    }; break;
            }
        }

        private void FastScrollDown(TableView tableView, int pages)
        {
            TableViewScroller scroller = tableView.GetPrivateField<TableViewScroller>("_scroller");

            float maxPos = (float)tableView.numberOfCells * tableView.cellSize - tableView.scrollRectTransform.rect.height;

            float targetPosition = scroller.position + Mathf.Max(1f, scroller.GetNumberOfVisibleCells() - 1f) * tableView.cellSize * pages;

            if (targetPosition > maxPos)
            {
                targetPosition = maxPos;
            }

            scroller.SetPrivateField("_targetPosition", Mathf.Round(targetPosition / tableView.cellSize) * tableView.cellSize);

            scroller.enabled = true;
            tableView.enabled = true;

            tableView.RefreshScrollButtons();
        }

        private void FastScrollUp(TableView tableView, int pages)
        {
            TableViewScroller scroller = tableView.GetPrivateField<TableViewScroller>("_scroller");
            
            float targetPosition = scroller.position - Mathf.Max(1f, scroller.GetNumberOfVisibleCells() - 1f) * tableView.cellSize * pages;

            if (targetPosition < 0f)
            {
                targetPosition = 0f;
            }

            scroller.SetPrivateField("_targetPosition", Mathf.Round(targetPosition / tableView.cellSize) * tableView.cellSize);

            scroller.enabled = true;
            tableView.enabled = true;

            tableView.RefreshScrollButtons();
        }

        public TableCell CellForIdx(TableView sender, int row)
        {
            LevelListTableCell cell = Instantiate(_songTableCellInstance);

            IPreviewBeatmapLevel song = availableSongs[row];
            
            song.GetCoverImageTexture2DAsync(new CancellationToken()).ContinueWith((tex) => {
                if(!tex.IsFaulted)
                    cell.SetIcon(tex.Result);
            }).ConfigureAwait(false);

            cell.SetText($"{song.songName} <size=80%>{song.songSubName}</size>");
            cell.SetSubText(song.songAuthorName + " <size=80%>[" + song.levelAuthorName + "]</size>");

            cell.reuseIdentifier = "SongCell";

            cell.SetPrivateField("_beatmapCharacteristicAlphas", new float[3] { song.beatmapCharacteristics.Any(x => x.serializedName == "Standard") ? 1f : 0.1f, song.beatmapCharacteristics.Any(x => x.serializedName == "NoArrows") ? 1f : 0.1f, song.beatmapCharacteristics.Any(x => x.serializedName == "OneSaber") ? 1f : 0.1f });
            cell.SetPrivateField("_beatmapCharacteristicImages", cell.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name.StartsWith("LevelTypeIcon")).ToArray());
            cell.SetPrivateField("_bought", true);

            return cell;
        }

        public int NumberOfCells()
        {
            return availableSongs.Count;
        }

        public float CellSize()
        {
            return 10f;
        }
    }
}
