using BeatSaberMultiplayer.Misc;
using CustomUI.BeatSaber;
using HMUI;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
using System;
using System.Collections;
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
        LevelListTableCell _songTableCellInstance;

        public List<LevelSO> availableSongs;
        public List<LevelSO> selectedSongs { get { return (availableSongs == null || _songsTableView == null) ? null : _songsTableView.GetPrivateField<HashSet<int>>("_selectedRows").Select(x => availableSongs[x]).ToList(); } }

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _songTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                _checkAllButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                _checkAllButton.SetButtonText("Check all");
                _checkAllButton.SetButtonTextSize(3f);
                (_checkAllButton.transform as RectTransform).sizeDelta = new Vector2(30f, 6f);
                (_checkAllButton.transform as RectTransform).anchoredPosition = new Vector2(-15f, 36.25f);
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

                _uncheckAllButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                _uncheckAllButton.SetButtonText("Uncheck all");
                _uncheckAllButton.SetButtonTextSize(3f);
                (_uncheckAllButton.transform as RectTransform).sizeDelta = new Vector2(30f, 6f);
                (_uncheckAllButton.transform as RectTransform).anchoredPosition = new Vector2(15f, 36.25f);
                _uncheckAllButton.onClick.RemoveAllListeners();
                _uncheckAllButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.ClearSelection();
                });

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -12f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 6f);
                _pageUpButton.interactable = true;
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
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                _songsTableView = new GameObject().AddComponent<TableView>();
                _songsTableView.transform.SetParent(rectTransform, false);

                _songsTableView.SetPrivateField("_isInitialized", false);
                _songsTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _songsTableView.Init();

                RectMask2D viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<RectMask2D>().First(), _songsTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _songsTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_songsTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                (_songsTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                (_songsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_songsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                ReflectionUtil.SetPrivateField(_songsTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_songsTableView, "_pageDownButton", _pageDownButton);

                _songsTableView.selectionType = TableViewSelectionType.Multiple;
                _songsTableView.didSelectRowEvent += SongsTableView_DidSelectRow;
                _songsTableView.dataSource = this;
                _songsTableView.ScrollToRow(0, false);
                StartCoroutine(Scroll());
            }
            else
            {
                _songsTableView.dataSource = this;
                _songsTableView.ScrollToRow(0, false);
                StartCoroutine(Scroll());
            }

        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            PreviewPlayer.CrossfadeToDefault();
        }

        IEnumerator Scroll()
        {
            yield return null;
            yield return null;
            _songsTableView.ScrollToRow(0, false);

        }

        public bool CheckRequirements()
        {
            return selectedSongs != null && selectedSongs.Count > 0;
        }

        public void SetSongs(List<LevelSO> levels)
        {
            availableSongs = levels;

            if (_songsTableView != null)
            {
                _songsTableView.dataSource = this;

                _songsTableView.ScrollToRow(0, false);
            }
        }

        public void SelectSongs(List<string> levelIds)
        {
            _songsTableView.GetPrivateField<List<TableCell>>("_visibleCells").ForEach(x => x.selected = levelIds.Any(y => y == (availableSongs[x.idx].levelID.Substring(0, Math.Min(32, availableSongs[x.idx].levelID.Length)))));

            HashSet<int> hashSet = new HashSet<int>();

            for (int i = 0; i < availableSongs.Count; i++)
            {
                if(levelIds.Any(x => x == availableSongs[i].levelID.Substring(0, Math.Min(32, availableSongs[i].levelID.Length))))
                {
                    hashSet.Add(i);
                }

            }
            _songsTableView.SetPrivateField("_selectedRows", hashSet);
            _songsTableView.RefreshCells(true);
        }

        private void SongsTableView_DidSelectRow(TableView sender, int row)
        {
            if (availableSongs[row] is CustomLevel) SongLoader.Instance.LoadAudioClipForLevel((CustomLevel)availableSongs[row], SongLoaded);
            else SongLoaded(availableSongs[row]);
        }

        private void SongLoaded(LevelSO song) 
		{
            PreviewPlayer.CrossfadeTo(song.audioClip, song.previewStartTime, song.previewDuration, 1f);
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
