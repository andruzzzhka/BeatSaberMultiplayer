using BeatSaberMultiplayer.Misc;
using CustomUI.BeatSaber;
using HMUI;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using VRUI;
using BeatSaberMultiplayer.UI.FlowCoordinators;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    public enum TopButtonsState { Select, SortBy, Search, Mode };

    class SongSelectionViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action<LevelSO> SongSelected;
        public event Action SearchPressed;
        public event Action<SortMode> SortPressed;
        public event Action<BeatmapCharacteristicSO> ModePressed;

        private Button _pageUpButton;
        private Button _pageDownButton;

        private Button _fastPageUpButton;
        private Button _fastPageDownButton;

        private Button _sortByButton;
        private Button _searchButton;
        private Button _modeButton;

        private Button _defButton;
        private Button _newButton;
        private Button _diffButton;
        
        private Button _stdButton;
        private Button _oneSbrButton;
        private Button _noArrButton;

        TextMeshProUGUI _hostIsSelectingSongText;

        TableView _songsTableView;
        LevelListTableCell _songTableCellInstance;
        List<LevelSO> availableSongs = new List<LevelSO>();
        private BeatmapCharacteristicSO[] _beatmapCharacteristics;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();

                _songTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                _searchButton = this.CreateUIButton("CreditsButton", new Vector2(-20f, 36.25f), new Vector2(20f, 6f), () => { SearchPressed?.Invoke(); }, "Search");
                _searchButton.SetButtonTextSize(3f);
                _searchButton.ToggleWordWrapping(false);

                _sortByButton = this.CreateUIButton("CreditsButton", new Vector2(0f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.SortBy);
                }, "Sort By");
                _sortByButton.SetButtonTextSize(3f);
                _sortByButton.ToggleWordWrapping(false);

                _modeButton = this.CreateUIButton("CreditsButton", new Vector2(20f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Mode);
                }, "Mode");
                _modeButton.SetButtonTextSize(3f);
                _modeButton.ToggleWordWrapping(false);

                _defButton = this.CreateUIButton("CreditsButton", new Vector2(-20f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    SortPressed?.Invoke(SortMode.Default);
                },
                "Default");

                _defButton.SetButtonTextSize(3f);
                _defButton.ToggleWordWrapping(false);
                _defButton.gameObject.SetActive(false);

                _newButton = this.CreateUIButton("CreditsButton", new Vector2(0f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    SortPressed?.Invoke(SortMode.Newest);
                }, "Newest");

                _newButton.SetButtonTextSize(3f);
                _newButton.ToggleWordWrapping(false);
                _newButton.gameObject.SetActive(false);


                _diffButton = this.CreateUIButton("CreditsButton", new Vector2(20f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    SortPressed?.Invoke(SortMode.Difficulty);
                }, "Difficulty");

                _diffButton.SetButtonTextSize(3f);
                _diffButton.ToggleWordWrapping(false);
                _diffButton.gameObject.SetActive(false);

                _stdButton = this.CreateUIButton("CreditsButton", new Vector2(-20f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    ModePressed?.Invoke(_beatmapCharacteristics.First(x => x.characteristicName == "Standard"));
                },
                "Standard");

                _stdButton.SetButtonTextSize(3f);
                _stdButton.ToggleWordWrapping(false);
                _stdButton.gameObject.SetActive(false);

                _oneSbrButton = this.CreateUIButton("CreditsButton", new Vector2(0f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    ModePressed?.Invoke(_beatmapCharacteristics.First(x => x.characteristicName == "One Saber"));
                }, "One Saber");

                _oneSbrButton.SetButtonTextSize(3f);
                _oneSbrButton.ToggleWordWrapping(false);
                _oneSbrButton.gameObject.SetActive(false);


                _noArrButton = this.CreateUIButton("CreditsButton", new Vector2(20f, 36.25f), new Vector2(20f, 6f), () =>
                {
                    SelectTopButtons(TopButtonsState.Select);
                    ModePressed?.Invoke(_beatmapCharacteristics.First(x => x.characteristicName == "No Arrows"));
                }, "No Arrows");

                _noArrButton.SetButtonTextSize(3f);
                _noArrButton.ToggleWordWrapping(false);
                _noArrButton.gameObject.SetActive(false);

                _fastPageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_fastPageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_fastPageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_fastPageUpButton.transform as RectTransform).anchoredPosition = new Vector2(-26f, -12f);
                (_fastPageUpButton.transform as RectTransform).sizeDelta = new Vector2(8f, 6f);
                _fastPageUpButton.GetComponentsInChildren<RectTransform>().First(x => x.name == "BG").sizeDelta = new Vector2(8f, 6f);
                _fastPageUpButton.GetComponentsInChildren<Image>().First(x => x.name == "Arrow").sprite = Sprites.doubleArrow;
                _fastPageUpButton.onClick.AddListener(delegate ()
                {
                    for (int i = 0; i < 10; i++)
                    {
                        _songsTableView.PageScrollUp();
                    }
                });

                _fastPageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_fastPageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_fastPageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_fastPageDownButton.transform as RectTransform).anchoredPosition = new Vector2(-26f, 6f);
                (_fastPageDownButton.transform as RectTransform).sizeDelta = new Vector2(8f, 6f);
                _fastPageDownButton.GetComponentsInChildren<RectTransform>().First(x => x.name == "BG").sizeDelta = new Vector2(8f, 6f);
                _fastPageDownButton.GetComponentsInChildren<Image>().First(x => x.name == "Arrow").sprite = Sprites.doubleArrow;
                _fastPageDownButton.onClick.AddListener(delegate ()
                {
                    for (int i = 0; i < 10; i++)
                    {
                        _songsTableView.PageScrollDown();
                    }
                });

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -12f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 6f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                RectTransform container = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.anchorMin = new Vector2(0.3f, 0.5f);
                container.anchorMax = new Vector2(0.7f, 0.5f);
                container.sizeDelta = new Vector2(0f, 60f);
                container.anchoredPosition = new Vector2(0f, -3f);

                _songsTableView = new GameObject("CustomTableView").AddComponent<TableView>();
                _songsTableView.gameObject.AddComponent<RectMask2D>();
                _songsTableView.transform.SetParent(container, false);

                _songsTableView.SetPrivateField("_isInitialized", false);
                _songsTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _songsTableView.Init();

                (_songsTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (_songsTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_songsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (_songsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                ReflectionUtil.SetPrivateField(_songsTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_songsTableView, "_pageDownButton", _pageDownButton);

                _songsTableView.didSelectRowEvent += SongsTableView_DidSelectRow;
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

        public void SetSongs(List<LevelSO> levels)
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

                _songsTableView.ScrollToRow(0, false);
            }
        }

        public void ScrollToLevel(string levelId)
        {
            if (availableSongs.Any(x => x.levelID == levelId))
            {
                int row = availableSongs.FindIndex(x => x.levelID == levelId);
                _songsTableView.ScrollToRow(row, false);
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
            _modeButton.gameObject.SetActive(isHost);
        }

        public void SelectTopButtons(TopButtonsState _newState)
        {
            switch (_newState)
            {
                case TopButtonsState.Select:
                    {
                        _sortByButton.gameObject.SetActive(true);
                        _searchButton.gameObject.SetActive(true);
                        _modeButton.gameObject.SetActive(true);

                        _defButton.gameObject.SetActive(false);
                        _newButton.gameObject.SetActive(false);
                        _diffButton.gameObject.SetActive(false);

                        _stdButton.gameObject.SetActive(false);
                        _oneSbrButton.gameObject.SetActive(false);
                        _noArrButton.gameObject.SetActive(false);
                    }; break;
                case TopButtonsState.SortBy:
                    {
                        _sortByButton.gameObject.SetActive(false);
                        _searchButton.gameObject.SetActive(false);
                        _modeButton.gameObject.SetActive(false);

                        _defButton.gameObject.SetActive(true);
                        _newButton.gameObject.SetActive(true);
                        _diffButton.gameObject.SetActive(true);
                        _diffButton.interactable = ScrappedData.Downloaded;

                        _stdButton.gameObject.SetActive(false);
                        _oneSbrButton.gameObject.SetActive(false);
                        _noArrButton.gameObject.SetActive(false);
                    }; break;
                case TopButtonsState.Search:
                    {
                        _sortByButton.gameObject.SetActive(false);
                        _searchButton.gameObject.SetActive(false);
                        _modeButton.gameObject.SetActive(false);

                        _defButton.gameObject.SetActive(false);
                        _newButton.gameObject.SetActive(false);
                        _diffButton.gameObject.SetActive(false);

                        _stdButton.gameObject.SetActive(false);
                        _oneSbrButton.gameObject.SetActive(false);
                        _noArrButton.gameObject.SetActive(false);
                    }; break;
                case TopButtonsState.Mode:
                    {
                        _sortByButton.gameObject.SetActive(false);
                        _searchButton.gameObject.SetActive(false);
                        _modeButton.gameObject.SetActive(false);

                        _defButton.gameObject.SetActive(false);
                        _newButton.gameObject.SetActive(false);
                        _diffButton.gameObject.SetActive(false);

                        _stdButton.gameObject.SetActive(true);
                        _oneSbrButton.gameObject.SetActive(true);
                        _noArrButton.gameObject.SetActive(true);
                    }; break;
            }
        }

        public TableCell CellForRow(int row)
        {
            LevelListTableCell cell = Instantiate(_songTableCellInstance);

            LevelSO song = availableSongs[row];

            cell.coverImage = song.coverImage;
            cell.songName = $"{song.songName}\n<size=80%>{song.songSubName}</size>";
            cell.author = song.songAuthorName;

            cell.reuseIdentifier = "SongCell";

            return cell;
        }

        public int NumberOfRows()
        {
            return availableSongs.Count;
        }

        public float RowHeight()
        {
            return 10f;
        }
    }
}
