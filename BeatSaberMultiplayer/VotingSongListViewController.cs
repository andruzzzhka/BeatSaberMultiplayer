using HMUI;
using SongLoaderPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer
{
    class VotingSongListViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action<CustomLevelStaticData> selectedSong;

        public Button _pageUpButton;
        public Button _pageDownButton;

        public TableView _songsTableView;
        SongListTableCell _songListTableCellInstance;

        List<CustomLevelStaticData> songs;

        protected override void DidActivate()
        {
            if (_pageUpButton == null)
            {
                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.PageScrollUp();
                });
            }

            if (_pageDownButton == null)
            {
                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _songsTableView.PageScrollDown();
                });
            }

            _songListTableCellInstance = Resources.FindObjectsOfTypeAll<SongListTableCell>().First(x => (x.name == "SongListTableCell"));

            if (_songsTableView == null)
            {
                _songsTableView = new GameObject().AddComponent<TableView>();

                _songsTableView.transform.SetParent(rectTransform, false);

                Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _songsTableView.transform, false);
                viewportMask.transform.DetachChildren();

                _songsTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_songsTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0.5f);
                (_songsTableView.transform as RectTransform).anchorMax = new Vector2(1f, 0.5f);
                (_songsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_songsTableView.transform as RectTransform).position = new Vector3(0f, 0f, 2.4f);
                (_songsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                ReflectionUtil.SetPrivateField(_songsTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_songsTableView, "_pageDownButton", _pageDownButton);

                _songsTableView.DidSelectRowEvent += _songsTableView_DidSelectRowEvent;
            }
        }

        private void _songsTableView_DidSelectRowEvent(TableView sender, int row)
        {
            selectedSong.Invoke(songs[row]);
        }

        public void SetSongs(List<CustomLevelStaticData> _songs)
        {
            songs = _songs;

            if(_songsTableView.dataSource != this)
            {
                _songsTableView.dataSource = this;
            }
            else
            {
                _songsTableView.ReloadData();
            }

            _songsTableView.ScrollToRow(0, false);
        }

        public TableCell CellForRow(int row)
        {
            SongListTableCell cell = Instantiate(_songListTableCellInstance);

            cell.author = songs[row].authorName;
            cell.coverImage = songs[row].coverImage;
            cell.songName = $"{songs[row].songName}\n<size=80%>{songs[row].songSubName}</size>";

            return cell;
        }

        public int NumberOfRows()
        {
            return songs.Count;
        }

        public float RowHeight()
        {
            return 10f;
        }
    }
}
