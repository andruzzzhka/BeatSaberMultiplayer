using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using CustomUI.BeatSaber;
using HMUI;
using Polyglot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class DifficultySelectionViewController : ViewController
    {
        public event Action discardPressed;
        public event Action levelOptionsChanged;
        public event Action<IBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty> playPressed;

        public BeatmapCharacteristicSO selectedCharacteristic { get; private set; }
        public BeatmapDifficulty selectedDifficulty { get; private set; }

        LevelListTableCell _selectedSongCell;

        TextMeshProUGUI _timeParamText;
        TextMeshProUGUI _bpmParamText;
        TextMeshProUGUI _blocksParamText;
        TextMeshProUGUI _obstaclesParamText;
        TextMeshProUGUI _starsParamText;
        TextMeshProUGUI _ratingParamText;

        TextMeshProUGUI _rankedText;

        IBeatmapLevel _selectedSong;
        BeatmapCharacteristicSO[] _beatmapCharacteristics;

        TextSegmentedControl _characteristicControl;
        TextSegmentedControl _difficultyControl;

        Button _cancelButton;
        Button _playButton;
        UnityEngine.UI.Image _playBtnGlow;

        TextMeshProUGUI _playersReadyText;
                
        private RectTransform _progressBarRect;
        private UnityEngine.UI.Image _progressBackground;
        private UnityEngine.UI.Image _progressBarImage;
        private TextMeshProUGUI _progressText;
        private GameObject _loadingSpinner;

        private RectTransform _characteristicControlBlocker;
        private RectTransform _difficultyControlBlocker;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            try
            {
                if (firstActivation && activationType == ActivationType.AddedToHierarchy)
                {
                    _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();

                    bool isHost = Client.Instance.isHost;

                    _selectedSongCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")), rectTransform, false);
                    (_selectedSongCell.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
                    (_selectedSongCell.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
                    (_selectedSongCell.transform as RectTransform).anchoredPosition = new Vector2(-25f, 17.5f);
                    //_selectedSongCell.SetPrivateField("_beatmapCharacteristicAlphas", new float[0]);
                    _selectedSongCell.SetPrivateField("_beatmapCharacteristicImages", new UnityEngine.UI.Image[0]);
                    _selectedSongCell.SetPrivateField("_bought", true);
                    foreach (var icon in _selectedSongCell.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name.StartsWith("LevelTypeIcon")))
                    {
                        Destroy(icon.gameObject);
                    }

                    _rankedText = this.CreateText("LOADING...", new Vector2(-1f, -25.5f));
                    _rankedText.alignment = TextAlignmentOptions.Center;

                    Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();

                    _timeParamText = CustomExtensions.CreateLevelParam(rectTransform, new Vector2(62f, 42.5f), "--", sprites.FirstOrDefault(x => x.name == "ClockIcon"), "");
                    _bpmParamText = CustomExtensions.CreateLevelParam(rectTransform, new Vector2(102f, 42.5f), "--", sprites.FirstOrDefault(x => x.name == "MetronomeIcon"), "");
                    _blocksParamText = CustomExtensions.CreateLevelParam(rectTransform, new Vector2(62f, 35f), "--", sprites.FirstOrDefault(x => x.name == "GameNoteIcon"), "");
                    _obstaclesParamText = CustomExtensions.CreateLevelParam(rectTransform, new Vector2(102f, 35f), "--", sprites.FirstOrDefault(x => x.name == "ObstacleIcon"), "");
                    _starsParamText = CustomExtensions.CreateLevelParam(rectTransform, new Vector2(62f, 27.5f), "--", Sprites.starIcon, "");
                    _ratingParamText = CustomExtensions.CreateLevelParam(rectTransform, new Vector2(102f, 27.5f), "--", Sprites.ratingIcon, "");

                    _playersReadyText = BeatSaberUI.CreateText(rectTransform, "0/0 players ready", new Vector2(0f, 5f));
                    _playersReadyText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    _playersReadyText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                    _playersReadyText.alignment = TextAlignmentOptions.Center;
                    _playersReadyText.fontSize = 5.5f;

                    _cancelButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton");
                    (_cancelButton.transform as RectTransform).anchoredPosition = new Vector2(-30f, -25f);
                    (_cancelButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                    _cancelButton.SetButtonText("CANCEL");
                    _cancelButton.ToggleWordWrapping(false);
                    _cancelButton.SetButtonTextSize(5.5f);
                    _cancelButton.onClick.AddListener(delegate () { discardPressed?.Invoke(); });
                    _cancelButton.gameObject.SetActive(isHost);

                    _playButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton");
                    (_playButton.transform as RectTransform).anchoredPosition = new Vector2(30f, -25f);
                    (_playButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                    _playButton.SetButtonText("PLAY");
                    _playButton.ToggleWordWrapping(false);
                    _playButton.SetButtonTextSize(5.5f);
                    _playButton.onClick.AddListener(delegate () { playPressed?.Invoke(_selectedSong, selectedCharacteristic, selectedDifficulty); });
                    var playGlowContainer = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "PlayButton").GetComponentsInChildren<RectTransform>().First(x => x.name == "GlowContainer"), _playButton.transform); //Let's add some glow!
                    playGlowContainer.transform.SetAsFirstSibling();
                    _playBtnGlow = playGlowContainer.GetComponentInChildren<UnityEngine.UI.Image>();
                    _playBtnGlow.color = new Color(0f, 0.7058824f, 1f, 0.7843137f);
                    _playButton.gameObject.SetActive(isHost);

                    _characteristicControl = BeatSaberUI.CreateTextSegmentedControl(rectTransform, new Vector2(0f, 34f), new Vector2(110f, 7f), _characteristicControl_didSelectCellEvent);
                    _characteristicControl.SetTexts(new string[] { "Standard", "No Arrows", "One Saber" });

                    _characteristicControlBlocker = new GameObject("CharacteristicControlBlocker", typeof(RectTransform)).GetComponent<RectTransform>(); //"If it works it's not stupid"
                    _characteristicControlBlocker.SetParent(rectTransform, false);
                    _characteristicControlBlocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0f);
                    _characteristicControlBlocker.anchorMin = new Vector2(0f, 1f);
                    _characteristicControlBlocker.anchorMax = new Vector2(1f, 1f);
                    _characteristicControlBlocker.sizeDelta = new Vector2(-30f, 7f);
                    _characteristicControlBlocker.anchoredPosition = new Vector2(0f, -6f);

                    _difficultyControl = BeatSaberUI.CreateTextSegmentedControl(rectTransform, new Vector2(0f, 24f), new Vector2(110f, 7f), _difficultyControl_didSelectCellEvent);
                    _difficultyControl.SetTexts(new string[] { "Easy", "Normal", "Hard", "Expert", "Expert+" });

                    _difficultyControlBlocker = new GameObject("DifficultyControlBlocker", typeof(RectTransform)).GetComponent<RectTransform>(); //"If it works it's not stupid"
                    _difficultyControlBlocker.SetParent(rectTransform, false);
                    _difficultyControlBlocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0f);
                    _difficultyControlBlocker.anchorMin = new Vector2(0f, 1f);
                    _difficultyControlBlocker.anchorMax = new Vector2(1f, 1f);
                    _difficultyControlBlocker.sizeDelta = new Vector2(-30f, 7f);
                    _difficultyControlBlocker.anchoredPosition = new Vector2(0f, -16f);

                    _progressBarRect = new GameObject("ProgressBar", typeof(RectTransform)).GetComponent<RectTransform>();

                    _progressBarRect.SetParent(rectTransform, false);
                    _progressBarRect.anchorMin = new Vector2(0.5f, 0.5f);
                    _progressBarRect.anchorMax = new Vector2(0.5f, 0.5f);
                    _progressBarRect.anchoredPosition = new Vector2(0f, -7.5f);
                    _progressBarRect.sizeDelta = new Vector2(46f, 5f);

                    _progressBackground = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                    _progressBackground.rectTransform.SetParent(_progressBarRect, false);
                    _progressBackground.rectTransform.anchorMin = new Vector2(0f, 0f);
                    _progressBackground.rectTransform.anchorMax = new Vector2(1f, 1f);
                    _progressBackground.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                    _progressBackground.rectTransform.sizeDelta = new Vector2(0f, 0f);

                    _progressBackground.sprite = Sprites.whitePixel;
                    _progressBackground.material = Sprites.NoGlowMat;
                    _progressBackground.color = new Color(1f, 1f, 1f, 0.075f);

                    _progressBarImage = new GameObject("ProgressImage", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                    _progressBarImage.rectTransform.SetParent(_progressBarRect, false);
                    _progressBarImage.rectTransform.anchorMin = new Vector2(0f, 0f);
                    _progressBarImage.rectTransform.anchorMax = new Vector2(1f, 1f);
                    _progressBarImage.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                    _progressBarImage.rectTransform.sizeDelta = new Vector2(0f, 0f);

                    _progressBarImage.sprite = Sprites.whitePixel;
                    _progressBarImage.material = Sprites.NoGlowMat;
                    _progressBarImage.type = UnityEngine.UI.Image.Type.Filled;
                    _progressBarImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                    _progressBarImage.fillAmount = 0.5f;

                    _progressText = BeatSaberUI.CreateText(rectTransform, "0.0%", new Vector2(55f, -10f));
                    _progressText.rectTransform.SetParent(_progressBarRect, true);

                    _loadingSpinner = this.CreateLoadingSpinner();
                    _playButton.interactable = false;
                    _progressBarRect.gameObject.SetActive(false);
                }
                _progressBarRect.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                Plugin.log.Critical($"Exception in DifficultySelectionViewController.DidActivate: {e}");
            }
        }

        private void _characteristicControl_didSelectCellEvent(int arg2)
        {
            if (_selectedSong != null)
            {
                selectedCharacteristic = _selectedSong.beatmapCharacteristics[arg2];

                if (_selectedSong.beatmapLevelData != null)
                {
                    Dictionary<IDifficultyBeatmap, string> difficulties = _selectedSong.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic == selectedCharacteristic).difficultyBeatmaps.ToDictionary(x => x, x => x.difficulty.ToString().Replace("Plus", "+"));

                    if (_selectedSong is CustomPreviewBeatmapLevel)
                    {
                        var songData = SongCore.Collections.RetrieveExtraSongData(SongCore.Collections.hashForLevelID(_selectedSong.levelID));
                        if (songData != null && songData._difficulties != null)
                        {
                            for (int i = 0; i < difficulties.Keys.Count; i++)
                            {
                                var diffKey = difficulties.Keys.ElementAt(i);

                                var difficultyLevel = songData._difficulties.FirstOrDefault(x => x._difficulty == diffKey.difficulty);
                                if (difficultyLevel != null && !string.IsNullOrEmpty(difficultyLevel._difficultyLabel))
                                {
                                    difficulties[diffKey] = difficultyLevel._difficultyLabel;
                                    Plugin.log.Debug($"Found difficulty label \"{difficulties[diffKey]}\" for difficulty {diffKey.difficulty.ToString()}");
                                }
                            }
                        }
                        else
                        {
                            Plugin.log.Warn($"Unable to retrieve extra data for song with hash \"{_selectedSong.levelID}\"!");
                        }
                    }

                    _difficultyControl.SetTexts(difficulties.Values.ToArray());

                    int closestDifficultyIndex = CustomExtensions.GetClosestDifficultyIndex(difficulties.Keys.ToArray(), selectedDifficulty);

                    _difficultyControl.SelectCellWithNumber(closestDifficultyIndex);

                    _difficultyControl_didSelectCellEvent(closestDifficultyIndex);
                }
            }
        }

        private void _difficultyControl_didSelectCellEvent(int arg2)
        {
            if (_selectedSong != null && _selectedSong.beatmapLevelData != null && _selectedSong.beatmapLevelData.difficultyBeatmapSets != null)
            {
                IDifficultyBeatmap beatmap = _selectedSong.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic == selectedCharacteristic).difficultyBeatmaps[arg2];
                selectedDifficulty = beatmap.difficulty;

                _blocksParamText.text = beatmap.beatmapData.notesCount.ToString();
                _obstaclesParamText.text = beatmap.beatmapData.obstaclesCount.ToString();

                if (ScrappedData.Downloaded)
                {
                    ScrappedSong scrappedSong = ScrappedData.Songs.FirstOrDefault(x => x.Hash == SongCore.Collections.hashForLevelID(_selectedSong.levelID));
                    if (scrappedSong != default) {
                        DifficultyStats stats = scrappedSong.Diffs.FirstOrDefault(x => x.Diff == selectedDifficulty.ToString().Replace("+", "Plus"));
                        if (stats != default)
                        {
                            _starsParamText.text = stats.Stars.ToString();
                            _rankedText.text = $"<color={(stats.Ranked == 0 ? "red" : "green")}>{(stats.Ranked == 0 ? "UNRANKED" : "RANKED")}</color>";
                        }
                        else
                        {
                            _starsParamText.text = "--";
                            _rankedText.text = $"<color=red>UNRANKED</color>";
                        }
                        long votes = scrappedSong.Upvotes - scrappedSong.Downvotes;
                        _ratingParamText.text = (votes < 0 ? votes.ToString() : $"+{votes}");
                    }
                    else
                    {
                        _starsParamText.text = "--";
                        _ratingParamText.text = "--";
                        _rankedText.text = "<color=yellow>SONG NOT FOUND</color>";
                    }
                }
                else
                {
                    _starsParamText.text = "--";
                    _ratingParamText.text = "--";
                    _rankedText.text = "<color=yellow>SONG NOT FOUND</color>";
                }

                levelOptionsChanged?.Invoke();
            }
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
            SetLoadingState(false);

            _selectedSong = null;

            _selectedSongCell.SetText(info.songName);
            _selectedSongCell.SetSubText("Loading info...");

            _playButton.SetButtonText("PLAY");
            _playBtnGlow.color = new Color(0f, 0.7058824f, 1f, 0.7843137f);

            SongDownloader.Instance.RequestSongByLevelID(info.hash, (song) =>
            {
                _selectedSongCell.SetText($"{song.songName} <size=80%>{song.songSubName}</size>");
                _selectedSongCell.SetSubText(song.songAuthorName + " <size=80%>[" + song.levelAuthorName + "]</size>");
                StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverURL, (cover) => { _selectedSongCell.SetIcon(cover); }));

            });

            _timeParamText.transform.parent.gameObject.SetActive(false);
            _bpmParamText.transform.parent.gameObject.SetActive(false);
            _blocksParamText.transform.parent.gameObject.SetActive(false);
            _obstaclesParamText.transform.parent.gameObject.SetActive(false);
            _starsParamText.transform.parent.gameObject.SetActive(false);
            _ratingParamText.transform.parent.gameObject.SetActive(false);

            _rankedText.gameObject.SetActive(false);

            _characteristicControl.SetTexts(new string[] { "None" });
            _characteristicControl.SelectCellWithNumber(0);
            _difficultyControl.SetTexts(new string[] { "None" });
            _difficultyControl.SelectCellWithNumber(0);
        }

        public void SetSelectedSong(IBeatmapLevel level)
        {
            SetLoadingState(false);

            _selectedSong = level;

            _selectedSongCell.SetText(_selectedSong.songName + " <size=80%>" + _selectedSong.songSubName + "</size>");
            _selectedSongCell.SetSubText(_selectedSong.songAuthorName + " <size=80%>[" + _selectedSong.levelAuthorName + "]</size>");

            _playButton.SetButtonText("PLAY");
            _playBtnGlow.color = new Color(0f, 0.7058824f, 1f, 0.7843137f);

            _selectedSong.GetCoverImageTexture2DAsync(new CancellationToken()).ContinueWith((tex) => {
                if (!tex.IsFaulted)
                    _selectedSongCell.SetIcon(tex.Result);
            }).ConfigureAwait(false);

            _timeParamText.transform.parent.gameObject.SetActive(true);
            _bpmParamText.transform.parent.gameObject.SetActive(true);
            _blocksParamText.transform.parent.gameObject.SetActive(true);
            _obstaclesParamText.transform.parent.gameObject.SetActive(true);
            _starsParamText.transform.parent.gameObject.SetActive(true);
            _ratingParamText.transform.parent.gameObject.SetActive(true);

            _rankedText.gameObject.SetActive(true);
            _rankedText.alignment = TextAlignmentOptions.Center;

            _timeParamText.text = level.beatmapLevelData.audioClip != null ? EssentialHelpers.MinSecDurationText(level.beatmapLevelData.audioClip.length) : "--";
            _bpmParamText.text = level.beatsPerMinute.ToString();
            _blocksParamText.text = "--";
            _obstaclesParamText.text = "--";
            _starsParamText.text = "--";
            _ratingParamText.text = "--";
            _rankedText.text = "LOADING...";

            _characteristicControl.SetTexts(_selectedSong.beatmapCharacteristics.Distinct().Select(x => Localization.Get(x.characteristicNameLocalizationKey)).ToArray());

            int standardCharacteristicIndex = Array.FindIndex(_selectedSong.beatmapCharacteristics.Distinct().ToArray(), x => x.serializedName == "Standard");
            
            _characteristicControl.SelectCellWithNumber((standardCharacteristicIndex == -1 ? 0 : standardCharacteristicIndex));
            _characteristicControl_didSelectCellEvent((standardCharacteristicIndex == -1 ? 0 : standardCharacteristicIndex));
        }

        public void SetSelectedSong(IPreviewBeatmapLevel level)
        {
            SetLoadingState(false);

            _selectedSong = null;

            _selectedSongCell.SetText(level.songName + " <size=80%>" + level.songSubName + "</size>");
            _selectedSongCell.SetSubText(level.songAuthorName + " <size=80%>[" + level.levelAuthorName + "]</size>");

            _playButton.SetButtonText("NOT BOUGHT");
            _playBtnGlow.color = Color.red;

            level.GetCoverImageTexture2DAsync(new CancellationToken()).ContinueWith((tex) => {
                if (!tex.IsFaulted)
                    _selectedSongCell.SetIcon(tex.Result);
            }).ConfigureAwait(false);

            _timeParamText.transform.parent.gameObject.SetActive(true);
            _bpmParamText.transform.parent.gameObject.SetActive(true);
            _blocksParamText.transform.parent.gameObject.SetActive(true);
            _obstaclesParamText.transform.parent.gameObject.SetActive(true);
            _starsParamText.transform.parent.gameObject.SetActive(true);
            _ratingParamText.transform.parent.gameObject.SetActive(true);

            _rankedText.gameObject.SetActive(true);
            _rankedText.alignment = TextAlignmentOptions.Center;

            _timeParamText.text = EssentialHelpers.MinSecDurationText(level.songDuration);
            _bpmParamText.text = level.beatsPerMinute.ToString();
            _blocksParamText.text = "--";
            _obstaclesParamText.text = "--";
            _starsParamText.text = "--";
            _ratingParamText.text = "--";
            _rankedText.text = "LOADING...";

            _characteristicControl.SetTexts(new string[] { "None" });
            _characteristicControl.SelectCellWithNumber(0);
            _difficultyControl.SetTexts(new string[] { "None" });
            _difficultyControl.SelectCellWithNumber(0);
        }

        public void SetSongDuration(float duration)
        {
            _timeParamText.text = EssentialHelpers.MinSecDurationText(duration);
        }

        public void SetLoadingState(bool loading)
        {
            _loadingSpinner.SetActive(loading);
            _selectedSongCell.gameObject.SetActive(!loading);
            _playersReadyText.gameObject.SetActive(!loading);
            _cancelButton.gameObject.SetActive(!loading && Client.Instance.isHost);
            _playButton.gameObject.SetActive(!loading && Client.Instance.isHost);
            _characteristicControl.gameObject.SetActive(!loading);
            _difficultyControl.gameObject.SetActive(!loading);

            _timeParamText.transform.parent.gameObject.SetActive(!loading);
            _bpmParamText.transform.parent.gameObject.SetActive(!loading);
            _blocksParamText.transform.parent.gameObject.SetActive(!loading);
            _obstaclesParamText.transform.parent.gameObject.SetActive(!loading);
            _starsParamText.transform.parent.gameObject.SetActive(!loading);
            _ratingParamText.transform.parent.gameObject.SetActive(!loading);
            _rankedText.gameObject.SetActive(!loading);
            _rankedText.alignment = TextAlignmentOptions.Center;
        }

        public void SetBeatmapCharacteristic(BeatmapCharacteristicSO characteristic)
        {
            if (_selectedSong.beatmapCharacteristics.Contains(characteristic))
            {
                int charIndex = Array.IndexOf(_selectedSong.beatmapCharacteristics, characteristic);

                _characteristicControl.SelectCellWithNumber(charIndex);
                _characteristicControl_didSelectCellEvent(charIndex);
            }
        }

        public void SetBeatmapDifficulty(BeatmapDifficulty difficulty)
        {
            if (_selectedSong != null && _selectedSong.beatmapLevelData != null)
            {
                IDifficultyBeatmap[] difficulties = _selectedSong.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic == selectedCharacteristic).difficultyBeatmaps;

                if (difficulties.Any(x => x.difficulty == difficulty))
                {
                    int diffIndex = Array.IndexOf(difficulties, difficulties.First(x => x.difficulty == difficulty));

                    _difficultyControl.SelectCellWithNumber(diffIndex);
                    _difficultyControl_didSelectCellEvent(diffIndex);
                }
            }
        }

        public void UpdateViewController(bool isHost, bool perPlayerDifficulty)
        {
            _cancelButton.gameObject.SetActive(isHost);
            _playButton.gameObject.SetActive(isHost);
            _characteristicControlBlocker.gameObject.SetActive(!isHost);
            _difficultyControlBlocker.gameObject.SetActive(!isHost && !perPlayerDifficulty);
        }
    }
}
