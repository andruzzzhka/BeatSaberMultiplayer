using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.ChannelSelectionScreen
{
    class ChannelSelectionViewController : VRUIViewController
    {

        public event Action nextPressedEvent;
        public event Action prevPressedEvent;
        public event Action<ChannelInfo> joinPressedEvent;

        GameObject _container;

        Button _joinButton;

        RawImage _channelCover;

        TextMeshProUGUI _channelNameText;
        TextMeshProUGUI _playerCountText;
        TextMeshProUGUI _nowPlayingText;


        Button _prevChannelButton;
        Button _nextChannelButton;
        
        ChannelInfo currentChannel;

        GameObject _loadingIndicator;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                _prevChannelButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton", new Vector2(-55f, 34f), () => { prevPressedEvent?.Invoke(); }, "<<");
                _nextChannelButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton", new Vector2(65f, 34f), () => { nextPressedEvent?.Invoke(); }, ">>");

                _loadingIndicator = BeatSaberUI.CreateLoadingSpinner(rectTransform);
                (_loadingIndicator.transform as RectTransform).anchoredPosition = new Vector2(4.5f, 4.5f);

                _container = new GameObject("Container", typeof(RectTransform));
                _container.transform.SetParent(rectTransform, false);

                _joinButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton", new Vector2(4.5f, -30f), () => { joinPressedEvent?.Invoke(currentChannel); }, "Join");
                _joinButton.ToggleWordWrapping(false);
                _joinButton.transform.SetParent(_container.transform, true);

                _channelCover = new GameObject("CustomUIImage").AddComponent<RawImage>();
                
                _channelCover.material = Sprites.NoGlowMat;
                _channelCover.rectTransform.SetParent(rectTransform, false);
                _channelCover.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _channelCover.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _channelCover.rectTransform.anchoredPosition = new Vector2(4.5f, 4.5f);
                _channelCover.rectTransform.sizeDelta = new Vector2(32f, 32f);
                _channelCover.texture = UIUtilities.BlankSprite.texture;
                _channelCover.rectTransform.SetParent(_container.transform, true);

                _channelNameText = BeatSaberUI.CreateText(rectTransform, "CHANNEL NAME", new Vector2(4.5f, 35.50f));
                _channelNameText.alignment = TextAlignmentOptions.Top;
                _channelNameText.overflowMode = TextOverflowModes.Overflow;
                _channelNameText.lineSpacing = -46f;
                _channelNameText.fontSize = 10;
                _channelNameText.transform.SetParent(_container.transform, true);

                _playerCountText = BeatSaberUI.CreateText(rectTransform, "Players: 0", new Vector2(4.5f, -15f));
                _playerCountText.alignment = TextAlignmentOptions.Center;
                _playerCountText.transform.SetParent(_container.transform, true);

                _nowPlayingText = BeatSaberUI.CreateText(rectTransform, "Now playing: UNKNOWN", new Vector2(4.5f, -21f));
                _nowPlayingText.alignment = TextAlignmentOptions.Center;
                _nowPlayingText.transform.SetParent(_container.transform, true);

            }
            _container.SetActive(false);
        }

        public void SetLoadingState(bool loading)
        {
            _loadingIndicator.SetActive(loading);
        }

        public void SetContent(ChannelInfo channelInfo)
        {
            _container.SetActive(true);

            currentChannel = channelInfo;

            if (!string.IsNullOrEmpty(channelInfo.iconUrl))
            {
                _loadingIndicator.SetActive(true);
                StartCoroutine(LoadScripts.LoadSpriteCoroutine(channelInfo.iconUrl, (image) => 
                {
                    _loadingIndicator.SetActive(false);
                    _channelCover.texture = image;
                }));
                Plugin.log.Info("Loading icon from URL: \""+channelInfo.iconUrl+"\"");
            }
            else
            {
                Plugin.log.Info("Icon URL is empty!");
                _channelCover.texture = Sprites.radioIcon.texture;
            }

            _channelNameText.text = channelInfo.name;
            _playerCountText.text = "Players: "+channelInfo.playerCount;
            _nowPlayingText.text = "Now playing: "+channelInfo.currentSong.songName;
        }
    }
}
