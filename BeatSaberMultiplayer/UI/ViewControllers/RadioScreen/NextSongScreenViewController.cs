using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using VRUI;
using UnityEngine.UI;
using BeatSaberMultiplayer.Misc;
using SongLoaderPlugin;
using CustomUI.BeatSaber;

namespace BeatSaberMultiplayer.UI.ViewControllers.RadioScreen
{
    class NextSongScreenViewController : VRUIViewController
    {
        public event Action skipPressedEvent;

        TextMeshProUGUI _timerText;

        TextMeshProUGUI _nextSongText;

        SongInfo _currentSongInfo;
        LevelListTableCell _currentSongCell;

        RectTransform _progressBarRect;

        Image _progressBackground;
        Image _progressBarImage;
        TextMeshProUGUI _progressText;

        Button _skipButton;
        TextMeshProUGUI _skipText;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                _timerText = BeatSaberUI.CreateText(rectTransform, "0:30", new Vector2(0f, 35f));
                _timerText.alignment = TextAlignmentOptions.Top;
                _timerText.fontSize = 8f;

                _nextSongText = BeatSaberUI.CreateText(rectTransform, "Next song:", new Vector2(0f, 15.5f));
                _nextSongText.alignment = TextAlignmentOptions.Top;
                _nextSongText.fontSize = 6f;

                _currentSongCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")), rectTransform, false);
                (_currentSongCell.transform as RectTransform).anchoredPosition = new Vector2(55f, -27f);


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

                _progressBarRect.gameObject.SetActive(false);

                _skipButton = BeatSaberUI.CreateUIButton(rectTransform, "QuitButton", new Vector2(-12.5f, -25f), new Vector2(20f, 8.8f), () => { skipPressedEvent?.Invoke(); }, "Skip");
                _skipButton.ToggleWordWrapping(false);

                _skipText = BeatSaberUI.CreateText(rectTransform, " this song", new Vector2(8.5f, -21f));
                _skipText.alignment = TextAlignmentOptions.Top;
                _skipText.fontSize = 6f;
                _skipText.rectTransform.sizeDelta = new Vector2(0f, 0f);
                _skipText.overflowMode = TextOverflowModes.Overflow;
                _skipText.enableWordWrapping = false;
            }
            _progressBarRect.gameObject.SetActive(false);
            SetSkipState(false);
        }

        public void SetSongInfo(SongInfo songInfo)
        {
            _currentSongInfo = songInfo;

            if (_currentSongCell != null)
            {
                LevelSO level = SongLoader.CustomLevelCollectionSO.levels.FirstOrDefault(x => x.levelID.StartsWith(songInfo.levelId));
                if (level == null)
                {
                    _currentSongCell.songName = _currentSongInfo.songName;
                    _currentSongCell.author = "Loading info...";
                    SongDownloader.Instance.RequestSongByLevelID(_currentSongInfo.levelId, (song) =>
                    {
                        _currentSongCell.songName = $"{song.songName}\n<size=80%>{song.songSubName}</size>";
                        _currentSongCell.author = song.authorName;
                        StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverUrl, (cover) => { _currentSongCell.coverImage = cover; }));
                    }
                    );
                }
                else
                {
                    _currentSongCell.songName = $"{level.songName}\n<size=80%>{level.songSubName}</size>";
                    _currentSongCell.author = level.songAuthorName;
                    _currentSongCell.coverImage = level.coverImage;
                }

            }
        }

        public void SetProgressBarState(bool enabled, float progress)
        {
            _progressBarRect.gameObject.SetActive(enabled);
            _progressBarImage.fillAmount = progress;
            _progressText.text = progress.ToString("P");
        }

        public void SetSkipState(bool skipped)
        {
            _skipButton.SetButtonText((skipped ? "Play" : "Skip"));
        }

        public void SetTimer(float currentTime, float totalTime)
        {
            if (_timerText != null)
            {
                _timerText.text = SecondsToString(totalTime - currentTime);
            }

        }

        public string SecondsToString(float time)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time - minutes * 60);
            return minutes.ToString() + ":" + string.Format("{0:00}", seconds);
        }
    }
}
