using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using HMUI;
using Polyglot;
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
    class DifficultySelectionViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action discardPressed;
        public event Action levelOptionsChanged;
        public event Action<IBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty> playPressed;

        public BeatmapDifficulty selectedDifficulty { get { return _selectedDifficultyBeatmap == null ? BeatmapDifficulty.Hard : _selectedDifficultyBeatmap.difficulty; } }
        public BeatmapCharacteristicSO selectedCharacteristic { get { return _selectedDifficultyBeatmap.parentDifficultyBeatmapSet?.beatmapCharacteristic; } }

        public CancellationTokenSource cancellationToken;

        [UIComponent("diff-selection-view")]
        public RectTransform diffSelectionView;
        [UIComponent("loading-indicator")]
        public RectTransform loadingIndicator;

        [UIComponent("level-details-rect")]
        public RectTransform levelDetailsRect;

        [UIComponent("players-ready-text")]
        public TextMeshProUGUI playersReadyText;

        [UIComponent("song-name-text")]
        public TextMeshProUGUI songNameText;
        [UIComponent("duration-text")]
        public TextMeshProUGUI durationText;
        [UIComponent("bpm-text")]
        public TextMeshProUGUI bpmText;
        [UIComponent("nps-text")]
        public TextMeshProUGUI npsText;
        [UIComponent("notes-count-text")]
        public TextMeshProUGUI notesCountText;
        [UIComponent("obstacles-count-text")]
        public TextMeshProUGUI obstaclesCountText;
        [UIComponent("bombs-count-text")]
        public TextMeshProUGUI bombsCountText;
        [UIComponent("stars-text")]
        public TextMeshProUGUI starsText;
        [UIComponent("rating-text")]
        public TextMeshProUGUI ratingText;

        [UIComponent("level-cover-image")]
        public RawImage levelCoverImage;

        [UIComponent("controls-rect")]
        public RectTransform controlsRect;
        [UIComponent("characteristic-control-blocker")]
        public RawImage charactertisticControlBlocker;
        [UIComponent("characteristic-control")]
        public IconSegmentedControl characteristicControl;
        [UIComponent("difficulty-control-blocker")]
        public RawImage difficultyControlBlocker;
        [UIComponent("difficulty-control")]
        public TextSegmentedControl difficultyControl;

        [UIComponent("play-button")]
        public Button playButton;
        public Glowable playButtonGlow;
        [UIComponent("cancel-button")]
        public Button cancelButton;

        [UIComponent("loading-rect")]
        public RectTransform loadingRect;

        [UIComponent("progress-bar-top")]
        public RawImage progressBarTop;
        [UIComponent("progress-bar-bg")]
        public RawImage progressBarBG;
        [UIComponent("loading-progress-text")]
        public TextMeshProUGUI loadingProgressText;

        [UIComponent("buttons-rect")]
        public RectTransform buttonsRect;

        [UIComponent("max-combo-value")]
        public TextMeshProUGUI maxComboValue;
        [UIComponent("highscore-value")]
        public TextMeshProUGUI highscoreValue;
        [UIComponent("max-rank-value")]
        public TextMeshProUGUI maxRankValue;
        [UIComponent("ranking-value")]
        public TextMeshProUGUI rankingValue;

        private IBeatmapLevel _selectedLevel;
        private List<BeatmapCharacteristicSO> _beatmapCharacteristics = new List<BeatmapCharacteristicSO>();
        private IDifficultyBeatmap _selectedDifficultyBeatmap;

        private Texture2D _defaultArtworkTexture;

        private PlayerDataModelSO _playerDataModel;

        private bool isHost;
        private bool perPlayerDifficulty;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);
        }

        [UIAction("#post-parse")]
        public void SetupViewController()
        {
            playButtonGlow = playButton.GetComponent<Glowable>();

            levelDetailsRect.gameObject.AddComponent<Mask>();

            Image maskImage = levelDetailsRect.gameObject.AddComponent<Image>();

            maskImage.material = Sprites.NoGlowMat;
            maskImage.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "RoundRectPanel");
            maskImage.type = Image.Type.Sliced;
            maskImage.color = new Color(0f, 0f, 0f, 0.25f);

            levelCoverImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            progressBarBG.color = new Color(1f, 1f, 1f, 0.2f);
            progressBarTop.color = new Color(1f, 1f, 1f, 1f);

            charactertisticControlBlocker.color = new Color(1f, 1f, 1f, 0f);
            difficultyControlBlocker.color = new Color(1f, 1f, 1f, 0f);

            cancellationToken = new CancellationTokenSource();

            _defaultArtworkTexture = Resources.FindObjectsOfTypeAll<Texture2D>().First(x => x.name == "DefaultSongArtwork");

            _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
        }

        public void SetBeatmapCharacteristic(BeatmapCharacteristicSO beatmapCharacteristicSO)
        {
            int num = _beatmapCharacteristics.IndexOf(beatmapCharacteristicSO);
            if (num != -1)
            {
                characteristicControl.SelectCellWithNumber(num);
                SetSelectedCharateristic(null, num);
            }
            else
            {
                Plugin.log.Error("Unable to set beatmap characteristic! Not found");
            }
        }

        public void SetBeatmapDifficulty(BeatmapDifficulty difficulty)
        {
            int num = _selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(selectedCharacteristic).difficultyBeatmaps.FindIndexInArray(x => x.difficulty == difficulty);
            if (num != -1)
            {
                difficultyControl.SelectCellWithNumber(num);
                SetSelectedDifficulty(null, num);
            }
            else
            {
                Plugin.log.Error("Unable to set beatmap difficulty! Not found");
            }
        }

        public void SetPlayersReady(int playersReady, int playersTotal)
        {
            if (playersReadyText != null)
                playersReadyText.text = $"{playersReady}/{playersTotal} players ready";
        }

        public void SetLoadingState(bool loading)
        {
            diffSelectionView.gameObject.SetActive(!loading);
            loadingIndicator.gameObject.SetActive(loading);
        }

        public void UpdateViewController(bool isHost, bool perPlayerDifficulty)
        {
            this.isHost = isHost;
            this.perPlayerDifficulty = perPlayerDifficulty;
            charactertisticControlBlocker.gameObject.SetActive(!isHost);
            difficultyControlBlocker.gameObject.SetActive(!isHost && !perPlayerDifficulty);
            buttonsRect.gameObject.SetActive(isHost);
        }

        public void SetSelectedSong(IPreviewBeatmapLevel selectedLevel)
        {
            loadingRect.gameObject.SetActive(false);
            buttonsRect.gameObject.SetActive(isHost);

            if (selectedLevel is IBeatmapLevel)
            {
                _selectedLevel = selectedLevel as IBeatmapLevel;
                controlsRect.gameObject.SetActive(true);
                charactertisticControlBlocker.gameObject.SetActive(!isHost);
                difficultyControlBlocker.gameObject.SetActive(!isHost && !perPlayerDifficulty);
                SetContent(_selectedLevel);
                playButtonGlow.SetGlow("#5DADE2");
                playButton.SetButtonText("PLAY");
            }
            else
            {
                _selectedLevel = null;
                controlsRect.gameObject.SetActive(false);
                SetContent(selectedLevel);
                playButtonGlow.SetGlow("#F73431");
                playButton.SetButtonText("NOT BOUGHT");
            }
        }

        public void SetSelectedSong(SongInfo song)
        {

            controlsRect.gameObject.SetActive(false);
            buttonsRect.gameObject.SetActive(false);
            loadingRect.gameObject.SetActive(true);

            songNameText.text = song.songName;

            durationText.text = "--";
            bpmText.text = "--";
            npsText.text = "--";
            notesCountText.text = "--";
            obstaclesCountText.text = "--";
            bombsCountText.text = "--";

            maxComboValue.text = "--";
            highscoreValue.text = "--";
            maxRankValue.text = "--";

            rankingValue.text = "--";
            starsText.text = "--";
            ratingText.text = "--";

            levelCoverImage.texture = _defaultArtworkTexture;

            SetLoadingState(false);

            SongDownloader.Instance.RequestSongByLevelID(song.hash, (info) =>
            {
                songNameText.text = info.songName;
                durationText.text = info.duration.MinSecDurationText();
                bpmText.text = info.bpm.ToString();

                long votes = info.upVotes - info.downVotes;
                ratingText.text = (votes < 0 ? votes.ToString() : $"+{votes}");

                StartCoroutine(LoadScripts.LoadSpriteCoroutine(info.coverURL, (cover) => { levelCoverImage.texture = cover; }));
            });
        }

        private async void SetContent(IBeatmapLevel level)
        {
            if (level.beatmapLevelData.difficultyBeatmapSets.Any(x => x.beatmapCharacteristic == _playerDataModel.playerData.lastSelectedBeatmapCharacteristic))
            {
                _selectedDifficultyBeatmap = level.GetDifficultyBeatmap(_playerDataModel.playerData.lastSelectedBeatmapCharacteristic, _playerDataModel.playerData.lastSelectedBeatmapDifficulty);
            }
            else if (level.beatmapLevelData.difficultyBeatmapSets.Length > 0)
            {
                _selectedDifficultyBeatmap = level.GetDifficultyBeatmap(level.beatmapLevelData.difficultyBeatmapSets[0].beatmapCharacteristic, _playerDataModel.playerData.lastSelectedBeatmapDifficulty);
            }
            else
                Plugin.log.Critical("Unable to set level! No beatmap characteristics found!");

            UpdateContent();

            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_selectedDifficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic);
            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmap.difficulty);

            SetControlData(_selectedLevel.beatmapLevelData.difficultyBeatmapSets, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic);

            levelCoverImage.texture = await level.GetCoverImageTexture2DAsync(cancellationToken.Token);

            SetLoadingState(false);
        }
        public void UpdateContent()
        {
            if (_selectedLevel != null)
            {
                songNameText.text = _selectedLevel.songName;
                durationText.text = _selectedLevel.beatmapLevelData.audioClip.length.MinSecDurationText();
                bpmText.text = Mathf.RoundToInt(_selectedLevel.beatsPerMinute).ToString();
                if (_selectedDifficultyBeatmap != null)
                {
                    npsText.text = (_selectedDifficultyBeatmap.beatmapData.notesCount / _selectedLevel.beatmapLevelData.audioClip.length).ToString("0.00");
                    notesCountText.text = _selectedDifficultyBeatmap.beatmapData.notesCount.ToString();
                    obstaclesCountText.text = _selectedDifficultyBeatmap.beatmapData.obstaclesCount.ToString();
                    bombsCountText.text = _selectedDifficultyBeatmap.beatmapData.bombsCount.ToString();

                    var playerLevelStats = _playerDataModel.playerData.GetPlayerLevelStatsData(_selectedDifficultyBeatmap);

                    if (playerLevelStats.validScore)
                    {
                        maxComboValue.text = playerLevelStats.maxCombo.ToString();
                        highscoreValue.text = playerLevelStats.highScore.ToString();
                        maxRankValue.text = playerLevelStats.maxRank.ToString();
                    }
                    else
                    {
                        maxComboValue.text = "--";
                        highscoreValue.text = "--";
                        maxRankValue.text = "--";
                    }

                    string levelHash = SongCore.Collections.hashForLevelID(_selectedLevel.levelID);

                    ScrappedSong scrappedSongData = ScrappedData.Songs.FirstOrDefault(x => x.Hash == levelHash);

                    if (scrappedSongData != default)
                    {
                        DifficultyStats stats = scrappedSongData.Diffs.FirstOrDefault(x => x.Diff == selectedDifficulty.ToString().Replace("+", "Plus"));

                        if (stats != default)
                        {
                            starsText.text = stats.Stars.ToString();
                            rankingValue.text = $"<color={(stats.Ranked == 0 ? "red" : "green")}>{(stats.Ranked == 0 ? "UNRANKED" : "RANKED")}</color>";
                        }
                        else
                        {
                            starsText.text = (stats.Stars > float.Epsilon) ? stats.Stars.ToString() : "--";
                            rankingValue.text = $"<color=red>UNRANKED</color>";
                        }

                        long votes = scrappedSongData.Upvotes - scrappedSongData.Downvotes;
                        ratingText.text = (votes < 0 ? votes.ToString() : $"+{votes}");

                    }
                    else
                    {
                        rankingValue.text = $"<color=yellow>NOT FOUND</color>";
                        starsText.text = "--";
                        ratingText.text = "--";
                    }
                }
                else
                {
                    npsText.text = "--";
                    notesCountText.text = "--";
                    obstaclesCountText.text = "--";
                    bombsCountText.text = "--";

                    maxComboValue.text = "--";
                    highscoreValue.text = "--";
                    maxRankValue.text = "--";

                    rankingValue.text = "--";
                    starsText.text = "--";
                    ratingText.text = "--";
                }
            }
        }

        public void SetControlData(IDifficultyBeatmapSet[] difficultyBeatmapSets, BeatmapCharacteristicSO selectedBeatmapCharacteristic)
        {
            _beatmapCharacteristics.Clear();
            List<IDifficultyBeatmapSet> list = new List<IDifficultyBeatmapSet>(difficultyBeatmapSets);
            list.Sort((IDifficultyBeatmapSet a, IDifficultyBeatmapSet b) => a.beatmapCharacteristic.sortingOrder.CompareTo(b.beatmapCharacteristic.sortingOrder));
            difficultyBeatmapSets = list.ToArray();
            IconSegmentedControl.DataItem[] array = new IconSegmentedControl.DataItem[difficultyBeatmapSets.Length];
            int num = 0;
            for (int i = 0; i < difficultyBeatmapSets.Length; i++)
            {
                BeatmapCharacteristicSO beatmapCharacteristic = difficultyBeatmapSets[i].beatmapCharacteristic;
                array[i] = new IconSegmentedControl.DataItem(beatmapCharacteristic.icon, Localization.Get(beatmapCharacteristic.descriptionLocalizationKey));
                _beatmapCharacteristics.Add(beatmapCharacteristic);
                if (beatmapCharacteristic == selectedBeatmapCharacteristic)
                {
                    num = i;
                }
            }
            characteristicControl.SetData(array);
            characteristicControl.SelectCellWithNumber(num);
            SetSelectedCharateristic(null, num);
        }

        private async void SetContent(IPreviewBeatmapLevel level)
        {
            songNameText.text = level.songName;
            durationText.text = "--";
            bpmText.text = Mathf.RoundToInt(level.beatsPerMinute).ToString();
            npsText.text = "--";
            notesCountText.text = "--";
            obstaclesCountText.text = "--";
            bombsCountText.text = "--";

            levelCoverImage.texture = await level.GetCoverImageTexture2DAsync(cancellationToken.Token);

            SetLoadingState(false);
        }

        public void SetProgressBarState(bool enabled, float progress, string message = null)
        {
            if (enabled)
            {
                buttonsRect.gameObject.SetActive(false);
                controlsRect.gameObject.SetActive(false);
                loadingRect.gameObject.SetActive(true);

                if (string.IsNullOrEmpty(message))
                    loadingProgressText.text = $"{(progress * 100).ToString("0.00")}%";
                else
                    loadingProgressText.text = message;
                progressBarTop.rectTransform.sizeDelta = new Vector2(60f * progress, 6f);
            }
            else
            {
                controlsRect.gameObject.SetActive(_selectedLevel != null);
                UpdateViewController(isHost, perPlayerDifficulty);
                loadingRect.gameObject.SetActive(false);
            }

        }

        [UIAction("characteristic-selected")]
        public void SetSelectedCharateristic(IconSegmentedControl sender, int index)
        {
            _playerDataModel.playerData.SetLastSelectedBeatmapCharacteristic(_beatmapCharacteristics[index]);

            var diffBeatmaps = _selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(_beatmapCharacteristics[index]).difficultyBeatmaps;

            int diffIndex = CustomExtensions.GetClosestDifficultyIndex(diffBeatmaps, _playerDataModel.playerData.lastSelectedBeatmapDifficulty);

            difficultyControl.SetTexts(diffBeatmaps.Select(x => x.difficulty.ToString().Replace("Plus", "+")).ToArray());
            difficultyControl.SelectCellWithNumber(diffIndex);
            SetSelectedDifficulty(null, diffIndex);
        }

        [UIAction("difficulty-selected")]
        public void SetSelectedDifficulty(TextSegmentedControl sender, int index)
        {
            _selectedDifficultyBeatmap = _selectedLevel.beatmapLevelData.GetDifficultyBeatmapSet(_playerDataModel.playerData.lastSelectedBeatmapCharacteristic).difficultyBeatmaps[index];

            _playerDataModel.playerData.SetLastSelectedBeatmapDifficulty(_selectedDifficultyBeatmap.difficulty);

            UpdateContent();

            levelOptionsChanged?.Invoke();
        }

        [UIAction("cancel-pressed")]
        public void CancelPressed()
        {
            discardPressed?.Invoke();
        }

        [UIAction("play-pressed")]
        public void PlayPressed()
        {
            playPressed?.Invoke(_selectedLevel, selectedCharacteristic, selectedDifficulty);
        }
    }
}
