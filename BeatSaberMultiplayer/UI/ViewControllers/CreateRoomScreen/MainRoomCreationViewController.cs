using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BS_Utils.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen
{
    class MainRoomCreationViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action didFinishEvent;
        public event Action<RoomSettings> CreatedRoom;
        public event Action<RoomSettings, string> SavePresetPressed;
        public event Action LoadPresetPressed;

        private string _presetName;

        [UIParams]
        private BSMLParserParams parserParams;

        [UIValue("room-name")]
        private string _roomName;
        [UIValue("room-password")]
        private string _roomPassword;
        [UIValue("song-selection-options")]
        public List<object> _songSelectionOptions = new List<object>() { (object)SongSelectionType.Manual, (object)SongSelectionType.Random };
        [UIValue("song-selection-type")]
        private SongSelectionType _songSelectionType;
        [UIValue("max-players")]
        private int _maxPlayers = 16;
        [UIValue("results-show-time")]
        private int _resultsShowTime = 15;
        [UIValue("use-password")]
        private bool _usePassword = false;
        [UIValue("per-player-difficulty")]
        private bool _allowPerPlayerDifficulty = true;

        [UIComponent("create-room-btn")]
        private Button _createRoomButton;

        [UIComponent("preset-name-keyboard")]
        private ModalKeyboard _presetNameKeyboard;
        [UIComponent("room-name-keyboard")]
        private StringSetting _roomNameKeyboard;
        [UIComponent("password-keyboard")]
        private StringSetting _passwordKeyboard;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);

            if(firstActivation)
            {
                _roomName = $"{GetUserInfo.GetUserName()}'s room".ToUpper();
                _roomPassword = "";
            }

            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
            parserParams.EmitEvent("cancel");
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            parserParams.EmitEvent("closeAllMPModals");
            if (_roomNameKeyboard != null)
            {
                _roomNameKeyboard.modalKeyboard.modalView.Hide(false);
            }
            if (_passwordKeyboard != null)
                _passwordKeyboard.modalKeyboard.modalView.Hide(false);
            base.DidDeactivate(deactivationType);
        }

        public void ApplyRoomSettings(RoomSettings settings)
        {
            _roomName = settings.Name;

            _usePassword = settings.UsePassword && !string.IsNullOrEmpty(settings.Password);
            _roomPassword = settings.Password;
            _allowPerPlayerDifficulty = settings.PerPlayerDifficulty;
            _maxPlayers = settings.MaxPlayers;

            _resultsShowTime = (int)settings.ResultsShowTime;

            _songSelectionType = settings.SelectionType;

            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();

            parserParams.EmitEvent("cancel");
        }

        public void Update()
        {
            if (isInViewControllerHierarchy && _createRoomButton != null)
                _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
        }

        public bool CheckRequirements()
        {
            return (!_usePassword || !string.IsNullOrEmpty(_roomPassword)) && !string.IsNullOrEmpty(_roomName);
        }

        [UIAction("room-password-changed")]
        private void PasswordEntered(string obj)
        {
            _roomPassword = obj?.ToUpper() ?? "";
            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
            parserParams.EmitEvent("cancel");
        }

        [UIAction("room-name-changed")]
        private void NameEntered(string obj)
        {
            _roomName = obj?.ToUpper() ?? "";
            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
            parserParams.EmitEvent("cancel");
        }

        [UIAction("preset-name-entered")]
        private void PresetNameEntered(string obj)
        {
            _presetName = obj.ToUpper();
            if(!string.IsNullOrEmpty(_presetName))
                SavePresetPressed?.Invoke(new RoomSettings() { Name = _roomName, UsePassword = _usePassword, Password = _roomPassword, PerPlayerDifficulty = _allowPerPlayerDifficulty, MaxPlayers = _maxPlayers, SelectionType = _songSelectionType, ResultsShowTime = _resultsShowTime }, _presetName);
        }
        

        public void SetCreateButtonInteractable(bool interactable)
        {
            if (_createRoomButton != null)
                _createRoomButton.interactable = interactable;
        }

        [UIAction("max-players-format")]
        public string MaxPlayersFormatter(float value)
        {
            if (value < float.Epsilon)
            {
                return "No limit";
            }
            return value.ToString("0");
        }

        [UIAction("save-preset-btn-pressed")]
        private void SavePresetBtnPressed()
        {
            _presetNameKeyboard.modalView.Show(true, true);
            _presetNameKeyboard.SetText("NEW PRESET");
        }

        [UIAction("load-preset-btn-pressed")]
        private void LoadPresetBtnPressed()
        {
            LoadPresetPressed?.Invoke();
        }

        [UIAction("create-room-btn-pressed")]
        private void CreateRoomBtnPressed()
        {
            CreatedRoom?.Invoke(new RoomSettings() { Name = _roomName, UsePassword = _usePassword && !string.IsNullOrEmpty(_roomPassword), Password = _roomPassword, PerPlayerDifficulty = _allowPerPlayerDifficulty, MaxPlayers = _maxPlayers, SelectionType = _songSelectionType, ResultsShowTime = _resultsShowTime });
        }
    }
}
