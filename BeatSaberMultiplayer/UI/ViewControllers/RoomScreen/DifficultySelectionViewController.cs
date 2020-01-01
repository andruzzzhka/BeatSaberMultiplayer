using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BS_Utils.Utilities;
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
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class DifficultySelectionViewController : BSMLResourceViewController
    {
        public override string ResourceName => "BeatSaberMultiplayer.UI.ViewControllers.RoomScreen.DifficultySelectionViewController";
        
        public event Action discardPressed;
        public event Action levelOptionsChanged;
        public event Action<IBeatmapLevel, BeatmapCharacteristicSO, BeatmapDifficulty> playPressed;
        public event Action errorOccurred;

        public BeatmapDifficulty selectedDifficulty { get; private set; }
        public BeatmapCharacteristicSO selectedCharacteristic { get; private set; }

        public CancellationTokenSource cancellationToken;

        [UIComponent("level-details")]
        public RectTransform levelDetailsRect;

        [UIComponent("players-ready-text")]
        public TextMeshProUGUI playersReadyText;

        Image _progressBarTop;
        Image _progressBarBG;
        TextMeshProUGUI _loadingProgressText;

        private GameObject _levelDetails;
        private StandardLevelDetailView _levelDetailView;
        private PlayerDataModelSO _playerDataModel;
        private BeatmapLevelsModel _beatmapLevelsModel;
        private AdditionalContentModel _additionalContentModel;

        private IBeatmapLevel _selectedLevel;

        private GameObject _playContainer;
        private GameObject _progressBarParent;

        private Button _cancelButton;
        private Button _playButton;

        private Image _segmentedControlsBlocker;

        [UIAction("#post-parse")]
        public void SetupViewController()
        {
            cancellationToken = new CancellationTokenSource();

            _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();
            _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();
            _additionalContentModel = Resources.FindObjectsOfTypeAll<AdditionalContentModel>().First();

            SetupDetailView();

            if (_selectedLevel != null)
                _levelDetailView.SetContent(_selectedLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic, _playerDataModel.playerData, true);
        }

        internal void SetupDetailView()
        {
            _levelDetails = GameObject.Instantiate(Resources.FindObjectsOfTypeAll<StandardLevelDetailView>().First(x => x.name == "LevelDetail").gameObject, levelDetailsRect);
            _levelDetails.gameObject.SetActive(false);

            var bsmlObjects = _levelDetails.GetComponentsInChildren<RectTransform>().Where(x => x.gameObject.name.StartsWith("BSML"));
            foreach (var bsmlObject in bsmlObjects)
                Destroy(bsmlObject.gameObject);

            var hoverHints = _levelDetails.GetComponentsInChildren<HoverHint>();
            var hoverHintController = Resources.FindObjectsOfTypeAll<HoverHintController>().First();
            foreach (var hint in hoverHints)
                hint.SetPrivateField("_hoverHintController", hoverHintController);

            _levelDetailView = _levelDetails.GetComponent<StandardLevelDetailView>();

            _levelDetailView.didChangeDifficultyBeatmapEvent += (sender, diffBeatmap) =>
            {
                selectedDifficulty = diffBeatmap.difficulty;
                selectedCharacteristic = diffBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic;

                levelOptionsChanged?.Invoke();
            };

            #region Fix TextSegmentedControl
            TextSegmentedControl textSegments = _levelDetails.GetComponentsInChildren<TextSegmentedControl>().First();

            TextSegmentedControlCellNew[] _textSegments = Resources.FindObjectsOfTypeAll<TextSegmentedControlCellNew>();

            textSegments.SetPrivateField("_singleCellPrefab", _textSegments.First(x => x.name == "HSingleTextSegmentedControlCell"));
            textSegments.SetPrivateField("_firstCellPrefab", _textSegments.First(x => x.name == "LeftTextSegmentedControlCell"));
            textSegments.SetPrivateField("_middleCellPrefab", _textSegments.Last(x => x.name == "HMiddleTextSegmentedControlCell"));
            textSegments.SetPrivateField("_lastCellPrefab", _textSegments.Last(x => x.name == "RightTextSegmentedControlCell"));

            textSegments.SetPrivateField("_container", Resources.FindObjectsOfTypeAll<TextSegmentedControl>().Select(x => x.GetPrivateField<object>("_container")).First(x => x != null));
            #endregion

            #region Fix IconSegmentedControl
            IconSegmentedControl iconSegments = _levelDetails.GetComponentsInChildren<IconSegmentedControl>().First();

            IconSegmentedControlCell[] _iconSegments = Resources.FindObjectsOfTypeAll<IconSegmentedControlCell>();

            iconSegments.SetPrivateField("_singleCellPrefab", _iconSegments.First(x => x.name == "SingleIconSegmentedControlCell"));
            iconSegments.SetPrivateField("_firstCellPrefab", _iconSegments.First(x => x.name == "LeftIconSegmentedControlCell"));
            iconSegments.SetPrivateField("_middleCellPrefab", _iconSegments.Last(x => x.name == "HMiddleIconSegmentedControlCell"));
            iconSegments.SetPrivateField("_lastCellPrefab", _iconSegments.Last(x => x.name == "RightIconSegmentedControlCell"));

            iconSegments.SetPrivateField("_container", Resources.FindObjectsOfTypeAll<IconSegmentedControl>().Select(x => x.GetPrivateField<object>("_container")).First(x => x != null));
            #endregion

            #region Add Cancel button
            GameObject practiceButton = _levelDetails.GetComponentsInChildren<RectTransform>().First(x => x.name == "PracticeButton").gameObject;
            GameObject playButton = _levelDetails.GetComponentsInChildren<RectTransform>().First(x => x.name == "PlayButton").gameObject;

            _playButton = playButton.GetComponent<Button>();

            Destroy(practiceButton);

            GameObject cancelButtonObj = Instantiate(playButton, playButton.transform.parent);
            Destroy(cancelButtonObj.GetComponentsInChildren<RectTransform>().First(x => x.name == "GlowContainer").gameObject);
            cancelButtonObj.transform.SetAsFirstSibling();

            _cancelButton = cancelButtonObj.GetComponent<Button>();
            BeatSaberMarkupLanguage.BeatSaberUI.SetButtonText(_cancelButton, "CANCEL");
            #endregion

            #region Add progress bar
            _playContainer = _levelDetails.GetComponentsInChildren<RectTransform>().First(x => x.name == "PlayContainer").gameObject;

            _progressBarParent = new GameObject("ProgressBar", typeof(RectTransform));
            _progressBarParent.transform.SetParent(_levelDetailView.transform, false);
            _progressBarParent.AddComponent<LayoutElement>().preferredHeight = 34f;
            VerticalLayoutGroup verticalLayoutGroup = _progressBarParent.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childControlWidth = true;

            ContentSizeFitter progressBarFitter = _progressBarParent.AddComponent<ContentSizeFitter>();
            progressBarFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            progressBarFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;


            Image bg = _progressBarParent.AddComponent<Image>();

            bg.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "RoundRectPanel");
            bg.type = Image.Type.Sliced;
            bg.material = Sprites.NoGlowMat;
            bg.color = new Color(0f, 0f, 0f, 0.25f);

            _loadingProgressText = BeatSaberMarkupLanguage.BeatSaberUI.CreateText(_progressBarParent.transform as RectTransform, "0.00%", Vector3.zero);
            _loadingProgressText.alignment = TextAlignmentOptions.Center;
            _loadingProgressText.fontSize = 6f;

            StackLayoutGroup progressBar = new GameObject("ProgressBar").AddComponent<StackLayoutGroup>();
            LayoutElement layoutElement = progressBar.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 8f;
            layoutElement.preferredWidth = 75f;
            ContentSizeFitter imagesFitter = progressBar.gameObject.AddComponent<ContentSizeFitter>();
            imagesFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            imagesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _progressBarBG = new GameObject("ProgressBar BG").AddComponent<Image>();
            _progressBarTop = new GameObject("ProgressBar Top").AddComponent<Image>();

            _progressBarBG.transform.SetParent(progressBar.transform, false);
            _progressBarTop.transform.SetParent(progressBar.transform, false);
            progressBar.transform.SetParent(_progressBarParent.transform, false);

            _progressBarTop.type = Image.Type.Filled;
            _progressBarTop.fillMethod = Image.FillMethod.Horizontal;
            _progressBarTop.fillAmount = 0.5f;

            _progressBarBG.color = new Color(0.8f, 0.8f, 0.8f, 0.2f);
            _progressBarBG.material = Sprites.NoGlowMat;
            _progressBarBG.sprite = Sprites.whitePixel;
            _progressBarTop.color = new Color(1f, 1f, 1f, 1f);
            _progressBarTop.material = Sprites.NoGlowMat;
            _progressBarTop.sprite = Sprites.whitePixel;

            _progressBarParent.gameObject.SetActive(false);
            #endregion

            #region Setup button listeners
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(() => { discardPressed?.Invoke(); });
            _playButton.onClick.RemoveAllListeners();
            _playButton.onClick.AddListener(() => { playPressed?.Invoke(_selectedLevel, selectedCharacteristic, selectedDifficulty); });
            #endregion

            _levelDetails.gameObject.SetActive(true);
        }

        internal void SetBeatmapCharacteristic(BeatmapCharacteristicSO beatmapCharacteristicSO)
        {
            BeatmapCharacteristicSegmentedControlController charController = _levelDetailView.GetPrivateField<BeatmapCharacteristicSegmentedControlController>("_beatmapCharacteristicSegmentedControlController");

            var charsList = charController.GetPrivateField<List<BeatmapCharacteristicSO>>("_beatmapCharacteristics");

            charController.HandleDifficultySegmentedControlDidSelectCell(null, charsList.IndexOf(beatmapCharacteristicSO) == -1 ? 0 : charsList.IndexOf(beatmapCharacteristicSO));
        }

        internal void SetBeatmapDifficulty(BeatmapDifficulty difficulty)
        {
            BeatmapDifficultySegmentedControlController diffController = _levelDetailView.GetPrivateField<BeatmapDifficultySegmentedControlController>("_beatmapDifficultySegmentedControlController");

            diffController.HandleDifficultySegmentedControlDidSelectCell(null, diffController.GetClosestDifficultyIndex(difficulty));
        }

        internal void SetPlayersReady(int playersReady, int playersTotal)
        {
            playersReadyText.text = $"{playersReady}/{playersTotal} players ready";
        }

        internal void SetPlayButtonInteractable(bool enabled)
        {
            _playButton.interactable = enabled;
        }

        internal void UpdateViewController(bool isHost, bool perPlayerDifficulty)
        {
            SetSegmentedControlsInteractable(isHost, isHost || perPlayerDifficulty);
        }

        internal async void SetSelectedSong(IPreviewBeatmapLevel selectedLevel)
        {
            if (selectedLevel is IBeatmapLevel)
            {
                _selectedLevel = selectedLevel as IBeatmapLevel;
                if (_levelDetailView != null)
                {
                    _levelDetailView.SetContent(selectedLevel as IBeatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic, _playerDataModel.playerData, true);
                    BeatSaberMarkupLanguage.BeatSaberUI.SetButtonText(_playButton, "PLAY");
                    SetPlayButtonGlowColor(new Color(1f, 0.706f, 0f));
                    HideSegmentedControls(false);
                }
            }
            else
            {
                var entitlementStatus = await _additionalContentModel.GetLevelEntitlementStatusAsync(selectedLevel.levelID, cancellationToken.Token);
                if (entitlementStatus == AdditionalContentModel.EntitlementStatus.Owned)
                {
                    BeatmapLevelsModel.GetBeatmapLevelResult getBeatmapLevelResult = await _beatmapLevelsModel.GetBeatmapLevelAsync(selectedLevel.levelID, cancellationToken.Token);
                    if (!getBeatmapLevelResult.isError)
                    {
                        _selectedLevel = getBeatmapLevelResult.beatmapLevel;
                        if (_levelDetailView != null)
                        {
                            _levelDetailView.SetContent(getBeatmapLevelResult.beatmapLevel, _playerDataModel.playerData.lastSelectedBeatmapDifficulty, _playerDataModel.playerData.lastSelectedBeatmapCharacteristic, _playerDataModel.playerData, true);
                            BeatSaberMarkupLanguage.BeatSaberUI.SetButtonText(_playButton, "PLAY");
                            SetPlayButtonGlowColor(new Color(1f, 0.706f, 0f));
                            HideSegmentedControls(false);
                        }
                    }
                    else
                    {
                        Plugin.log.Error("Unable to load beatmap! An error occurred");
                        errorOccurred?.Invoke();
                    }
                }else if(entitlementStatus == AdditionalContentModel.EntitlementStatus.NotOwned)
                {
                    _levelDetailView.GetPrivateField<TextMeshProUGUI>("_songNameText").text = selectedLevel.songName;
                    _levelDetailView.SetTextureAsync(selectedLevel);
                    LevelParamsPanel paramsPanel = _levelDetailView.GetPrivateField<LevelParamsPanel>("_levelParamsPanel");
                    paramsPanel.bombsCount = 0;
                    paramsPanel.bpm = 0;
                    paramsPanel.duration = 0;
                    paramsPanel.notesCount = 0;
                    paramsPanel.notesPerSecond = 0;
                    paramsPanel.obstaclesCount = 0;
                    _levelDetailView.GetPrivateField<GameObject>("_playerStatsContainer").SetActive(false);
                    _levelDetailView.GetPrivateField<GameObject>("_separator").SetActive(true);
                    BeatSaberMarkupLanguage.BeatSaberUI.SetButtonText(_playButton, "NOT BOUGHT");
                    SetPlayButtonGlowColor(Color.red);
                    HideSegmentedControls(true);
                }
                else
                {
                    Plugin.log.Error("Unable to get level entitlement status! An error occurred");
                    errorOccurred?.Invoke();
                }
            }
        }

        private void SetPlayButtonGlowColor(Color color)
        {
            _playButton.GetComponentsInChildren<Image>().First(x => x.name == "Glow").color = color;
        }

        private void HideSegmentedControls(bool hide)
        {
            _levelDetailView.GetPrivateField<BeatmapCharacteristicSegmentedControlController>("_beatmapCharacteristicSegmentedControlController").gameObject.SetActive(!hide);
            _levelDetailView.GetPrivateField<BeatmapDifficultySegmentedControlController>("_beatmapDifficultySegmentedControlController").gameObject.SetActive(!hide);
        }

        private void SetSegmentedControlsInteractable(bool charInteractable, bool diffInteractable)
        {
            if(_segmentedControlsBlocker == null)
            {
                _segmentedControlsBlocker = new GameObject("Blocker").AddComponent<Image>();
                _segmentedControlsBlocker.rectTransform.SetParent(_levelDetailView.GetComponentsInChildren<RectTransform>().First(x => x.name == "PlayContainer"), false);
                _segmentedControlsBlocker.rectTransform.pivot = new Vector2(0.5f, 1f);
                _segmentedControlsBlocker.rectTransform.anchorMin = new Vector2(0f, 1f);
                _segmentedControlsBlocker.rectTransform.anchorMax = new Vector2(1f, 1f);
                _segmentedControlsBlocker.rectTransform.anchoredPosition = new Vector2(0f, -1f);
                _segmentedControlsBlocker.material = Sprites.NoGlowMat;
                _segmentedControlsBlocker.color = new Color(1f, 1f, 1f, 0f);
            }

            _segmentedControlsBlocker.rectTransform.sizeDelta = new Vector2(0f, (!charInteractable ? 10 : 0) + (!diffInteractable ? 10 : 0));
        }

        internal void SetSelectedSong(SongInfo song)
        {
            _playContainer.SetActive(false);
        }

        internal void SetProgressBarState(bool enabled, float progress)
        {
            if (enabled)
            {
                _playContainer.SetActive(false);
                _progressBarParent.SetActive(true);

                _loadingProgressText.text = $"{(Mathf.Round(progress * 10000f) / 100f).ToString("0.00")}%";
                _progressBarTop.fillAmount = progress;
            }
            else
            {
                _playContainer.SetActive(true);
                _progressBarParent.SetActive(false);
            }

        }
    }
}
