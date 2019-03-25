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
using System.Globalization;
using UnityEngine.Networking;
using SimpleJSON;
using System.Collections;
using Steamworks;

namespace BeatSaberMultiplayer.UI.ViewControllers.RadioScreen
{
    class ResultsScreenViewController : VRUIViewController
    {
        TextMeshProUGUI _timerText;

        TextMeshProUGUI _songNameText;
        TextMeshProUGUI _scoreText;
        TextMeshProUGUI _difficultyText;
        TextMeshProUGUI _rankText;
        TextMeshProUGUI _goodCutsText;
        TextMeshProUGUI _fullComboText;

        IDifficultyBeatmap lastSong;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _timerText = BeatSaberUI.CreateText(rectTransform, "0:30", new Vector2(0f, 35f));
                _timerText.alignment = TextAlignmentOptions.Top;
                _timerText.fontSize = 8f;

                RectTransform resultsRectTransform = Resources.FindObjectsOfTypeAll<RectTransform>().First(x => x.name == "StandardLevelResultsViewController");
                Instantiate(resultsRectTransform.GetComponentsInChildren<RectTransform>().First(x => x.name == "Cleared"), rectTransform);

                _songNameText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "SongNameText");
                _scoreText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "ScoreText");
                _difficultyText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "DifficultyText");
                _rankText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "RankText");
                _goodCutsText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "GoodCutsText");
                _fullComboText = GetComponentsInChildren<TextMeshProUGUI>().First(x => x.name == "FullComboText");
                Destroy(GetComponentsInChildren<RectTransform>().First(x => x.name == "HeaderPanel").gameObject);
                Destroy(GetComponentsInChildren<RectTransform>().First(x => x.name == "NewHighScoreText").gameObject);
            }
        }

        public void SetSongInfo(SongInfo songInfo, BeatmapDifficulty difficulty)
        {
            lastSong = null;

            _songNameText.text = songInfo.songName;
            _scoreText.text = ScoreFormatter.Format(0);
            _difficultyText.text = difficulty.Name();
            _rankText.text = "E";
            _goodCutsText.text = "0<size=50%> / 0";
            _fullComboText.gameObject.SetActive(false);
        }

        public void SetSongInfo(IDifficultyBeatmap songInfo, LevelCompletionResults results)
        {
            if (lastSong != songInfo)
            {
                lastSong = songInfo;

                _songNameText.text = $"{songInfo.level.songName} <size=80%>{songInfo.level.songSubName}</size>";
                _scoreText.text = ScoreFormatter.Format(results.score);
                _difficultyText.text = songInfo.difficulty.Name();
                _rankText.text = RankModel.GetRankName(results.rank);
                _goodCutsText.text = $"{results.goodCutsCount}<size=50%> / {songInfo.beatmapData.notesCount}";
                _fullComboText.gameObject.SetActive(results.fullCombo);
            }
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
