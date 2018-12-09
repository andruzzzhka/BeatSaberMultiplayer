using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
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

        private uint previousScore;
        private uint currentScore;
        private float progress;

        void Update()
        {
            if (_playerInfo != null)
            {
                progress += Time.deltaTime * Client.instance.Tickrate;
                uint score = (uint)Mathf.Lerp(previousScore, currentScore, Mathf.Clamp01(progress));

#if DEBUG
                playerScoreText.text = string.Format("{0} {2} {3}", score, _playerInfo.playerEnergy, _playerInfo.playerCutBlocks, _playerInfo.playerComboBlocks);
#else
                playerScoreText.text = string.Format("{0}", score, _playerInfo.playerEnergy, _playerInfo.playerCutBlocks, _playerInfo.playerComboBlocks);
#endif
            }
        }

        void Awake()
        {
            playerPlaceText = CustomExtensions.CreateWorldText(transform, "");
            playerPlaceText.rectTransform.anchoredPosition = new Vector2(2.5f, 0f);
            playerPlaceText.fontSize = 8f;

            playerNameText = CustomExtensions.CreateWorldText(transform, "");
            playerNameText.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            playerNameText.fontSize = 7f;

            playerScoreText = CustomExtensions.CreateWorldText(transform, "");
            playerScoreText.rectTransform.anchoredPosition = new Vector2(15f, 0f);
            playerScoreText.fontSize = 8f;
        }

        public void UpdatePlayerInfo(PlayerInfo _info, int _index)
        {
            _playerInfo = _info;

            if (_playerInfo != null)
            {
                playerPlaceText.text = (_index + 1).ToString();
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
