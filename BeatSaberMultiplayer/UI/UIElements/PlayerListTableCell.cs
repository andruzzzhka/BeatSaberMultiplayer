using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using CustomUI.BeatSaber;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI.UIElements
{
    class PlayerListTableCell : LeaderboardTableCell
    {
        public float progress
        {
            set
            {
                if (playerInfo == null)
                    return;

                if (value < 0f)
                {
                    if (_prevProgress != value || value < 0f)
                    {
                        _scoreText.text = "";
                    }

                    if (_transferHostButtonEnabled != (Client.Instance.isHost && !playerInfo.Equals(Client.Instance.playerInfo)))
                    {
                        _transferHostButtonEnabled = (Client.Instance.isHost && !playerInfo.Equals(Client.Instance.playerInfo));
                        _transferHostButton.gameObject.SetActive(_transferHostButtonEnabled);
                    }

                    if (_muteButtonEnabled != (!playerInfo.Equals(Client.Instance.playerInfo)))
                    {
                        _muteButtonEnabled = (!playerInfo.Equals(Client.Instance.playerInfo));
                        _muteButton.gameObject.SetActive(_muteButtonEnabled);
                    }
                }
                else if (value < 1f)
                {
                    if (_prevProgress != value || value < 0f)
                    {
                        _scoreText.text = value.ToString("P");
                    }

                    if (_transferHostButtonEnabled)
                    {
                        _transferHostButtonEnabled = false;
                        _transferHostButton.gameObject.SetActive(_transferHostButtonEnabled);
                    }

                    if (_muteButtonEnabled)
                    {
                        _muteButtonEnabled = false;
                        _muteButton.gameObject.SetActive(_muteButtonEnabled);
                    }
                }
                else
                {
                    if (_prevProgress != value || value < 0f)
                    {
                        _scoreText.text = "DOWNLOADED";
                    }

                    if (_transferHostButtonEnabled)
                    {
                        _transferHostButtonEnabled = false;
                        _transferHostButton.gameObject.SetActive(_transferHostButtonEnabled);
                    }

                    if (_muteButtonEnabled)
                    {
                        _muteButtonEnabled = false;
                        _muteButton.gameObject.SetActive(_muteButtonEnabled);
                    }
                }
                _prevProgress = value;
            }
        }
        private float _prevProgress;

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
                if (_playerSpeakerIcon != null)
                    _playerSpeakerIcon.gameObject.SetActive(value);
            }
        }

        public Color NameColor
        {
            set
            {
                if (_playerNameText != null)
                    _playerNameText.color = value;
            }
        }

        public PlayerInfo playerInfo;
        public IPlayerManagementButtons buttonsInterface;

        private Sprite _buttonStrokeSprite;

        private Image _playerSpeakerIcon;
        private Button _transferHostButton;
        private Button _muteButton;
        private bool _isMuted;

        private bool _transferHostButtonEnabled;
        private bool _muteButtonEnabled;
        
        public void Init()
        {
            LeaderboardTableCell cell = GetComponent<LeaderboardTableCell>();

            foreach (FieldInfo info in cell.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                info.SetValue(this, info.GetValue(cell));
            }

            Destroy(cell);

            reuseIdentifier = "PlayerCell";

            if(_buttonStrokeSprite == null)
            {
                _buttonStrokeSprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "RoundRectSmallStroke");
            }

            _playerNameText.rectTransform.anchoredPosition = new Vector2(12f, 0f);

            _playerSpeakerIcon = new GameObject("Player Speaker Icon", typeof(Canvas), typeof(CanvasRenderer)).AddComponent<Image>();
            _playerSpeakerIcon.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _playerSpeakerIcon.rectTransform.SetParent(transform);
            _playerSpeakerIcon.rectTransform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            _playerSpeakerIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _playerSpeakerIcon.rectTransform.anchoredPosition = new Vector2(-38f, 0f);
            _playerSpeakerIcon.sprite = Sprites.speakerIcon;
            _playerSpeakerIcon.material = Sprites.NoGlowMat;

            _transferHostButton = BeatSaberUI.CreateUIButton(transform as RectTransform, "CancelButton", new Vector2(14f, 0f), new Vector2(14f, 6f), () => {
                if (buttonsInterface != null)
                    buttonsInterface.TransferHostButtonWasPressed(playerInfo);
            }, "PASS\nHOST");

            _transferHostButton.ToggleWordWrapping(false);
            _transferHostButton.SetButtonTextSize(3.25f);
            _transferHostButton.GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name == "Stroke").sprite = _buttonStrokeSprite;
            _transferHostButtonEnabled = true;

            _muteButton = BeatSaberUI.CreateUIButton(transform as RectTransform, "CancelButton", new Vector2(30f, 0f), new Vector2(14f, 6f), () => {
                if (buttonsInterface != null)
                    buttonsInterface.MuteButtonWasPressed(playerInfo);
            }, "MUTE");
            _isMuted = false;

            _muteButton.ToggleWordWrapping(false);
            _muteButton.GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name == "Stroke").sprite = _buttonStrokeSprite;
            _muteButtonEnabled = true;

            _scoreText.overflowMode = TextOverflowModes.Overflow;
            _scoreText.enableWordWrapping = false;
        }

        public void Update()
        {
            if (_muteButton != null && playerInfo != null)
            {
                if (_isMuted && !InGameOnlineController.Instance.mutedPlayers.Contains(playerInfo.playerId))
                {
                    _isMuted = false;
                    _muteButton.SetButtonText("MUTE");
                }
                else if (!_isMuted && InGameOnlineController.Instance.mutedPlayers.Contains(playerInfo.playerId))
                {
                    _isMuted = true;
                    _muteButton.SetButtonText("UNMUTE");
                }
            }
        }

    }
}
