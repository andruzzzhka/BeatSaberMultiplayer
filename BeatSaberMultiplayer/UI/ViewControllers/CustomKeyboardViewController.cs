using BeatSaberMultiplayer.UI;
using CustomUI.BeatSaber;
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

        TextMeshProUGUI _inputText;
        public string _inputString = "";

        public event Action<string> enterButtonPressed;
        public event Action backButtonPressed;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (type == ActivationType.AddedToHierarchy && firstActivation)
            {
                _customKeyboardGO = Instantiate(Resources.FindObjectsOfTypeAll<UIKeyboard>().First(x => x.name != "CustomUIKeyboard"), rectTransform, false).gameObject;

                Destroy(_customKeyboardGO.GetComponent<UIKeyboard>());
                _customKeyboard = _customKeyboardGO.AddComponent<CustomUIKeyboard>();

                _customKeyboard.textKeyWasPressedEvent += delegate (char input) { _inputString += input; UpdateInputText(); };
                _customKeyboard.deleteButtonWasPressedEvent += delegate () { _inputString = _inputString.Substring(0, _inputString.Length - 1); UpdateInputText(); };
                _customKeyboard.cancelButtonWasPressedEvent += () => { backButtonPressed?.Invoke(); };
                _customKeyboard.okButtonWasPressedEvent += () => { enterButtonPressed?.Invoke(_inputString); };

                _inputText = BeatSaberUI.CreateText(rectTransform, _inputString, new Vector2(0f, 22f));
                _inputText.alignment = TextAlignmentOptions.Center;
                _inputText.fontSize = 6f;

            }
            else
            {
                _inputString = "";
                UpdateInputText();
            }

        }

        void UpdateInputText()
        {
            if (_inputText != null)
            {
                _inputText.text = _inputString.ToUpper();
                if (string.IsNullOrEmpty(_inputString))
                {
                    _customKeyboard.OkButtonInteractivity = false;
                }
                else
                {
                    _customKeyboard.OkButtonInteractivity = true;
                }
            }
        }

        void ClearInput()
        {
            _inputString = "";
        }


    }
}
