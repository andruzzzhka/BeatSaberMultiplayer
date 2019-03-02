﻿using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.UIElements;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class QuickSettingsViewController : VRUIViewController
    {
        private List<GameObject> _generalSettings = new List<GameObject>();
        private List<GameObject> _voiceSettings = new List<GameObject>();

        private TextSegmentedControl _settingsSegments;
        private MultiplayerListViewController _publicAvatarOption;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                _settingsSegments = new GameObject("CustomSegmentedControl", typeof(RectTransform)).AddComponent<TextSegmentedControl>();
                _settingsSegments.gameObject.AddComponent<HorizontalLayoutGroup>();

                TextSegmentedControlCellNew[] _segments = Resources.FindObjectsOfTypeAll<TextSegmentedControlCellNew>();
                
                _settingsSegments.SetPrivateField("_firstCellPrefab", _segments.First(x => x.name == "LeftTextSegmentedControlCell"));
                _settingsSegments.SetPrivateField("_middleCellPrefab", _segments.First(x => x.name == "HMiddleTextSegmentedControlCell"));
                _settingsSegments.SetPrivateField("_lastCellPrefab", _segments.First(x => x.name == "RightTextSegmentedControlCell"));
                
                _settingsSegments.transform.SetParent(rectTransform, false);
                (_settingsSegments.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_settingsSegments.transform as RectTransform).anchorMin = new Vector2(0f, 1f);
                (_settingsSegments.transform as RectTransform).anchoredPosition = new Vector2(0f, -9f);
                (_settingsSegments.transform as RectTransform).sizeDelta = new Vector2(-20f, 7f);

                _settingsSegments.SetPrivateField("_fontSize", 4f);
                _settingsSegments.SetPrivateField("_padding", 8f);
                _settingsSegments.SetTexts(new string[] { "General", "Voice" });

                _settingsSegments.didSelectCellEvent += SettingsCellSelected;

                #region General Settings
                int generalSettingsIndex = 0;

                var avatarsInGame = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Show Avatars In Game", new Vector2(0f, 22.5f + -8f * generalSettingsIndex++));
                avatarsInGame.ValueChanged += (value) => { Config.Instance.ShowAvatarsInGame = value; };
                avatarsInGame.Value = Config.Instance.ShowAvatarsInGame;
                _generalSettings.Add(avatarsInGame.gameObject);

                var blocksInGame = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Show Other Players Blocks", new Vector2(0f, 22.5f + -8f * generalSettingsIndex++));
                blocksInGame.ValueChanged += (value) => { Config.Instance.ShowOtherPlayersBlocks = value; };
                blocksInGame.Value = Config.Instance.ShowOtherPlayersBlocks;
                _generalSettings.Add(blocksInGame.gameObject);

                var avatarsInRoom = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Show Avatars In Room", new Vector2(0f, 22.5f + -8f * generalSettingsIndex++));
                avatarsInRoom.ValueChanged += (value) => { Config.Instance.ShowAvatarsInRoom = value; };
                avatarsInRoom.Value = Config.Instance.ShowAvatarsInRoom;
                _generalSettings.Add(avatarsInRoom.gameObject);

                var downloadAvatars = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Download Other Players Avatars", new Vector2(0f, 22.5f + -8f * generalSettingsIndex++));
                downloadAvatars.ValueChanged += (value) => { Config.Instance.DownloadAvatars = value; };
                downloadAvatars.Value = Config.Instance.DownloadAvatars;
                _generalSettings.Add(downloadAvatars.gameObject);

                var separateAvatar = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Separate Avatar For Multiplayer", new Vector2(0f, 22.5f + -8f * generalSettingsIndex++));
                separateAvatar.ValueChanged += (value) => { InGameOnlineController.Instance.SetSeparatePublicAvatarState(value); };
                separateAvatar.Value = Config.Instance.SeparateAvatarForMultiplayer;
                _generalSettings.Add(separateAvatar.gameObject);

                _publicAvatarOption = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>(rectTransform, "Public Avatar", new Vector2(0f, 22.5f + -8f * generalSettingsIndex++));
                _publicAvatarOption.OnEnable();
                _publicAvatarOption.ValueChanged += (e) => { InGameOnlineController.Instance.SetSeparatePublicAvatarHash(ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == CustomAvatar.Plugin.Instance.AvatarLoader.Avatars[e]).Key); };
                _publicAvatarOption.maxValue = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.Count - 1;
                _publicAvatarOption.textForValues = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.Select(x => (string.IsNullOrEmpty(x.Name) ? "" : x.Name)).ToArray();

                if (ModelSaberAPI.cachedAvatars.TryGetValue(Config.Instance.PublicAvatarHash, out CustomAvatar.CustomAvatar avatar))
                {
                    _publicAvatarOption.Value = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.ToList().IndexOf(avatar);
                }
                else
                {
                    if (ModelSaberAPI.isCalculatingHashes)
                    {
                        ModelSaberAPI.hashesCalculated -= UpdateSelectedAvatar;
                        ModelSaberAPI.hashesCalculated += UpdateSelectedAvatar;
                    }
                    _publicAvatarOption.Value = 0;
                }

                _publicAvatarOption.UpdateText();
                _generalSettings.Add(_publicAvatarOption.gameObject);

                var spectatorMode = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Spectator Mode", new Vector2(0f, 22.5f + -8f * generalSettingsIndex++));
                spectatorMode.Value = Config.Instance.SpectatorMode;
                spectatorMode.ValueChanged += (value) => { Config.Instance.SpectatorMode = value; };
                _generalSettings.Add(spectatorMode.gameObject);

                #endregion

                #region Voice Settings
                int voiceSettingsIndex = 0;

                var voiceEnabled = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Enable Voice Chat", new Vector2(0f, 22.5f + -8f * voiceSettingsIndex++));
                voiceEnabled.ValueChanged += (value) => { InGameOnlineController.Instance.ToggleVoiceChat(value); };
                voiceEnabled.Value = Config.Instance.EnableVoiceChat;
                _voiceSettings.Add(voiceEnabled.gameObject);

                var voiceVolume = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>(rectTransform, "Voice Chat Volume", new Vector2(0f, 22.5f + -8f * voiceSettingsIndex++));
                voiceVolume.Value = (int)(Config.Instance.VoiceChatVolume * 20f);
                voiceVolume.ValueChanged += delegate (int value) { Config.Instance.VoiceChatVolume = value / 20f; InGameOnlineController.Instance.VoiceChatVolumeChanged(value / 20f); };
                voiceVolume.maxValue = 20;
                _voiceSettings.Add(voiceVolume.gameObject);

                var micEnabled = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Enable Microphone", new Vector2(0f, 22.5f + -8f * voiceSettingsIndex++));
                micEnabled.Value = Config.Instance.MicEnabled;
                micEnabled.ValueChanged += delegate (bool value) { Config.Instance.MicEnabled = value; };
                _voiceSettings.Add(micEnabled.gameObject);

                var spatialAudio = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Spatial Audio", new Vector2(0f, 22.5f + -8f * voiceSettingsIndex++));
                spatialAudio.Value = Config.Instance.SpatialAudio;
                spatialAudio.ValueChanged += delegate (bool value) { Config.Instance.SpatialAudio = value; InGameOnlineController.Instance.VoiceChatSpatialAudioChanged(value); };
                _voiceSettings.Add(spatialAudio.gameObject);

                var pushToTalk = CustomSettingsHelper.AddToggleSetting<OnOffViewController>(rectTransform, "Push to Talk", new Vector2(0f, 22.5f + -8f * voiceSettingsIndex++));
                pushToTalk.Value = Config.Instance.PushToTalk;
                pushToTalk.ValueChanged += delegate (bool value) { Config.Instance.PushToTalk = value; };
                _voiceSettings.Add(pushToTalk.gameObject);

                var pushToTalkButton = CustomSettingsHelper.AddListSetting<MultiplayerListViewController>(rectTransform, "Push to Talk Button", new Vector2(0f, 22.5f + -8f * voiceSettingsIndex++));
                pushToTalkButton.OnEnable();
                pushToTalkButton.ValueChanged += (e) => { Config.Instance.PushToTalkButton = e; };
                pushToTalkButton.maxValue = 7;
                pushToTalkButton.textForValues = new string[] { "L Grip", "R Grip", "L Trigger", "R Trigger", "L+R Grip", "L+R Trigger", "Any Grip", "Any Trigger" };
                pushToTalkButton.Value = Config.Instance.PushToTalkButton;
                pushToTalkButton.UpdateText();
                _voiceSettings.Add(pushToTalkButton.gameObject);

                #endregion

                SettingsCellSelected(null, 0);
            }
        }

        private void SettingsCellSelected(SegmentedControl sender, int selectedIndex)
        {

            foreach (var obj in _generalSettings)
            {
                obj.SetActive(selectedIndex == 0);
            }
            foreach (var obj in _voiceSettings)
            {
                obj.SetActive(selectedIndex == 1);
            }

        }

        void UpdateSelectedAvatar()
        {
            if (ModelSaberAPI.cachedAvatars.TryGetValue(Config.Instance.PublicAvatarHash, out CustomAvatar.CustomAvatar avatar))
            {
                _publicAvatarOption.Value = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.ToList().IndexOf(avatar);
            }
        }
    }
}
