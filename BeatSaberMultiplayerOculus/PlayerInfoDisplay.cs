using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    class PlayerInfoDisplay : MonoBehaviour
    {
        private PlayerInfo _playerInfo;

        public TextMeshPro playerNameText;
        public TextMeshPro playerScoreText;
        public TextMeshPro playerPlaceText;
        
        private int previousScore;
        private int currentScore;
        private float progress;

        void Update()
        {
            progress += Time.deltaTime * 20;
            playerScoreText.text = Mathf.Lerp(previousScore, currentScore, Mathf.Clamp01(progress)).ToString("#");
        }

        void Awake()
        {
            BSMultiplayerUI ui = BSMultiplayerUI._instance;

            playerPlaceText = ui.CreateWorldText(transform, "");
            playerPlaceText.rectTransform.anchoredPosition = new Vector2(2.5f, 0f);
            playerPlaceText.fontSize = 8f;

            playerNameText = ui.CreateWorldText(transform, "");
            playerNameText.rectTransform.anchoredPosition = new Vector2(4f,0f);
            playerNameText.fontSize = 7f;

            playerScoreText = ui.CreateWorldText(transform, "");
            playerScoreText.rectTransform.anchoredPosition = new Vector2(15f,0f);
            playerScoreText.fontSize = 8f;
        }

        public void UpdatePlayerInfo(PlayerInfo _info, int _index)
        {
            _playerInfo = _info;

            if (_playerInfo != null)
            {
                playerPlaceText.text = (_index+1).ToString();
                playerNameText.text = _playerInfo.playerName;
                previousScore = currentScore;
                currentScore = _playerInfo.playerScore;
                progress = 0;
            }
            else
            {
                playerPlaceText.text = "";
                playerNameText.text = "";
                playerScoreText.text = "";
            }
        }

    }
}
