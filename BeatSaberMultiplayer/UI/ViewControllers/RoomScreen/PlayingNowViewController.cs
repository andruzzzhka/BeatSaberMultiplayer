using BeatSaberMarkupLanguage;
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
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class PlayingNowViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action playNowPressed;

        public IBeatmapLevel selectedLevel;
        public BeatmapCharacteristicSO selectedBeatmapCharacteristic;
        public BeatmapDifficulty selectedDifficulty;
        public bool perPlayerDifficulty;

        [UIComponent("level-details-rect")]
        public RectTransform levelDetailsRect;

        [UIComponent("song-name-text")]
        public TextMeshProUGUI songNameText;
        [UIComponent("level-cover-image")]
        public RawImage levelCoverImage;
        
        [UIComponent("progress-bar-top")]
        public RawImage progressBarTop;
        [UIComponent("progress-bar-bg")]
        public RawImage progressBarBG;
        [UIComponent("loading-progress-text")]
        public TextMeshProUGUI loadingProgressText;

        [UIComponent("difficulty-control")]
        public TextSegmentedControl difficultyControl;
        [UIComponent("difficulty-control-rect")]
        public RectTransform difficultyControlRect;

        [UIComponent("leaderboard-table")]
        public LeaderboardTableView leaderboardTableView;

        [UIComponent("timer-text")]
        public TextMeshProUGUI timerText;

        [UIComponent("play-now-button")]
        public Button playNowButton;
        public Glowable playNowButtonGlow;

        private List<LeaderboardTableView.ScoreData> _scoreData = new List<LeaderboardTableView.ScoreData>();

        private Texture2D _defaultArtworkTexture;

        private bool _progressBarEnabled;

        private PlayerDataModelSO _playerDataModel;

        [UIAction("#post-parse")]
        public void SetupViewController()
        {
            playNowButtonGlow = playNowButton.GetComponent<Glowable>();

            levelDetailsRect.gameObject.AddComponent<Mask>();

            Image maskImage = levelDetailsRect.gameObject.AddComponent<Image>();

            maskImage.material = Sprites.NoGlowMat;
            maskImage.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "RoundRectPanel");
            maskImage.type = Image.Type.Sliced;
            maskImage.color = new Color(0f, 0f, 0f, 0.25f);

            levelCoverImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            progressBarBG.color = new Color(1f, 1f, 1f, 0.2f);
            progressBarTop.color = new Color(1f, 1f, 1f, 1f);

            //_beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();

            _defaultArtworkTexture = Resources.FindObjectsOfTypeAll<Texture2D>().First(x => x.name == "DefaultSongArtwork");

            _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();

            leaderboardTableView.GetComponent<TableView>().RemoveReusableCells("Cell");

            if (selectedLevel != null)
                SetSong(selectedLevel, selectedBeatmapCharacteristic, selectedDifficulty);
        }

        public void UpdateLeaderboard()
        {
            var scores = InGameOnlineController.Instance.playerScores;

            if (scores == null)
                return;

            if (scores.Count < _scoreData.Count)
            {
                _scoreData.RemoveRange(scores.Count, _scoreData.Count - scores.Count);
            }

            scores.Sort();

            for (int i = 0; i < scores.Count; i++)
            {
                if (_scoreData.Count <= i)
                {
                    _scoreData.Add(new LeaderboardTableView.ScoreData((int)scores[i].score, scores[i].name, i + 1, false));
                }
                else
                {
                    _scoreData[i].SetProperty("playerName", scores[i].name);
                    _scoreData[i].SetProperty("score", (int)scores[i].score);
                    _scoreData[i].SetProperty("rank", i + 1);
                }
            }

            leaderboardTableView.SetScores(_scoreData, -1);

        }

        public void SetSong(SongInfo info)
        {
            selectedLevel = null;

            songNameText.text = info.songName;

            levelCoverImage.texture = _defaultArtworkTexture;

            difficultyControlRect.gameObject.SetActive(false);
            playNowButton.SetButtonText("DOWNLOAD"); 
            playNowButtonGlow.SetGlow("#5DADE2");

            SongDownloader.Instance.RequestSongByLevelID(info.hash, (song) =>
            {
                songNameText.text = info.songName;

                StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverURL, (cover) => { levelCoverImage.texture = cover; }));
            });

            SetProgressBarState(false, 0f);

        }

        public async void SetSong(IBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty)
        {
            selectedLevel = level;
            selectedBeatmapCharacteristic = characteristic;
            selectedDifficulty = difficulty;

            songNameText.text = selectedLevel.songName;

            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(selectedBeatmapCharacteristic);

            var diffBeatmaps = selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(selectedBeatmapCharacteristic).difficultyBeatmaps;

            int diffIndex = CustomExtensions.GetClosestDifficultyIndex(diffBeatmaps, selectedDifficulty);

            difficultyControlRect.gameObject.SetActive(perPlayerDifficulty);
            difficultyControl.SetTexts(diffBeatmaps.Select(x => x.difficulty.ToString().Replace("Plus", "+")).ToArray());
            difficultyControl.SelectCellWithNumber(diffIndex);

            playNowButton.SetButtonText("PLAY NOW");
            playNowButtonGlow.SetGlow("#5DADE2");

            SetProgressBarState(false, 0f);

            levelCoverImage.texture = await selectedLevel.GetCoverImageTexture2DAsync(new CancellationTokenSource().Token);
        }

        public async void SetSong(IPreviewBeatmapLevel level)
        {
            selectedLevel = null;

            songNameText.text = selectedLevel.songName;

            difficultyControlRect.gameObject.SetActive(false);
            playNowButton.SetButtonText("NOT BOUGHT");
            playNowButtonGlow.SetGlow("#F73431");

            SetProgressBarState(false, 0f);

            levelCoverImage.texture = await selectedLevel.GetCoverImageTexture2DAsync(new CancellationTokenSource().Token);

        }

        public void SetProgressBarState(bool enabled, float progress)
        {
            _progressBarEnabled = enabled;

            if (enabled)
            {
                if (progress < 0f)
                {
                    loadingProgressText.text = $"An error occurred!";
                    progressBarTop.rectTransform.sizeDelta = new Vector2(0f, 6f);
                }
                else
                {
                    loadingProgressText.text = $"{(progress * 100).ToString("0.00")}%";
                    progressBarTop.rectTransform.sizeDelta = new Vector2(60f * progress, 6f);
                }
            }
        }

        public void SetTimer(float currentTime, float totalTime)
        {
            timerText.text = EssentialHelpers.MinSecDurationText((int)(totalTime - currentTime));

            if (!_progressBarEnabled)
            {
                loadingProgressText.text = $"{currentTime.MinSecDurationText()} / {totalTime.MinSecDurationText()}";
                progressBarTop.rectTransform.sizeDelta = new Vector2(60f * (currentTime / totalTime), 6f);
            }

            if(totalTime - currentTime < 15f)
            {
                playNowButton.interactable = false;
            }
        }

        [UIAction("difficulty-selected")]
        public void DifficultySelected(TextSegmentedControl sender, int index)
        {
            selectedDifficulty = selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(selectedBeatmapCharacteristic).difficultyBeatmaps[index].difficulty;

            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(selectedDifficulty);

        }

        [UIAction("play-now-pressed")]
        public void PlayNowPressed()
        {
            playNowPressed?.Invoke();
        }
    }
}
