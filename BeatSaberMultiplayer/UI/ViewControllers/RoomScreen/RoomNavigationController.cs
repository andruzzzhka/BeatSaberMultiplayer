using CustomUI.BeatSaber;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class RoomNavigationController : VRUINavigationController
    {
        public event Action didFinishEvent;

        public TextMeshProUGUI _errorText;

        private Button _backButton;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform);
                _backButton.onClick.AddListener(delegate () { didFinishEvent?.Invoke(); });

                _errorText = BeatSaberUI.CreateText(rectTransform, "", new Vector2(0f, 0f));
                _errorText.fontSize = 8f;
                _errorText.alignment = TextAlignmentOptions.Center;
                _errorText.rectTransform.sizeDelta = new Vector2(120f, 6f);


            }
            _errorText.text = "";
            _errorText.gameObject.SetActive(false);
        }

        public void DisplayError(string error)
        {
            if (_errorText != null)
            {
                _errorText.gameObject.SetActive(true);
                _errorText.text = error;
            }
        }

    }
}
