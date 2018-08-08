using BeatSaberMultiplayer.Misc;
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
    class DifficultySelectionViewController : VRUIViewController
    {
        public event Action DiscardPressed;
        public event Action<CustomLevel, LevelDifficulty> PlayPressed;
        public event Action<bool> ReadyPressed;

        StandardLevelListTableCell _selectedSongCell;

        CustomLevel _selectedSong;
        LevelDifficulty _selectedDifficulty;

        Button _easyButton;
        Button _normalButton;
        Button _hardButton;
        Button _expertButton;
        Button _expertPlusButton;

        Button _discardButton;
        Button _playButton;
        //Button _readyButton;

        TextMeshProUGUI _selectDifficultyText;
        //TextMeshProUGUI _playersReadyText;

        bool ready;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                bool isHost = Client.instance.isHost;

                _selectedSongCell = Instantiate(Resources.FindObjectsOfTypeAll<StandardLevelListTableCell>().First(x => (x.name == "StandardLevelListTableCell")), rectTransform, false);
                (_selectedSongCell.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
                (_selectedSongCell.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
                (_selectedSongCell.transform as RectTransform).anchoredPosition = new Vector2(-25f, 7.5f);

                _selectDifficultyText = BeatSaberUI.CreateText(rectTransform, "Select difficulty:",new Vector2(0f, -5f));
                _selectDifficultyText.alignment = TextAlignmentOptions.Center;
                _selectDifficultyText.fontSize = 7f;
                _selectDifficultyText.gameObject.SetActive(isHost);

                /*
                _playersReadyText = BeatSaberUI.CreateText(rectTransform, "0/0 players ready", new Vector2(0f, -5f));
                _playersReadyText.alignment = TextAlignmentOptions.Center;
                _playersReadyText.fontSize = 5.5f;
                */

                _discardButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_discardButton.transform as RectTransform).anchoredPosition = new Vector2(-105f, 10f);
                (_discardButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                BeatSaberUI.SetButtonText(_discardButton, "DISCARD");
                BeatSaberUI.SetButtonTextSize(_discardButton, 5.5f);
                _discardButton.onClick.AddListener(delegate () { DiscardPressed?.Invoke(); });
                _discardButton.gameObject.SetActive(isHost);

                _playButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_playButton.transform as RectTransform).anchoredPosition = new Vector2(-25f, 10f);
                (_playButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                BeatSaberUI.SetButtonText(_playButton, "PLAY");
                BeatSaberUI.SetButtonTextSize(_playButton, 5.5f);
                _playButton.onClick.AddListener(delegate () { PlayPressed?.Invoke(_selectedSong, _selectedDifficulty); });
                _playButton.gameObject.SetActive(isHost);

                /*
                _readyButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_readyButton.transform as RectTransform).anchoredPosition = new Vector2(-65f, 10f);
                (_readyButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                BeatSaberUI.SetButtonText(_readyButton, "READY");
                BeatSaberUI.SetButtonTextSize(_readyButton, 5.5f);
                _readyButton.onClick.AddListener(delegate () { ready = !ready; ReadyPressed?.Invoke(ready); UpdateButtons(); });
                _readyButton.gameObject.SetActive(!isHost);
                */

                float buttonsY = 60f;
                float buttonsStartX = -115f;
                float butonsSizeX = 24f;

                _easyButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_easyButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX, buttonsY);
                (_easyButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                BeatSaberUI.SetButtonText(_easyButton, "EASY");
                _easyButton.onClick.AddListener(delegate() { _selectedDifficulty = LevelDifficulty.Easy; UpdateButtons(); });
                _easyButton.gameObject.SetActive(isHost);

                _normalButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_normalButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + butonsSizeX, buttonsY);
                (_normalButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                BeatSaberUI.SetButtonText(_normalButton, "NORMAL");
                _normalButton.onClick.AddListener(delegate () { _selectedDifficulty = LevelDifficulty.Normal; UpdateButtons(); });
                _normalButton.gameObject.SetActive(isHost);

                _hardButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_hardButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + 2 * butonsSizeX, buttonsY);
                (_hardButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                BeatSaberUI.SetButtonText(_hardButton, "HARD");
                _hardButton.onClick.AddListener(delegate () { _selectedDifficulty = LevelDifficulty.Hard; UpdateButtons(); });
                _hardButton.gameObject.SetActive(isHost);

                _expertButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_expertButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + 3 * butonsSizeX, buttonsY);
                (_expertButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                BeatSaberUI.SetButtonText(_expertButton, "EXPERT");
                _expertButton.onClick.AddListener(delegate () { _selectedDifficulty = LevelDifficulty.Expert; UpdateButtons(); });
                _expertButton.gameObject.SetActive(isHost);

                _expertPlusButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                (_expertPlusButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + 4 * butonsSizeX, buttonsY);
                (_expertPlusButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                BeatSaberUI.SetButtonText(_expertPlusButton, "EXPERT+");
                _expertPlusButton.onClick.AddListener(delegate () { _selectedDifficulty = LevelDifficulty.ExpertPlus; UpdateButtons(); });
                _expertPlusButton.gameObject.SetActive(isHost);
            }
            _playButton.interactable = false;
        }

        protected override void LeftAndRightScreenViewControllers(out VRUIViewController leftScreenViewController, out VRUIViewController rightScreenViewController)
        {
            PluginUI.instance.roomFlowCoordinator.GetLeftAndRightScreenViewControllers(out leftScreenViewController, out rightScreenViewController);
        }

        public void SetPlayButtonInteractable(bool interactable)
        {
            _playButton.interactable = interactable;
        }

        /*
        public void SetPlayersRead(int playersReady, int playersTotal)
        {
            _playersReadyText.text = $"{playersReady}/{playersTotal} players ready";
        }
        */

        public void SetSelectedSong(CustomLevel song)
        {
            _selectedSong = song;
            _selectedDifficulty = song.difficultyBeatmaps.OrderByDescending(x => x.difficulty).First().difficulty;

            _selectedSongCell.songName = _selectedSong.songName + "\n<size=80%>" + _selectedSong.songSubName + "</size>";
            _selectedSongCell.author = _selectedSong.songAuthorName;
            _selectedSongCell.coverImage = _selectedSong.coverImage;
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            switch (_selectedDifficulty)
            {
                case LevelDifficulty.Easy:
                    {
                        _easyButton.SetButtonStrokeColor(new Color(0f, 1f, 0f, 1f));
                        _normalButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _hardButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertPlusButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                    }
                    break;
                case LevelDifficulty.Normal:
                    {
                        _easyButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _normalButton.SetButtonStrokeColor(new Color(0f, 1f, 0f, 1f));
                        _hardButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertPlusButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                    }
                    break;
                case LevelDifficulty.Hard:
                    {
                        _easyButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _normalButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _hardButton.SetButtonStrokeColor(new Color(0f, 1f, 0f, 1f));
                        _expertButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertPlusButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                    }
                    break;
                case LevelDifficulty.Expert:
                    {
                        _easyButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _normalButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _hardButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertButton.SetButtonStrokeColor(new Color(0f, 1f, 0f, 1f));
                        _expertPlusButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                    }
                    break;
                case LevelDifficulty.ExpertPlus:
                    {
                        _easyButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _normalButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _hardButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertButton.SetButtonStrokeColor(new Color(1f, 1f, 1f, 1f));
                        _expertPlusButton.SetButtonStrokeColor(new Color(0f, 1f, 0f, 1f));
                    }
                    break;
            }

            _easyButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == LevelDifficulty.Easy);
            _normalButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == LevelDifficulty.Normal);
            _hardButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == LevelDifficulty.Hard);
            _expertButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == LevelDifficulty.Expert);
            _expertPlusButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == LevelDifficulty.ExpertPlus);
            
            //BeatSaberUI.SetButtonText(_readyButton, ready ? "CANCEL" : "READY");
        }

        public void UpdateViewController(CustomLevel song)
        {
            _easyButton.gameObject.SetActive(Client.instance.isHost);
            _normalButton.gameObject.SetActive(Client.instance.isHost);
            _hardButton.gameObject.SetActive(Client.instance.isHost);
            _expertButton.gameObject.SetActive(Client.instance.isHost);
            _expertPlusButton.gameObject.SetActive(Client.instance.isHost);
            _discardButton.gameObject.SetActive(Client.instance.isHost);
            _playButton.gameObject.SetActive(Client.instance.isHost);
            _selectDifficultyText.gameObject.SetActive(Client.instance.isHost);
            //_readyButton.gameObject.SetActive(!Client.instance.isHost);
        }
    }
}
