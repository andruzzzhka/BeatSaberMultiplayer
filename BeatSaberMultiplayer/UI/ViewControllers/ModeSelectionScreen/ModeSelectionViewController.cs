using CustomUI.BeatSaber;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                    _roomsButton = BeatSaberUI.CreateUIButton(rectTransform, "SoloFreePlayButton", new Vector2(-5f, 10f), () => { didSelectRooms?.Invoke(); }, "ROOMS");
                    _roomsButton.SetButtonIcon(Sprites.roomsIcon);

                    _radioButton = BeatSaberUI.CreateUIButton(rectTransform, "SoloFreePlayButton", new Vector2(45f, 10f), () => { didSelectRadio?.Invoke(); }, "RADIO");
                    _radioButton.SetButtonIcon(Sprites.radioIcon);
                }
            }
        }
    }
}
