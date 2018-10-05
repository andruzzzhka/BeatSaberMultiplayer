using BeatSaberMultiplayer.Misc;
using HMUI;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen
{
    class LeftRoomCreationViewController : VRUIViewController, TableView.IDataSource
    {
        private SongPreviewPlayer _songPreviewPlayer;
        public SongPreviewPlayer PreviewPlayer {
            get {
                if (_songPreviewPlayer == null) {
                    _songPreviewPlayer = Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();
                }

                return _songPreviewPlayer;
            }
            private set { _songPreviewPlayer = value; }
        }

        private Button _pageUpButton;
        private Button _pageDownButton;
        private Button _checkAllButton;
        private Button _uncheckAllButton;

        TableView _songsTableView;
        StandardLevelListTableCell _songTableCellInstance;

        public List<IStandardLevel> availableSongs;
        public List<IStandardLevel> selectedSongs { get { return (availableSongs == null || _songsTableView == null) ? null : _songsTableView.GetPrivateField<HashSet<int>>("_selectedRows").Select(x => availableSongs[x]).ToList(); } }

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _songTableCellInstance = Resources.FindObjectsOfTypeAll<StandardLevelListTableCell>().First(x => (x.name == "StandardLevelListTableCell"));
                                
                _checkAllButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                BeatSaberUI.SetButtonText(_checkAllButton, "Check all");
                BeatSaberUI.SetButtonTextSize(_checkAllButton, 3f);
                (_checkAllButton.transform as RectTransform).sizeDelta = new Vector2(30f, 6f);
                (_checkAllButton.transform as RectTransform).anchoredPosition = new Vector2(-30f, 73f);
                _checkAllButton.onClick.RemoveAllListeners();
                _checkAllButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.GetPrivateField<List<TableCell>>("_visibleCells").ForEach(x => x.selected = true);
                    HashSet<int> hashSet = new HashSet<int>();
                    for (int i = 0; i < availableSongs.Count; i++)
                    {
                        hashSet.Add(i);
                    }
                    _songsTableView.SetPrivateField("_selectedRows", hashSet);
                    _songsTableView.RefreshCells(true);
                });
                
                _uncheckAllButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                BeatSaberUI.SetButtonText(_uncheckAllButton, "Uncheck all");
                BeatSaberUI.SetButtonTextSize(_uncheckAllButton, 3f);
                (_uncheckAllButton.transform as RectTransform).sizeDelta = new Vector2(30f, 6f);
                (_uncheckAllButton.transform as RectTransform).anchoredPosition = new Vector2(-60f, 73f);
                _uncheckAllButton.onClick.RemoveAllListeners();
                _uncheckAllButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.ClearSelection();
                });

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                _songsTableView = new GameObject().AddComponent<TableView>();
                _songsTableView.transform.SetParent(rectTransform, false);

                Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _songsTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _songsTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_songsTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                (_songsTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                (_songsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_songsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                ReflectionUtil.SetPrivateField(_songsTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_songsTableView, "_pageDownButton", _pageDownButton);

                _songsTableView.selectionType = TableView.SelectionType.Multiple;
                _songsTableView.didSelectRowEvent += SongsTableView_DidSelectRow;
                _songsTableView.dataSource = this;
                
                _songsTableView.ScrollToRow(0, false);
            }
        }

        public bool CheckRequirements()
        {
            return selectedSongs != null && selectedSongs.Count > 0;
        }

        public void SetSongs(List<IStandardLevel> levels)
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

        private void SongsTableView_DidSelectRow(TableView sender, int row)
        {
            if (availableSongs[row] is CustomLevel) SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)availableSongs[row], SongLoaded);
            else SongLoaded(availableSongs[row]);
        }

        private void SongLoaded(IStandardLevel song) 
		{
            PreviewPlayer.CrossfadeTo(song.audioClip, song.previewStartTime, song.previewDuration, 1f);
        }

        public TableCell CellForRow(int row)
        {
            StandardLevelListTableCell cell = Instantiate(_songTableCellInstance);

            IStandardLevel song = availableSongs[row];

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
