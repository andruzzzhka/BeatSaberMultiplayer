using CustomUI.BeatSaber;
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers
{
    class ModeSelectionViewController : VRUIViewController
    {
        public event Action didSelectRooms;
        public event Action didSelectRadio;

        private string[] _dllPaths = new[] { @"Libs\Lidgren.Network.2012.1.7.0.dll", @"Libs\NSpeex.1.1.1.0.dll" };
        private bool _filesMising;
        private string _missingFilesString = "Missing DLL files:";

        Button _roomsButton;
        Button _radioButton;
        TextMeshProUGUI _missingFilesText;
        TextMeshProUGUI _versionText;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {

                foreach(string path in _dllPaths)
                {
                    if (!File.Exists(path))
                    {
                        _filesMising = true;
                        _missingFilesString += "\n" + path;
                    }
                }

                if (_filesMising)
                {
                    _missingFilesText = BeatSaberUI.CreateText(rectTransform, _missingFilesString, new Vector2(0f, 0f));
                    _missingFilesText.alignment = TextAlignmentOptions.Center;
                    _missingFilesText.color = Color.red;
                    _missingFilesText.fontSize = 6f;
                }
                else
                {
                    _roomsButton = BeatSaberUI.CreateUIButton(rectTransform, "SoloFreePlayButton", new Vector2(-40f, 10f), () => { didSelectRooms?.Invoke(); }, "ROOMS");
                    Destroy(_roomsButton.GetComponentInChildren<HoverHint>());
                    _roomsButton.SetButtonIcon(Sprites.roomsIcon);

                    _radioButton = BeatSaberUI.CreateUIButton(rectTransform, "SoloFreePlayButton", new Vector2(10f, 10f), () => { didSelectRadio?.Invoke(); }, "RADIO");
                    Destroy(_radioButton.GetComponentInChildren<HoverHint>());
                    _radioButton.SetButtonIcon(Sprites.radioIcon);
                }
                _versionText = BeatSaberUI.CreateText(rectTransform, "v" + IPA.Loader.PluginManager.GetPlugin("Beat Saber Multiplayer").Metadata.Version.ToString(), new Vector2(45f, -35f));
                _versionText.alignment = TextAlignmentOptions.Right;
                _versionText.color = Color.white;
                _versionText.fontSize = 4.5f;
            }
        }
    }
}
