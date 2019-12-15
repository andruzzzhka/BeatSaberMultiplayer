using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.ViewControllers.ModeSelectionScreen
{
    class ModeSelectionViewController : BSMLResourceViewController
    {
        public override string ResourceName => "BeatSaberMultiplayer.UI.ViewControllers.ModeSelectionScreen.ModeSelectionViewController";

        public event Action didSelectRooms;
        public event Action didSelectRadio;
        public event Action didFinishEvent;

        private string[] _dllPaths = new[] { @"Libs\Lidgren.Network.2012.1.7.0.dll", @"Libs\NSpeex.1.1.1.0.dll" };
        private bool _filesMising;
        private string _missingFilesString = "Missing DLL files:";


        [UIComponent("rooms-button")]
        Button _roomsButton;
        [UIComponent("radio-button")]
        Button _radioButton;
        [UIComponent("missing-files-text")]
        TextMeshProUGUI _missingFilesText;
        [UIComponent("version-text")]
        TextMeshProUGUI _versionText;

        [UIComponent("missing-files-rect")]
        RectTransform _missingFilesRect;
        [UIComponent("buttons-rect")]
        RectTransform _buttonsRect;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);
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

                _missingFilesText.color = Color.red;

                if (_filesMising)
                {
                    _missingFilesRect.gameObject.SetActive(true);
                    _buttonsRect.gameObject.SetActive(false);
                }
                else
                {
                    _missingFilesRect.gameObject.SetActive(false);
                    _buttonsRect.gameObject.SetActive(true);
                }

                _versionText.text = "v" + IPA.Loader.PluginManager.GetPlugin("Beat Saber Multiplayer").Metadata.Version.ToString();
            }
        }

        [UIAction("rooms-btn-pressed")]
        private void RoomsBtnPressed()
        {
            didSelectRooms?.Invoke();
        }

        [UIAction("radio-btn-pressed")]
        private void RadioBtnPressed()
        {
            didSelectRadio?.Invoke();
        }

        [UIAction("back-btn-pressed")]
        private void BackBtnPressed()
        {
            didFinishEvent?.Invoke();
        }
    }
}
