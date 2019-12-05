using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.UIElements;
using BS_Utils.Gameplay;
using CustomUI.BeatSaber;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen
{
    class MainRoomCreationViewController : BSMLResourceViewController
    {
        public override string ResourceName => "BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen.MainRoomCreationViewController";

        public event Action didFinishEvent;
        public event Action<KeyboardViewController> keyboardDidFinishEvent;
        public event Action<KeyboardViewController> presentKeyboardEvent;
        public event Action<RoomSettings> CreatedRoom;
        public event Action<RoomSettings, string> SavePresetPressed;
        public event Action LoadPresetPressed;

        private KeyboardViewController _presetNameKeyboard;
        private KeyboardViewController _roomNameKeyboard;
        private KeyboardViewController _roomPasswordKeyboard;

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
        private int _maxPlayers = 0;
        [UIValue("results-show-time")]
        private int _resultsShowTime = 15;
        [UIValue("use-password")]
        private bool _usePassword = false;
        [UIValue("per-player-difficulty")]
        private bool _allowPerPlayerDifficulty = true;

        [UIComponent("create-room-btn")]
        private Button _createRoomButton;

        /*
        [UIComponent("room-name-setting")]
        private StringSetting _roomNameSetting;
        [UIComponent("room-password-setting")]
        private StringSetting _roomPasswordSetting;
        [UIComponent("use-password-setting")]
        private CheckboxSetting _usePasswordSetting;
        [UIComponent("song-selection-setting")]
        private DropDownListSetting _songSelectionTypeSetting;
        [UIComponent("max-players-setting")]
        private SliderSetting _maxPlayerSetting;
        [UIComponent("results-show-time-setting")]
        private SliderSetting _resultsShowTimeSetting;
        [UIComponent("per-player-diff-setting")]
        private CheckboxSetting _perPlayerDiffSetting;
        */

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);

            if(firstActivation)
            {
                _presetNameKeyboard = BeatSaberUI.CreateViewController<KeyboardViewController>();
                _presetNameKeyboard.enterPressed = null;
                _presetNameKeyboard.enterPressed += PresetNameEntered;

                _roomNameKeyboard = BeatSaberUI.CreateViewController<KeyboardViewController>();
                _roomNameKeyboard.enterPressed = null;
                _roomNameKeyboard.enterPressed += NameEntered;

                _roomPasswordKeyboard = BeatSaberUI.CreateViewController<KeyboardViewController>();
                _roomPasswordKeyboard.enterPressed = null;
                _roomPasswordKeyboard.enterPressed += PasswordEntered;

                _roomName = $"{GetUserInfo.GetUserName()}'s room".ToUpper();
                _roomPassword = "";
            }

            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
            parserParams.EmitEvent("cancel");
        }

        public void ApplyRoomSettings(RoomSettings settings)
        {
            _roomName = settings.Name;

            _usePassword = settings.UsePassword;
            _roomPassword = settings.Password;
            _allowPerPlayerDifficulty = settings.PerPlayerDifficulty;
            _maxPlayers = settings.MaxPlayers;

            _resultsShowTime = (int)settings.ResultsShowTime;

            _songSelectionType = settings.SelectionType;

            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();

            parserParams.EmitEvent("cancel");
        }
        
        private void PasswordEntered(string obj)
        {
            keyboardDidFinishEvent?.Invoke(_roomPasswordKeyboard);
            _roomPassword = obj?.ToUpper() ?? "";
            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
            parserParams.EmitEvent("cancel");
        }

        private void NameEntered(string obj)
        {
            keyboardDidFinishEvent?.Invoke(_roomNameKeyboard);
            _roomName = obj?.ToUpper() ?? "";
            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
            parserParams.EmitEvent("cancel");
        }

        private void PresetNameEntered(string obj)
        {
            keyboardDidFinishEvent?.Invoke(_presetNameKeyboard);
            _presetName = obj.ToUpper();
            if(!string.IsNullOrEmpty(_presetName))
                SavePresetPressed?.Invoke(new RoomSettings() { Name = _roomName, UsePassword = _usePassword, Password = _roomPassword, PerPlayerDifficulty = _allowPerPlayerDifficulty, MaxPlayers = _maxPlayers, SelectionType = _songSelectionType, ResultsShowTime = _resultsShowTime }, _presetName);
        }

        public void SetCreateButtonInteractable(bool interactable)
        {
            if (_createRoomButton != null)
                _createRoomButton.interactable = interactable;
        }

        public void Update()
        {
            if(isInViewControllerHierarchy && _createRoomButton != null)
                _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
        }

        public bool CheckRequirements()
        {
            return (!_usePassword || !string.IsNullOrEmpty(_roomPassword)) && !string.IsNullOrEmpty(_roomName);
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

        [UIAction("on-edit-room-password")]
        private void OnEditRoomPasswordPressed()
        {
            _roomPasswordKeyboard.startingText = _roomPassword;
            presentKeyboardEvent?.Invoke(_roomPasswordKeyboard);
        }

        [UIAction("on-edit-room-name")]
        private void OnEditRoomNamePressed()
        {
            _roomNameKeyboard.startingText = _roomName;
            presentKeyboardEvent?.Invoke(_roomNameKeyboard);
        }

        [UIAction("save-preset-btn-pressed")]
        private void SavePresetBtnPressed()
        {
            _presetNameKeyboard.startingText = "NEW PRESET";
            presentKeyboardEvent?.Invoke(_presetNameKeyboard);
        }

        [UIAction("load-preset-btn-pressed")]
        private void LoadPresetBtnPressed()
        {
            LoadPresetPressed?.Invoke();
        }

        [UIAction("create-room-btn-pressed")]
        private void CreateRoomBtnPressed()
        {
            CreatedRoom?.Invoke(new RoomSettings() { Name = _roomName, UsePassword = _usePassword, Password = _roomPassword, PerPlayerDifficulty = _allowPerPlayerDifficulty, MaxPlayers = _maxPlayers, SelectionType = _songSelectionType, ResultsShowTime = _resultsShowTime });
        }

        [UIAction("back-btn-pressed")]
        private void BackBtnPressed()
        {
            didFinishEvent?.Invoke();
        }
    }
}
