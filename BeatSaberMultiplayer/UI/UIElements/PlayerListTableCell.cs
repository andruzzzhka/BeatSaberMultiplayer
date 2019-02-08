using BeatSaberMultiplayer.Data;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI.UIElements
{
    class PlayerListTableCell : LeaderboardTableCell
    {
        public float progress
        {
            set
            {
                if(value < 0f)
                {
                    _scoreText.text = "";
                }
                else if (value < 1f)
                {
                    _scoreText.text = value.ToString("P");
                }
                else
                {
                    _scoreText.text = "DOWNLOADED";
                }
            }
        }

        public new int rank
        {
            set
            {
                if(value <= 0)
                {
                    _rankText.text = "";
                }
                else
                {
                    _rankText.text = value.ToString();
                }
            }
        }

        public bool IsTalking
        {
            set
            {
                if (playerSpeakerIcon != null)
                    playerSpeakerIcon.gameObject.SetActive(value);
            }
        }

        private Image playerSpeakerIcon;

        protected override void Awake()
        {
            base.Awake();
        }

        public void Init()
        {
            LeaderboardTableCell cell = GetComponent<LeaderboardTableCell>();

            foreach (FieldInfo info in cell.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                info.SetValue(this, info.GetValue(cell));
            }

            Destroy(cell);

            reuseIdentifier = "DownloadCell";

            _playerNameText.rectTransform.anchoredPosition = new Vector2(12f, 0f);

            playerSpeakerIcon = new GameObject("Player Speaker Icon", typeof(Canvas), typeof(CanvasRenderer)).AddComponent<Image>();
            playerSpeakerIcon.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            playerSpeakerIcon.rectTransform.SetParent(transform);
            playerSpeakerIcon.rectTransform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            playerSpeakerIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            playerSpeakerIcon.rectTransform.anchoredPosition = new Vector2(-38f, 0f);
            playerSpeakerIcon.sprite = Sprites.speakerIcon;
            playerSpeakerIcon.material = Sprites.NoGlowMat;

        }

    }
}
