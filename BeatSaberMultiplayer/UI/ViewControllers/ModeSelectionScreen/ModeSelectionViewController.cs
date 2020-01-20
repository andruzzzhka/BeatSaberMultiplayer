using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Misc;
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.ViewControllers.ModeSelectionScreen
{
    class ModeSelectionViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

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
        [UIComponent("loading-progress-text")]
        TextMeshProUGUI _loadingProgressText;

        [UIComponent("missing-files-rect")]
        RectTransform _missingFilesRect;
        [UIComponent("buttons-rect")]
        RectTransform _buttonsRect;
        [UIComponent("avatars-loading-rect")]
        RectTransform _avatarsLoadingRect;

        [UIComponent("progress-bar-top")]
        RawImage _progressBarTop;
        [UIComponent("progress-bar-bg")]
        RawImage _progressBarBG;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);
            if (firstActivation)
            {
                _radioButton.interactable = false;

                foreach (string path in _dllPaths)
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

                if (ModelSaberAPI.isCalculatingHashes)
                {
                    _buttonsRect.gameObject.SetActive(false);
                    _avatarsLoadingRect.gameObject.SetActive(true);
                    ModelSaberAPI.hashesCalculated += ModelSaberAPI_hashesCalculated;

                    _progressBarBG.color = new Color(1f, 1f, 1f, 0.2f);
                    _progressBarTop.color = new Color(1f, 1f, 1f, 1f);

                }
                else
                    _avatarsLoadingRect.gameObject.SetActive(false);

                var pluginVersion = IPA.Loader.PluginManager.GetPlugin("Beat Saber Multiplayer").Metadata.Version.ToString();
                var pluginBuild = pluginVersion.Substring(pluginVersion.LastIndexOf('.') + 1);

                _versionText.text = $"v{pluginVersion}{(!int.TryParse(pluginBuild, out var buildNumber) ? " <color=red>(UNSTABLE)</color>" : "")}";
            }
        }

        private void Update()
        {
            if (ModelSaberAPI.isCalculatingHashes)
            {
                _loadingProgressText.text = $"{ModelSaberAPI.calculatedHashesCount} out of {ModelSaberAPI.totalAvatarsCount}";
                _progressBarTop.rectTransform.sizeDelta = new Vector2(60f * (ModelSaberAPI.calculatedHashesCount / (float)ModelSaberAPI.totalAvatarsCount), 6f);
            }
        }

        private void ModelSaberAPI_hashesCalculated()
        {
            _buttonsRect.gameObject.SetActive(true); 
            _avatarsLoadingRect.gameObject.SetActive(false);
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
