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
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class SongSelectionViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action<CustomLevel> SongSelected;

        private Button _pageUpButton;
        private Button _pageDownButton;

        TextMeshProUGUI _hostIsSelectingSongText;

        TableView _songsTableView;
        StandardLevelListTableCell _songTableCellInstance;

        List<CustomLevel> availableSongs = new List<CustomLevel>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                bool isHost = Client.instance.isHost;

                _songTableCellInstance = Resources.FindObjectsOfTypeAll<StandardLevelListTableCell>().First(x => (x.name == "StandardLevelListTableCell"));

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
                _pageUpButton.gameObject.SetActive(isHost);

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
                _pageDownButton.gameObject.SetActive(isHost);

                _songsTableView = new GameObject().AddComponent<TableView>();
                _songsTableView.transform.SetParent(rectTransform, false);

                Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _songsTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _songsTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_songsTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                (_songsTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                (_songsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_songsTableView.transform as RectTransform).position = new Vector3(0f, 0f, 2.4f);
                (_songsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                ReflectionUtil.SetPrivateField(_songsTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_songsTableView, "_pageDownButton", _pageDownButton);

                _songsTableView.didSelectRowEvent += SongsTableView_DidSelectRow;
                _songsTableView.dataSource = this;
                _songsTableView.gameObject.SetActive(isHost);

                _hostIsSelectingSongText = BeatSaberUI.CreateText(rectTransform, "Host is selecting song...", new Vector2(0f, -25f));
                _hostIsSelectingSongText.fontSize = 8f;
                _hostIsSelectingSongText.alignment = TextAlignmentOptions.Center;
                _hostIsSelectingSongText.rectTransform.sizeDelta = new Vector2(120f, 6f);
                _hostIsSelectingSongText.gameObject.SetActive(!isHost);

            }
            else
            {
                _songsTableView.ReloadData();
            }

        }

        protected override void LeftAndRightScreenViewControllers(out VRUIViewController leftScreenViewController, out VRUIViewController rightScreenViewController)
        {
            PluginUI.instance.roomFlowCoordinator.GetLeftAndRightScreenViewControllers(out leftScreenViewController, out rightScreenViewController);
        }

        private void SongsTableView_DidSelectRow(TableView sender, int row)
        {
            SongSelected?.Invoke(availableSongs[row]);
        }

        public void SetSongs(List<CustomLevel> levels)
        {
            availableSongs = levels;

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
        
        public void UpdateViewController()
        {
            bool isHost = Client.instance.isHost;
            _songsTableView.gameObject.SetActive(isHost);
            _pageUpButton.gameObject.SetActive(isHost);
            _pageDownButton.gameObject.SetActive(isHost);
            _hostIsSelectingSongText.gameObject.SetActive(!isHost);
        }

        public TableCell CellForRow(int row)
        {
            StandardLevelListTableCell cell = Instantiate(_songTableCellInstance);

            CustomLevel song = availableSongs[row];

            cell.coverImage = song.coverImage;
            cell.songName = $"{song.songName}\n<size=80%>{song.songSubName}</size>";
            cell.author = song.songAuthorName;

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
