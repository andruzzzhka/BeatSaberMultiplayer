using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Interop;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using BS_Utils.Utilities;
using HMUI;
using IPA.Loader;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class MultiplayerResultsViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public IPreviewBeatmapLevel selectedLevel;

        [UIComponent("results-tab")]
        public RectTransform resultsTab;

        [UIComponent("level-details-rect")]
        public RectTransform levelDetailsRect;

        [UIComponent("song-name-text")]
        public TextMeshProUGUI songNameText;
        [UIComponent("level-cover-image")]
        public RawImage levelCoverImage;

        [UIComponent("leaderboard-table")]
        public LeaderboardTableView leaderboardTableView;

        [UIComponent("timer-text")]
        public TextMeshProUGUI timerText;

        [UIComponent("difficlty-value")]
        public TextMeshProUGUI diffValue;
        [UIComponent("good-cuts-value")]
        public TextMeshProUGUI goodCutsValue;
        [UIComponent("max-combo-value")]
        public TextMeshProUGUI maxComboValue;
        [UIComponent("score-value")]
        public TextMeshProUGUI scoreValue;
        [UIComponent("rank-value")]
        public TextMeshProUGUI rankValue;
        [UIComponent("score-change-value")]
        public TextMeshProUGUI scoreChangeValue;
        [UIComponent("score-change-icon")]
        public Image scoreChangeIcon;

        private List<LeaderboardTableView.ScoreData> _scoreData = new List<LeaderboardTableView.ScoreData>();

        private Texture2D _defaultArtworkTexture;

        private BeatmapLevelsModel _beatmapLevelsModel;

        [UIAction("#post-parse")]
        public void SetupViewController()
        {
            levelDetailsRect.gameObject.AddComponent<Mask>();

            Image maskImage = levelDetailsRect.gameObject.AddComponent<Image>();

            maskImage.material = Sprites.NoGlowMat;
            maskImage.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "RoundRectPanel");
            maskImage.type = Image.Type.Sliced;
            maskImage.color = new Color(0f, 0f, 0f, 0.25f);

            levelCoverImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();

            _defaultArtworkTexture = Resources.FindObjectsOfTypeAll<Texture2D>().First(x => x.name == "DefaultSongArtwork");

            leaderboardTableView.GetComponent<TableView>().RemoveReusableCells("Cell");

            if (selectedLevel != null)
                SetContent(selectedLevel);
        }

        public void UpdateLeaderboard()
        {
            var scores = InGameOnlineController.Instance.playerScores;

            if (scores == null)
                return;

            scores.Sort();

            if (scores.Count < _scoreData.Count)
            {
                _scoreData.RemoveRange(scores.Count, _scoreData.Count - scores.Count);
            }

            for (int i = 0; i < scores.Count; i++)
            {
                if (_scoreData.Count <= i)
                {
                    _scoreData.Add(new LeaderboardTableView.ScoreData((int)scores[i].score, scores[i].name, i + 1, false));
                    Plugin.log.Debug("Added new ScoreData!");
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
            selectedLevel = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID.StartsWith(info.levelId));

            if (selectedLevel != null)
            {
                SetContent(selectedLevel);
            }
            else
            {
                songNameText.text = info.songName;

                levelCoverImage.texture = _defaultArtworkTexture;

                diffValue.text = "--";
                goodCutsValue.text = "--";
                maxComboValue.text = "";
                scoreValue.text = "--";
                rankValue.text = "--";
                scoreChangeValue.text = "--";
                scoreChangeValue.color = Color.white;
                scoreChangeIcon.gameObject.SetActive(false);

                SongDownloader.Instance.RequestSongByLevelID(info.hash, (song) =>
                {
                    songNameText.text = info.songName;

                    StartCoroutine(LoadScripts.LoadSpriteCoroutine(song.coverURL, (cover) => { levelCoverImage.texture = cover; }));
                }); 
                
                if (PluginManager.GetPluginFromId("BeatSaverVoting") != null)
                    BeatSaverVotingInterop.Hide();
            }
        }

        public async void SetContent(IPreviewBeatmapLevel level)
        {
            songNameText.text = selectedLevel.songName;

            Plugin.log.Debug("Set content called!");
            if (PluginUI.instance.roomFlowCoordinator.levelDifficultyBeatmap != null)
            {
                var diffBeatmap = PluginUI.instance.roomFlowCoordinator.levelDifficultyBeatmap;
                var levelResults = PluginUI.instance.roomFlowCoordinator.levelResults;

                diffValue.text = diffBeatmap.difficulty.ToString().Replace("Plus", "+").ToUpper();
                goodCutsValue.text = $"{levelResults.goodCutsCount}<size=50%>/{diffBeatmap.beatmapData.notesCount}</size>";
                maxComboValue.text = $"MAX COMBO {levelResults.maxCombo}";
                scoreValue.text = ScoreFormatter.Format(levelResults.modifiedScore);
                rankValue.text = levelResults.rank.ToString();

                if (!PluginUI.instance.roomFlowCoordinator.lastHighscoreValid)
                {
                    scoreChangeValue.text = "--";
                    scoreChangeValue.color = Color.white;
                    scoreChangeIcon.gameObject.SetActive(false);
                }
                else
                {
                    if(PluginUI.instance.roomFlowCoordinator.lastHighscoreForLevel > levelResults.modifiedScore)
                    {
                        scoreChangeValue.text = (levelResults.modifiedScore - PluginUI.instance.roomFlowCoordinator.lastHighscoreForLevel).ToString();
                        scoreChangeValue.color = new Color32(240, 38, 31, 255);
                        scoreChangeIcon.gameObject.SetActive(true);
                        scoreChangeIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                        scoreChangeIcon.color = new Color32(240, 38, 31, 255);
                    }
                    else
                    {
                        scoreChangeValue.text = "+"+(levelResults.modifiedScore - PluginUI.instance.roomFlowCoordinator.lastHighscoreForLevel).ToString();
                        scoreChangeValue.color = new Color32(55, 235, 43, 255);
                        scoreChangeIcon.gameObject.SetActive(true);
                        scoreChangeIcon.rectTransform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                        scoreChangeIcon.color = new Color32(55, 235, 43, 255);
                    }
                }


                if (PluginManager.GetPluginFromId("BeatSaverVoting") != null)
                    BeatSaverVotingInterop.Setup(this, PluginUI.instance.roomFlowCoordinator.levelDifficultyBeatmap.level);
            }
            else if (PluginManager.GetPluginFromId("BeatSaverVoting") != null)
                    BeatSaverVotingInterop.Hide();

            levelCoverImage.texture = await selectedLevel.GetCoverImageTexture2DAsync(new CancellationTokenSource().Token);

        }

        public void SetTimer(int time)
        {
            timerText.text = EssentialHelpers.MinSecDurationText(time);
        }
    }
}
