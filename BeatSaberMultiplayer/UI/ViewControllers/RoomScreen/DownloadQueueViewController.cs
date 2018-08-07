using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.UIElements;
using HMUI;
using System;
using System.Collections;
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
    class DownloadQueueViewController : VRUIViewController, TableView.IDataSource
    {

        public List<Song> _queuedSongs = new List<Song>();

        TextMeshProUGUI _titleText;

        Button _pageUpButton;
        Button _pageDownButton;
        TableView _queuedSongsTableView;
        StandardLevelListTableCell _songListTableCellInstance;


        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _songListTableCellInstance = Resources.FindObjectsOfTypeAll<StandardLevelListTableCell>().First(x => (x.name == "StandardLevelListTableCell"));

                _titleText = BeatSaberUI.CreateText(rectTransform, "DOWNLOAD QUEUE", new Vector2(0f, -3f));
                _titleText.alignment = TextAlignmentOptions.Top;
                _titleText.fontSize = 8;


                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _queuedSongsTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _queuedSongsTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                _queuedSongsTableView = new GameObject().AddComponent<TableView>();
                _queuedSongsTableView.transform.SetParent(rectTransform, false);
                Mask viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<Mask>().First(), _queuedSongsTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _queuedSongsTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);
                (_queuedSongsTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                (_queuedSongsTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                (_queuedSongsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_queuedSongsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);
                _queuedSongsTableView.selectionType = TableView.SelectionType.None;
                _queuedSongsTableView.dataSource = this;
                _queuedSongsTableView.SetPrivateField("_pageUpButton", _pageUpButton);
                _queuedSongsTableView.SetPrivateField("_pageDownButton", _pageDownButton);
            }
        }

        public void DisplayError(string error)
        {
            TextMeshProUGUI _errorText = BeatSaberUI.CreateText(rectTransform, error, new Vector2(0f, -48f));
            _errorText.fontSize = 7f;
            _errorText.alignment = TextAlignmentOptions.Center;
            Destroy(_errorText.gameObject, 2f);
        }

        public void Refresh()
        {
            int removed = _queuedSongs.RemoveAll(x => x.songQueueState != SongQueueState.Downloading && x.songQueueState != SongQueueState.Queued);

            Log.Info($"Removed {removed} songs from queue");

            _queuedSongsTableView.ReloadData();
        }

        public void EnqueueSong(Song song)
        {
            _queuedSongs.Add(song);
            song.songQueueState = SongQueueState.Queued;

            Refresh();


            StartCoroutine(DownloadSongFromQueue(song));
        }

        IEnumerator DownloadSongFromQueue(Song song)
        {
            yield return new WaitWhile(delegate () { return _queuedSongs.Count(x => x.songQueueState == SongQueueState.Downloading) > 4; });
            yield return PluginUI.instance.downloadFlowCoordinator.DownloadSongCoroutine(song);

            _queuedSongs.Remove(song);
            song.songQueueState = SongQueueState.Available;
            Refresh();
        }

        public float RowHeight()
        {
            return 10f;
        }

        public int NumberOfRows()
        {
            return _queuedSongs.Count;
        }

        public TableCell CellForRow(int row)
        {
            StandardLevelListTableCell _tableCell = Instantiate(_songListTableCellInstance);

            DownloadQueueTableCell _queueCell = _tableCell.gameObject.AddComponent<DownloadQueueTableCell>();

            _queueCell.Init(_queuedSongs[row]);

            return _queueCell;
        }
    }
}
