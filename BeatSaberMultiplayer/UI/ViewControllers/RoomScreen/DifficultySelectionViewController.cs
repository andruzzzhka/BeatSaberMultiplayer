using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using CustomUI.BeatSaber;
using HMUI;
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
        public event Action discardPressed;
        public event Action levelOptionsChanged;
        public event Action<BeatmapLevelSO, BeatmapCharacteristicSO, BeatmapDifficulty> playPressed;

        public BeatmapCharacteristicSO selectedCharacteristic { get; private set; }
        public BeatmapDifficulty selectedDifficulty { get; private set; }

        LevelListTableCell _selectedSongCell;

        BeatmapLevelSO _selectedSong;
        BeatmapCharacteristicSO[] _beatmapCharacteristics;
        BeatmapCharacteristicSO _standardCharacteristic;

        TextSegmentedControl _characteristicControl;
        TextSegmentedControl _difficultyControl;

        Button _cancelButton;
        Button _playButton;
        
        TextMeshProUGUI _playersReadyText;
                
        private RectTransform _progressBarRect;
        private UnityEngine.UI.Image _progressBackground;
        private UnityEngine.UI.Image _progressBarImage;
        private TextMeshProUGUI _progressText;

        private RectTransform _characteristicControlBlocker;
        private RectTransform _difficultyControlBlocker;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _beatmapCharacteristics = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSO>();
                _standardCharacteristic = _beatmapCharacteristics.First(x => x.characteristicName == "Standard");

                bool isHost = Client.Instance.isHost;

                _selectedSongCell = Instantiate(Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell")), rectTransform, false);
                (_selectedSongCell.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
                (_selectedSongCell.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
                (_selectedSongCell.transform as RectTransform).anchoredPosition = new Vector2(-25f, 7.5f);
                _selectedSongCell.SetPrivateField("_beatmapCharacteristicAlphas", new float[0]);
                _selectedSongCell.SetPrivateField("_beatmapCharacteristicImages", new UnityEngine.UI.Image[0]);
                _selectedSongCell.SetPrivateField("_bought", true);
                foreach (var icon in _selectedSongCell.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name.StartsWith("LevelTypeIcon")))
                {
                    Destroy(icon.gameObject);
                }

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
                _cancelButton.onClick.AddListener(delegate () { discardPressed?.Invoke(); });
                _cancelButton.gameObject.SetActive(isHost);

                _playButton = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton");
                (_playButton.transform as RectTransform).anchoredPosition = new Vector2(30f, -25f);
                (_playButton.transform as RectTransform).sizeDelta = new Vector2(28f, 12f);
                _playButton.SetButtonText("PLAY");
                _playButton.ToggleWordWrapping(false);
                _playButton.SetButtonTextSize(5.5f);
                _playButton.onClick.AddListener(delegate () { playPressed?.Invoke(_selectedSong, selectedCharacteristic, selectedDifficulty); });
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
            }
            _playButton.interactable = false;
            _progressBarRect.gameObject.SetActive(false);
        }

        private void _characteristicControl_didSelectCellEvent(int arg2)
        {
            selectedCharacteristic = _selectedSong.beatmapCharacteristics[arg2];
            
            IDifficultyBeatmap[] difficulties = _selectedSong.difficultyBeatmapSets.First(x => x.beatmapCharacteristic == selectedCharacteristic).difficultyBeatmaps;
            
            _difficultyControl.SetTexts(difficulties.Select(x => x.difficulty.ToString().Replace("Plus", "+")).ToArray());

            int closestDifficultyIndex = CustomExtensions.GetClosestDifficultyIndex(difficulties, selectedDifficulty);

            _difficultyControl.SelectCellWithNumber(closestDifficultyIndex);

            if (!difficulties.Any(x => x.difficulty == selectedDifficulty))
            {
                _difficultyControl_didSelectCellEvent(closestDifficultyIndex);
            }
            else
            {
                levelOptionsChanged?.Invoke();
            }
        }

        private void _difficultyControl_didSelectCellEvent(int arg2)
        {
            selectedDifficulty = _selectedSong.difficultyBeatmapSets.First(x => x.beatmapCharacteristic == selectedCharacteristic).difficultyBeatmaps[arg2].difficulty;

            levelOptionsChanged?.Invoke();
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
            _selectedSong = SongLoader.CustomBeatmapLevelPackCollectionSO.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(info.levelId)) as BeatmapLevelSO;

            if (_selectedSong != null)
            {                
                _selectedSongCell.SetText(_selectedSong.songName + " <size=80%>" + _selectedSong.songSubName + "</size>");
                _selectedSongCell.SetSubText(_selectedSong.songAuthorName);
                _selectedSongCell.SetIcon(_selectedSong.coverImage);

                _characteristicControl.SetTexts(_selectedSong.beatmapCharacteristics.Select(x => x.characteristicName).ToArray());

                int standardCharacteristicIndex = Array.FindIndex(_selectedSong.beatmapCharacteristics, x => x.serializedName == "Standard");

                _characteristicControl.SelectCellWithNumber((standardCharacteristicIndex == -1 ? 0 : standardCharacteristicIndex));
                _characteristicControl_didSelectCellEvent((standardCharacteristicIndex == -1 ? 0 : standardCharacteristicIndex));
            }
            else
            {
                _selectedSongCell.SetText(info.songName);
                _selectedSongCell.SetSubText( "Loading info...");
                SongDownloader.Instance.RequestSongByLevelID(info.levelId, (song) =>
                {
                    _selectedSongCell.SetText($"{song.songName} <size=80%>{song.songSubName}</size>");
                    _selectedSongCell.SetSubText(song.authorName);
                    StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverUrl, (cover) => { _selectedSongCell.SetIcon(cover); }));

                });
            }
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
            if (_selectedSong.difficultyBeatmapSets.First(x => x.beatmapCharacteristic == selectedCharacteristic).difficultyBeatmaps.Any(x => x.difficulty == difficulty))
            {
                int diffIndex = Array.IndexOf(_selectedSong.difficultyBeatmapSets.First(x => x.beatmapCharacteristic == selectedCharacteristic).difficultyBeatmaps, difficulty);

                _difficultyControl.SelectCellWithNumber(diffIndex);
                _difficultyControl_didSelectCellEvent(diffIndex);
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
