using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using CustomUI.BeatSaber;
using SongLoaderPlugin;
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
        public event Action<LevelSO, BeatmapDifficulty> PlayPressed;

        LevelListTableCell _selectedSongCell;

        LevelSO _selectedSong;
        BeatmapDifficulty _selectedDifficulty;

        Button _easyButton;
        Button _normalButton;
        Button _hardButton;
        Button _expertButton;
        Button _expertPlusButton;

        Button _cancelButton;
        Button _playButton;

        TextMeshProUGUI _selectDifficultyText;
        TextMeshProUGUI _playersReadyText;
                
        private RectTransform _progressBarRect;
        private Image _progressBackground;
        private Image _progressBarImage;
        private TextMeshProUGUI _progressText;
        private TextMeshProUGUI _nowPlayingText;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                bool isHost = Client.Instance.isHost;

                _selectedSongCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")), rectTransform, false);
                (_selectedSongCell.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
                (_selectedSongCell.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
                (_selectedSongCell.transform as RectTransform).anchoredPosition = new Vector2(-25f, 7.5f);

                _selectDifficultyText = BeatSaberUI.CreateText(rectTransform, "Select difficulty:",new Vector2(0f, 20f));
                _selectDifficultyText.alignment = TextAlignmentOptions.Center;
                _selectDifficultyText.fontSize = 7f;
                _selectDifficultyText.gameObject.SetActive(isHost);

                _playersReadyText = BeatSaberUI.CreateText(rectTransform, "0/0 players ready", new Vector2(0f, 5f));
                _playersReadyText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                _playersReadyText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                _playersReadyText.alignment = TextAlignmentOptions.Center;
                _playersReadyText.fontSize = 5.5f;

                _cancelButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_cancelButton.transform as RectTransform).anchoredPosition = new Vector2(-30f, -25f);
                (_cancelButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                _cancelButton.SetButtonText("CANCEL");
                _cancelButton.ToggleWordWrapping(false);
                _cancelButton.SetButtonTextSize(5.5f);
                _cancelButton.onClick.AddListener(delegate () { DiscardPressed?.Invoke(); });
                _cancelButton.gameObject.SetActive(isHost);

                _playButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_playButton.transform as RectTransform).anchoredPosition = new Vector2(30f, -25f);
                (_playButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                _playButton.SetButtonText("PLAY");
                _playButton.ToggleWordWrapping(false);
                _playButton.SetButtonTextSize(5.5f);
                _playButton.onClick.AddListener(delegate () { PlayPressed?.Invoke(_selectedSong, _selectedDifficulty); });
                _playButton.gameObject.SetActive(isHost);
                
                float buttonsY = 30f;
                float buttonsStartX = -47.5f;
                float butonsSizeX = 24f;

                _easyButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_easyButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX, buttonsY);
                (_easyButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                _easyButton.SetButtonText("EASY");
                _easyButton.ToggleWordWrapping(false);
                _easyButton.onClick.AddListener(delegate() { _selectedDifficulty = BeatmapDifficulty.Easy; UpdateButtons(); });
                _easyButton.gameObject.SetActive(isHost);

                _normalButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_normalButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + butonsSizeX, buttonsY);
                (_normalButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                _normalButton.SetButtonText("NORMAL");
                _normalButton.ToggleWordWrapping(false);
                _normalButton.onClick.AddListener(delegate () { _selectedDifficulty = BeatmapDifficulty.Normal; UpdateButtons(); });
                _normalButton.gameObject.SetActive(isHost);

                _hardButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_hardButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + 2 * butonsSizeX, buttonsY);
                (_hardButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                _hardButton.SetButtonText("HARD");
                _hardButton.ToggleWordWrapping(false);
                _hardButton.onClick.AddListener(delegate () { _selectedDifficulty = BeatmapDifficulty.Hard; UpdateButtons(); });
                _hardButton.gameObject.SetActive(isHost);

                _expertButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_expertButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + 3 * butonsSizeX, buttonsY);
                (_expertButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                _expertButton.SetButtonText("EXPERT");
                _expertButton.ToggleWordWrapping(false);
                _expertButton.onClick.AddListener(delegate () { _selectedDifficulty = BeatmapDifficulty.Expert; UpdateButtons(); });
                _expertButton.gameObject.SetActive(isHost);

                _expertPlusButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_expertPlusButton.transform as RectTransform).anchoredPosition = new Vector2(buttonsStartX + 4 * butonsSizeX, buttonsY);
                (_expertPlusButton.transform as RectTransform).sizeDelta = new Vector2(butonsSizeX - 1f, 10f);
                _expertPlusButton.SetButtonText("EXPERT+");
                _expertPlusButton.ToggleWordWrapping(false);
                _expertPlusButton.onClick.AddListener(delegate () { _selectedDifficulty = BeatmapDifficulty.ExpertPlus; UpdateButtons(); });
                _expertPlusButton.gameObject.SetActive(isHost);


                _progressBarRect = new GameObject("ProgressBar", typeof(RectTransform)).GetComponent<RectTransform>();

                _progressBarRect.SetParent(rectTransform, false);
                _progressBarRect.anchorMin = new Vector2(0.5f, 0.5f);
                _progressBarRect.anchorMax = new Vector2(0.5f, 0.5f);
                _progressBarRect.anchoredPosition = new Vector2(0f, -7.5f);
                _progressBarRect.sizeDelta = new Vector2(46f, 5f);

                _progressBackground = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _progressBackground.rectTransform.SetParent(_progressBarRect, false);
                _progressBackground.rectTransform.anchorMin = new Vector2(0f, 0f);
                _progressBackground.rectTransform.anchorMax = new Vector2(1f, 1f);
                _progressBackground.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                _progressBackground.rectTransform.sizeDelta = new Vector2(0f, 0f);

                _progressBackground.sprite = Sprites.whitePixel;
                _progressBackground.material = Sprites.NoGlowMat;
                _progressBackground.color = new Color(1f, 1f, 1f, 0.075f);

                _progressBarImage = new GameObject("ProgressImage", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                _progressBarImage.rectTransform.SetParent(_progressBarRect, false);
                _progressBarImage.rectTransform.anchorMin = new Vector2(0f, 0f);
                _progressBarImage.rectTransform.anchorMax = new Vector2(1f, 1f);
                _progressBarImage.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                _progressBarImage.rectTransform.sizeDelta = new Vector2(0f, 0f);

                _progressBarImage.sprite = Sprites.whitePixel;
                _progressBarImage.material = Sprites.NoGlowMat;
                _progressBarImage.type = Image.Type.Filled;
                _progressBarImage.fillMethod = Image.FillMethod.Horizontal;
                _progressBarImage.fillAmount = 0.5f;

                _progressText = BeatSaberUI.CreateText(rectTransform, "0.0%", new Vector2(55f, -10f));
                _progressText.rectTransform.SetParent(_progressBarRect, true);
            }
            _playButton.interactable = false;
            _progressBarRect.gameObject.SetActive(false);
        }

        public void SetPlayButtonInteractable(bool interactable)
        {
            _playButton.interactable = interactable;
        }
        
        public void SetPlayersReady(int playersReady, int playersTotal)
        {
            _playersReadyText.text = $"{playersReady} of {playersTotal} players downloaded song";
        }

        public void SetProgressBarState(bool enabled, float progress)
        {
            _progressBarRect.gameObject.SetActive(enabled);
            _progressBarImage.fillAmount = progress;
            _progressText.text = progress.ToString("P");
        }

        public void SetSelectedSong(SongInfo info)
        {
            _selectedSong = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(info.levelId));

            if (_selectedSong != null)
            {
                _selectedDifficulty = _selectedSong.difficultyBeatmaps.OrderByDescending(x => x.difficulty).First().difficulty;

                _selectedSongCell.songName = _selectedSong.songName + "\n<size=80%>" + _selectedSong.songSubName + "</size>";
                _selectedSongCell.author = _selectedSong.songAuthorName;
                _selectedSongCell.coverImage = _selectedSong.coverImage;
            }
            else
            {
                _selectedSongCell.songName = info.songName;
                _selectedSongCell.author = "Loading info...";
                SongDownloader.Instance.RequestSongByLevelID(info.levelId, (song) =>
                {
                    _selectedSongCell.songName = $"{song.songName}\n<size=80%>{song.songSubName}</size>";
                    _selectedSongCell.author = song.authorName;
                    StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverUrl, (cover) => { _selectedSongCell.coverImage = cover; }));

                });
            }
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            if (_selectedSong != null)
            {
                _easyButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == BeatmapDifficulty.Easy);
                _normalButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == BeatmapDifficulty.Normal);
                _hardButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == BeatmapDifficulty.Hard);
                _expertButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == BeatmapDifficulty.Expert);
                _expertPlusButton.interactable = _selectedSong.difficultyBeatmaps.Any(x => x.difficulty == BeatmapDifficulty.ExpertPlus);

                switch (_selectedDifficulty)
                {
                    case BeatmapDifficulty.Easy:
                        {
                            _easyButton.SetButtonStrokeColor(_easyButton.interactable ? new Color(0f, 1f, 0f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _normalButton.SetButtonStrokeColor(_normalButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _hardButton.SetButtonStrokeColor(_hardButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertButton.SetButtonStrokeColor(_expertButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertPlusButton.SetButtonStrokeColor(_expertPlusButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                        }
                        break;
                    case BeatmapDifficulty.Normal:
                        {
                            _easyButton.SetButtonStrokeColor(_easyButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _normalButton.SetButtonStrokeColor(_normalButton.interactable ? new Color(0f, 1f, 0f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _hardButton.SetButtonStrokeColor(_hardButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertButton.SetButtonStrokeColor(_expertButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertPlusButton.SetButtonStrokeColor(_expertPlusButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                        }
                        break;
                    case BeatmapDifficulty.Hard:
                        {
                            _easyButton.SetButtonStrokeColor(_easyButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _normalButton.SetButtonStrokeColor(_normalButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _hardButton.SetButtonStrokeColor(_hardButton.interactable ? new Color(0f, 1f, 0f, 0f) : new Color(0f, 0f, 0f, 1f));
                            _expertButton.SetButtonStrokeColor(_expertButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertPlusButton.SetButtonStrokeColor(_expertPlusButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                        }
                        break;
                    case BeatmapDifficulty.Expert:
                        {
                            _easyButton.SetButtonStrokeColor(_easyButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _normalButton.SetButtonStrokeColor(_normalButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _hardButton.SetButtonStrokeColor(_hardButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertButton.SetButtonStrokeColor(_expertButton.interactable ? new Color(0f, 1f, 0f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertPlusButton.SetButtonStrokeColor(_expertPlusButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                        }
                        break;
                    case BeatmapDifficulty.ExpertPlus:
                        {
                            _easyButton.SetButtonStrokeColor(_easyButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _normalButton.SetButtonStrokeColor(_normalButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _hardButton.SetButtonStrokeColor(_hardButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertButton.SetButtonStrokeColor(_expertButton.interactable ? new Color(1f, 1f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
                            _expertPlusButton.SetButtonStrokeColor(_expertPlusButton.interactable ? new Color(0f, 1f, 0f, 1f) : new Color(0f, 0f, 0f, 1f));
                        }
                        break;
                }
            }
        }

        public void UpdateViewController(bool isHost)
        {
            _easyButton.gameObject.SetActive(isHost);
            _normalButton.gameObject.SetActive(isHost);
            _hardButton.gameObject.SetActive(isHost);
            _expertButton.gameObject.SetActive(isHost);
            _expertPlusButton.gameObject.SetActive(isHost);
            _cancelButton.gameObject.SetActive(isHost);
            _playButton.gameObject.SetActive(isHost);
            _selectDifficultyText.gameObject.SetActive(isHost);
        }
    }
}
