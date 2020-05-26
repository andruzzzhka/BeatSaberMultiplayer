using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BS_Utils.Utilities;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    public class RequestsViewController : HotReloadableViewController, TableView.IDataSource
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public override string ContentFilePath => @"C:\Users\andru_000\Source\Repos\BeatSaberMultiplayer\BeatSaberMultiplayer\UI\ViewControllers\RoomScreen\RequestsViewController.bsml";

        public event Action BackPressed;
        public event Action<SongInfo> SongSelected;
        public event Action<SongInfo> RemovePressed;

        [UIComponent("song-list")]
        CustomListTableData _songsTableView;

        [UIComponent("play-btn")]
        Button _playButton;
        [UIComponent("remove-btn")]
        Button _removeButton;

        private LevelListTableCell songListTableCellInstance;
        private AdditionalContentModel _additionalContentModel;
        private BeatmapLevelsModel _beatmapLevelsModel;

        List<SongInfo> requestedSongs = new List<SongInfo>();
        SongInfo _selectedSong;
            
        private IEnumerable<IPreviewBeatmapLevel> _allBeatmaps;


        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);

            if (firstActivation)
            {
                _songsTableView.tableView.didSelectCellWithIdxEvent += SongsTableView_DidSelectRow;
                _songsTableView.tableView.dataSource = this;

                _additionalContentModel = Resources.FindObjectsOfTypeAll<AdditionalContentModel>().First(); 
                _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();
            }

            _selectedSong = null;
            _playButton.interactable = false;
            _removeButton.interactable = false;
        }

        public void SetSongs(List<SongInfo> songs)
        {
            requestedSongs = songs;

            _allBeatmaps = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels);

            _songsTableView.tableView.ReloadData();

            if (_selectedSong != null)
            {

                int index = requestedSongs.FindIndex(x => x.hash == _selectedSong.hash);

                if (index != -1)
                {
                    _songsTableView.tableView.ScrollToCellWithIdx(index, TableViewScroller.ScrollPositionType.Beginning, false);
                    _songsTableView.tableView.SelectCellWithIdx(index, false);
                }
                else
                {
                    _songsTableView.tableView.ScrollToCellWithIdx(0, TableViewScroller.ScrollPositionType.Beginning, false);
                    _selectedSong = null;
                    _playButton.interactable = false;
                    _removeButton.interactable = false;
                }
            }
            else
            {
                _songsTableView.tableView.ScrollToCellWithIdx(0, TableViewScroller.ScrollPositionType.Beginning, false);
                _playButton.interactable = false;
                _removeButton.interactable = false;
            }

            Plugin.log.Debug($"Set list of {songs.Count} songs!");
        }

        [UIAction("back-pressed")]
        public void BackButtonPressed()
        {
            BackPressed?.Invoke();
        }

        [UIAction("play-pressed")]
        public void PlayButtonPressed()
        {
            SongSelected?.Invoke(_selectedSong);
        }

        [UIAction("remove-pressed")]
        public void RemoveButtonPressed()
        {
            RemovePressed?.Invoke(_selectedSong);
        }

        private void SongsTableView_DidSelectRow(TableView arg1, int arg2)
        {
            _selectedSong = requestedSongs[arg2];
            _playButton.interactable = true;
            _removeButton.interactable = true;
        }

        public float CellSize()
        {
            return 10f;
        }

        public int NumberOfCells()
        {
            return requestedSongs.Count;
        }

        public TableCell CellForIdx(TableView tableView, int idx)
        {
            LevelListTableCell tableCell = (LevelListTableCell)tableView.DequeueReusableCellForIdentifier(_songsTableView.reuseIdentifier);
            if (!tableCell)
            {
                if (songListTableCellInstance == null)
                    songListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                tableCell = Instantiate(songListTableCellInstance);
            }

            var level = _allBeatmaps.FirstOrDefault(x => x.levelID == requestedSongs[idx].levelId);

            if (level != null)
            {
                tableCell.SetDataFromLevelAsync(level, false);
                tableCell.RefreshAvailabilityAsync(_additionalContentModel, level.levelID);
            }
            else
            {
                tableCell.GetPrivateField<TextMeshProUGUI>("_songNameText").text = string.Format("{0} <size=80%>{1}</size>", requestedSongs[idx].songName, requestedSongs[idx].songSubName);
                tableCell.GetPrivateField<TextMeshProUGUI>("_authorText").text = "Loading info...";

                var coverImage = tableCell.GetPrivateField<RawImage>("_coverRawImage");
                coverImage.texture = null;
                coverImage.color = Color.clear;

                tableCell.GetPrivateField<RawImage>("_favoritesBadgeImage").enabled = false;

                Image[] chars = tableCell.GetPrivateField<Image[]>("_beatmapCharacteristicImages");

                foreach(var img in chars)
                {
                    img.enabled = false;
                }

                SongDownloader.Instance.RequestSongByLevelID(requestedSongs[idx].hash, (info) =>
                {
                    tableCell.GetPrivateField<TextMeshProUGUI>("_songNameText").text = string.Format("{0} <size=80%>{1}</size>", info.songName, info.songSubName);
                    tableCell.GetPrivateField<TextMeshProUGUI>("_authorText").text = info.songAuthorName;

                    StartCoroutine(LoadScripts.LoadSpriteCoroutine(info.coverURL, (cover) =>
                    { 
                        coverImage.texture = cover;
                        coverImage.color = Color.white;
                    }));
                });
            }

            tableCell.reuseIdentifier = _songsTableView.reuseIdentifier;
            return tableCell;
        }
    }
}
