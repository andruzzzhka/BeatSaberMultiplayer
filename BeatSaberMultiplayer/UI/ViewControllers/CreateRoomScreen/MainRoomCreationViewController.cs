using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.UIElements;
using BS_Utils.Gameplay;
using CustomUI.BeatSaber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen
{
    class MainRoomCreationViewController : VRUIViewController
    {
        public event Action didFinishEvent;
        public event Action<CustomKeyboardViewController> keyboardDidFinishEvent;
        public event Action<CustomKeyboardViewController> presentKeyboardEvent;
        public event Action<RoomSettings> CreatedRoom;
        public event Action<RoomSettings, string> SavePresetPressed;
        public event Action LoadPresetPressed;

        private Button _backButton;
        private Button _editNameButton;
        private Button _editPasswordButton;
        private OnOffViewController _usePasswordToggle;
        private MultiplayerListViewController _songSelectionList;
        private MultiplayerListViewController _maxPlayersList;
        private MultiplayerListViewController _resultsShowTimeList;
        private OnOffViewController _perPlayerDifficultyToggle;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _passwordText;

        private Button _createRoomButton;
        private Button _loadPresetButton;
        private Button _savePresetButton;

        private CustomKeyboardViewController _nameKeyboard;
        private CustomKeyboardViewController _passwordKeyboard;
        private CustomKeyboardViewController _presetNameKeyboard;

        private string _presetName;

        private string _roomName;
        private string _roomPassword;
        private SongSelectionType _songSelectionType;
        private int _maxPlayers = 0;
        private int _resultsShowTime = 15;
        private bool _usePassword = false;
        private bool _allowPerPlayerDifficulty = true;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform);
                _backButton.onClick.AddListener(delegate () { didFinishEvent?.Invoke(); });

                _nameKeyboard = BeatSaberUI.CreateViewController<CustomKeyboardViewController>();
                _nameKeyboard.enterButtonPressed += NameEntered;
                _nameKeyboard.backButtonPressed += () => { keyboardDidFinishEvent?.Invoke(_nameKeyboard); };

                _passwordKeyboard = BeatSaberUI.CreateViewController<CustomKeyboardViewController>();
                _passwordKeyboard.allowEmptyInput = true;
                _passwordKeyboard.enterButtonPressed += PasswordEntered;
                _passwordKeyboard.backButtonPressed += () => { keyboardDidFinishEvent?.Invoke(_passwordKeyboard); };

                _presetNameKeyboard = BeatSaberUI.CreateViewController<CustomKeyboardViewController>();
                _presetNameKeyboard.enterButtonPressed += PresetNameEntered;
                _presetNameKeyboard.backButtonPressed += () => { keyboardDidFinishEvent?.Invoke(_presetNameKeyboard); };

                _usePasswordToggle = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Use Password", new Vector2(0f, 11f));
                _usePasswordToggle.ValueChanged += UsePasswordToggle_ValueChanged;
                _usePasswordToggle.Value = _usePassword;

                _songSelectionList = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>(rectTransform, "Song Selection", new Vector2(0f, 3f));
                _songSelectionList.ValueChanged += SongSelection_ValueChanged;
                _songSelectionList.maxValue = 1;
                _songSelectionList.Value = (int)_songSelectionType;
                _songSelectionList.textForValues = new string[] { "Manual", "Random" };
                _songSelectionList.UpdateText();

                _maxPlayersList = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>(rectTransform, "Max Players", new Vector2(0f, -5f));
                _maxPlayersList.ValueChanged += MaxPlayers_ValueChanged;
                _maxPlayersList.Value = _maxPlayers;
                _maxPlayersList.maxValue = 16;
                _maxPlayersList.textForValues = new string[] { "No limit" };
                _maxPlayersList.UpdateText();

                _resultsShowTimeList = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>(rectTransform, "Results Show Time", new Vector2(0f, -13f));
                _resultsShowTimeList.ValueChanged += ResultsShowTime_ValueChanged;
                _resultsShowTimeList.minValue = 5;
                _resultsShowTimeList.maxValue = 45;
                _resultsShowTimeList.Value = _resultsShowTime;

                _perPlayerDifficultyToggle = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Per player difficulty", new Vector2(0f, -21f));
                _perPlayerDifficultyToggle.ValueChanged += PerPlayerDifficultyToggle_ValueChanged;
                _perPlayerDifficultyToggle.Value = _allowPerPlayerDifficulty;

                _roomName = $"{GetUserInfo.GetUserName()}'s room".ToUpper();
                _nameText = BeatSaberUI.CreateText(rectTransform, _roomName, new Vector2(-22.5f, 30f));
                _nameText.fontSize = 5f;

                _editNameButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton");
                _editNameButton.SetButtonText("EDIT NAME");
                (_editNameButton.transform as RectTransform).sizeDelta = new Vector2(34f, 8f);
                (_editNameButton.transform as RectTransform).anchoredPosition = new Vector2(34f, 32f);
                _editNameButton.onClick.RemoveAllListeners();
                _editNameButton.onClick.AddListener(delegate ()
                {
                    _nameKeyboard.inputString = _roomName;
                    presentKeyboardEvent?.Invoke(_nameKeyboard);
                });
                
                _roomPassword = "";
                _passwordText = BeatSaberUI.CreateText(rectTransform, "ENTER PASSWORD", new Vector2(-22.5f, 20f));
                _passwordText.fontSize = 5f;

                _editPasswordButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton");
                _editPasswordButton.SetButtonText("EDIT PASS");
                (_editPasswordButton.transform as RectTransform).sizeDelta = new Vector2(34f, 8f);
                (_editPasswordButton.transform as RectTransform).anchoredPosition = new Vector2(34f, 22f);
                _editPasswordButton.onClick.RemoveAllListeners();
                _editPasswordButton.onClick.AddListener(delegate ()
                {
                    _passwordKeyboard.inputString = _roomPassword;
                    presentKeyboardEvent?.Invoke(_passwordKeyboard);
                });

                _createRoomButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton");
                _createRoomButton.SetButtonText("Create Room");
                (_createRoomButton.transform as RectTransform).sizeDelta = new Vector2(30f, 10f);
                (_createRoomButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -32.5f);
                _createRoomButton.onClick.RemoveAllListeners();
                _createRoomButton.onClick.AddListener(delegate () {
                    CreatedRoom?.Invoke(new RoomSettings() { Name = _roomName, UsePassword = _usePassword, Password = _roomPassword, PerPlayerDifficulty = _allowPerPlayerDifficulty, MaxPlayers = _maxPlayers, SelectionType = _songSelectionType, ResultsShowTime = _resultsShowTime});
                });
                _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();

                _savePresetButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton");
                _savePresetButton.SetButtonText("Save Preset");
                (_savePresetButton.transform as RectTransform).sizeDelta = new Vector2(30f, 10f);
                (_savePresetButton.transform as RectTransform).anchoredPosition = new Vector2(-32f, -32.5f);
                _savePresetButton.onClick.RemoveAllListeners();
                _savePresetButton.onClick.AddListener(delegate () {
                    _presetNameKeyboard.inputString = "NEW PRESET";
                    presentKeyboardEvent?.Invoke(_presetNameKeyboard);
                });

                _loadPresetButton = BeatSaberUI.CreateUIButton(rectTransform, "CancelButton");
                _loadPresetButton.SetButtonText("Load Preset");
                (_loadPresetButton.transform as RectTransform).sizeDelta = new Vector2(30f, 10f);
                (_loadPresetButton.transform as RectTransform).anchoredPosition = new Vector2(32f, -32.5f);
                _loadPresetButton.onClick.RemoveAllListeners();
                _loadPresetButton.onClick.AddListener(delegate () {
                    LoadPresetPressed?.Invoke();
                });
            }
            else
            {
                _songSelectionList.textForValues = new string[] { "Manual", "Random" };
                _songSelectionList.UpdateText();
                _maxPlayersList.textForValues = new string[] { "No limit" };
                _maxPlayersList.UpdateText();
                _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
            }

        }

        public void ApplyRoomSettings(RoomSettings settings)
        {
            _roomName = settings.Name;

            _usePassword = settings.UsePassword;
            _usePasswordToggle.Value = _usePassword;

            _roomPassword = settings.Password;

            _allowPerPlayerDifficulty = settings.PerPlayerDifficulty;
            _perPlayerDifficultyToggle.Value = _allowPerPlayerDifficulty;

            _maxPlayers = settings.MaxPlayers;
            _maxPlayersList.Value = _maxPlayers;

            _resultsShowTime = (int)settings.ResultsShowTime;
            _resultsShowTimeList.Value = _resultsShowTime;

            _songSelectionType = settings.SelectionType;
            _songSelectionList.Value = (int)_songSelectionType;
            _songSelectionList.textForValues = new string[] { "Manual", "Random" };
            _songSelectionList.UpdateText();

            _passwordText.text = string.IsNullOrEmpty(_roomPassword) ? "ENTER PASSWORD" : _roomPassword.ToUpper();
            _nameText.text = string.IsNullOrEmpty(_roomName) ? "ENTER ROOM NAME" : _roomName.ToUpper();

            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
        }

        private void SongSelection_ValueChanged(int obj)
        {
            _songSelectionType = (SongSelectionType)obj;
        }

        private void MaxPlayers_ValueChanged(int obj)
        {
            _maxPlayers = obj;
        }

        private void ResultsShowTime_ValueChanged(int obj)
        {
            _resultsShowTime = obj;
        }

        private void PasswordEntered(string obj)
        {
            keyboardDidFinishEvent?.Invoke(_passwordKeyboard);
            _roomPassword = obj?.ToUpper() ?? "";
            _passwordText.text = string.IsNullOrEmpty(obj) ? "ENTER PASSWORD" : obj.ToUpper();
            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
        }

        private void NameEntered(string obj)
        {
            keyboardDidFinishEvent?.Invoke(_nameKeyboard);
            _roomName = obj?.ToUpper() ?? "";
            _nameText.text = string.IsNullOrEmpty(obj) ? "ENTER ROOM NAME" : obj.ToUpper();
            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
        }

        private void PresetNameEntered(string obj)
        {
            keyboardDidFinishEvent?.Invoke(_presetNameKeyboard);
            _presetName = obj.ToUpper();
            SavePresetPressed?.Invoke(new RoomSettings() { Name = _roomName, UsePassword = _usePassword, Password = _roomPassword, PerPlayerDifficulty = _allowPerPlayerDifficulty, MaxPlayers = _maxPlayers, SelectionType = _songSelectionType, ResultsShowTime = _resultsShowTime }, _presetName);

        }

        private void PerPlayerDifficultyToggle_ValueChanged(bool value)
        {
            _allowPerPlayerDifficulty = value;
        }

        private void UsePasswordToggle_ValueChanged(bool value)
        {
            _usePassword = value;
            _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
        }

        public void Update()
        {
            if(isInViewControllerHierarchy)
                _createRoomButton.interactable = PluginUI.instance.roomCreationFlowCoordinator.CheckRequirements();
        }

        public void CreateButtonInteractable(bool interactable)
        {
            _createRoomButton.interactable = interactable;
        }

        public bool CheckRequirements()
        {
            return (!_usePassword || !string.IsNullOrEmpty(_roomPassword)) && !string.IsNullOrEmpty(_roomName);
        }
        
    }
}
