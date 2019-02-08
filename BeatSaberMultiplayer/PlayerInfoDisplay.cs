using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer
{
    public class PlayerInfoDisplay : MonoBehaviour
    {
        private PlayerInfo _playerInfo;

        public TextMeshPro playerNameText;
        public TextMeshPro playerScoreText;
        public TextMeshPro playerPlaceText;

        public Image playerSpeakerIcon;

        private uint previousScore;
        private uint currentScore;
        private float progress;

        void Update()
        {
            if (_playerInfo != null)
            {
                progress += Time.deltaTime * Client.Instance.Tickrate;
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
            
            playerSpeakerIcon = new GameObject("Player Speaker Icon", typeof(Canvas), typeof(CanvasRenderer)).AddComponent<Image>();
            playerSpeakerIcon.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            playerSpeakerIcon.rectTransform.SetParent(transform);
            playerSpeakerIcon.rectTransform.localScale = new Vector3(0.008f, 0.008f, 1f);
            playerSpeakerIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            playerSpeakerIcon.rectTransform.anchoredPosition3D = new Vector3(-8.5f, 2f, 0f);
            playerSpeakerIcon.sprite = Sprites.speakerIcon;
        }

        public void UpdatePlayerInfo(PlayerInfo _info, int _index)
        {
            _playerInfo = _info;

            if (_playerInfo != null)
            {
                playerPlaceText.text = (_index + 1).ToString();
                playerNameText.text = _playerInfo.playerName;
                playerNameText.color = _info.playerNameColor;
                previousScore = currentScore;
                currentScore = _playerInfo.playerScore;
                playerSpeakerIcon.gameObject.SetActive(InGameOnlineController.Instance.VoiceChatIsTalking(_info.playerId));
                progress = 0;
            }
            else
            {
                playerPlaceText.text = "";
                playerNameText.text = "";
                playerScoreText.text = "";
                playerSpeakerIcon.gameObject.SetActive(false);
            }
        }

    }
}
