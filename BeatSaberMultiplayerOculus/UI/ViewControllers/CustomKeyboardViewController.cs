using BeatSaberMultiplayer.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI
{
    class CustomKeyboardViewController : VRUIViewController
    {
        GameObject _customKeyboardGO;

        CustomUIKeyboard _customKeyboard;

        Button _enterButton;
        Button _backButton;

        TextMeshProUGUI _inputText;
        public string _inputString = "";

        public event Action<string> enterButtonPressed;
        public event Action backButtonPressed;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (_customKeyboard == null)
            {
                _customKeyboardGO = Instantiate(Resources.FindObjectsOfTypeAll<UIKeyboard>().First(x => x.name != "CustomUIKeyboard"), rectTransform, false).gameObject;

                _customKeyboard = _customKeyboardGO.AddComponent<CustomUIKeyboard>();

                _customKeyboard.uiKeyboardKeyEvent += delegate (char input) { _inputString += input; UpdateInputText(); };
                _customKeyboard.uiKeyboardDeleteEvent += delegate () { _inputString = _inputString.Substring(0, Math.Max(_inputString.Length - 1, 0)); UpdateInputText(); };
            }

            if(_inputText == null)
            {
                _inputText = BeatSaberUI.CreateText(rectTransform, _inputString, new Vector2(0f,-11.5f));
                _inputText.alignment = TextAlignmentOptions.Center;
                _inputText.fontSize = 6f;
            }
            else
            {
                UpdateInputText();
            }

            if(_enterButton == null)
            {
                _enterButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                BeatSaberUI.SetButtonText(_enterButton, "Enter");
                (_enterButton.transform as RectTransform).sizeDelta = new Vector2(30f, 10f);
                (_enterButton.transform as RectTransform).anchoredPosition = new Vector2(-65f, 1.5f);
                _enterButton.onClick.RemoveAllListeners();
                _enterButton.onClick.AddListener(delegate() {
                    DismissModalViewController(null, false);
                    enterButtonPressed?.Invoke(_inputString);
                });
            }

            if (_backButton == null)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform);

                _backButton.onClick.AddListener(delegate ()
                {
                    _inputString = "";
                    DismissModalViewController(null, false);
                    backButtonPressed?.Invoke();
                });
            }

        }

        void UpdateInputText()
        {
            if (_inputText != null)
            {
                _inputText.text = _inputString.ToUpper();
            }
        }

        void ClearInput()
        {
            _inputString = "";
        }
        

    }
}
