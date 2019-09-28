using CustomUI.BeatSaber;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using VRUI;

namespace BeatSaberMultiplayer.UI
{
    class CustomKeyboardViewController : VRUIViewController
    {
        GameObject _customKeyboardGO;

        CustomUIKeyboard _customKeyboard;

        TextMeshProUGUI _inputText;
        public string inputString = "";
        public bool allowEmptyInput;

        public event Action<string> enterButtonPressed;
        public event Action backButtonPressed;

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (type == ActivationType.AddedToHierarchy && firstActivation)
            {
                _customKeyboardGO = Instantiate(Resources.FindObjectsOfTypeAll<UIKeyboard>().First(x => x.name != "CustomUIKeyboard"), rectTransform, false).gameObject;

                Destroy(_customKeyboardGO.GetComponent<UIKeyboard>());
                _customKeyboard = _customKeyboardGO.AddComponent<CustomUIKeyboard>();

                _customKeyboard.textKeyWasPressedEvent += delegate (char input) { inputString += input; UpdateInputText(); };
                _customKeyboard.deleteButtonWasPressedEvent += delegate () { inputString = inputString.Substring(0, inputString.Length - 1); UpdateInputText(); };
                _customKeyboard.cancelButtonWasPressedEvent += () => { backButtonPressed?.Invoke(); };
                _customKeyboard.okButtonWasPressedEvent += () => { enterButtonPressed?.Invoke(inputString); };

                _inputText = BeatSaberUI.CreateText(rectTransform, "", new Vector2(0f, 22f));
                _inputText.alignment = TextAlignmentOptions.Center;
                _inputText.fontSize = 6f;

                UpdateInputText();
            }
            else
            {
                inputString = "";
                _inputText.alignment = TextAlignmentOptions.Center;
                UpdateInputText();
            }

        }

        void UpdateInputText()
        {
            if (_inputText != null)
            {
                _inputText.text = inputString?.ToUpper() ?? "";
                if (string.IsNullOrEmpty(inputString) && !allowEmptyInput)
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
            inputString = "";
        }


    }
}
