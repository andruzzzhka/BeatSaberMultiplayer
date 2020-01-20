using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMultiplayer.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatSaberMultiplayer.UI
{
    class Settings : MonoBehaviour
    {
        public static event Action<string> voiceChatMicrophoneChanged;

        public void Awake()
        {
            if (ModelSaberAPI.isCalculatingHashes)
            {
                ModelSaberAPI.hashesCalculated -= ListAllAvatars;
                ModelSaberAPI.hashesCalculated += ListAllAvatars;
            }
            else
                ListAllAvatars();

            AudioSettings.OnAudioConfigurationChanged += UpdateMicrophoneList;
            UpdateMicrophoneList(false);
        }

        #region General settings

        void ListAllAvatars()
        {
            ModelSaberAPI.hashesCalculated -= ListAllAvatars;
            publicAvatars.Clear();
            foreach (var avatar in CustomAvatar.Plugin.Instance.AvatarLoader.Avatars)
            {
                if (avatar.IsLoaded)
                {
                    publicAvatars.Add(avatar);
                }
            }

            if (publicAvatarSetting)
            {
                publicAvatarSetting.tableView.ReloadData();
                publicAvatarSetting.ReceiveValue();
            }
        }

        CustomAvatar.CustomAvatar GetSelectedAvatar()
        {
            if (ModelSaberAPI.cachedAvatars.TryGetValue(Config.Instance.PublicAvatarHash, out CustomAvatar.CustomAvatar avatar))
            {
                return avatar;
            }
            else
            {
                return CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault();
            }
        }

        [UIComponent("public-avatar-setting")]
        public DropDownListSetting publicAvatarSetting;

        [UIAction("public-avatar-formatter")]
        public string PublicAvatarFormatter(object avatar)
        {
            string name = (avatar as CustomAvatar.CustomAvatar)?.Name;
            return (avatar == null) ? "LOADING AVATARS..." : (string.IsNullOrEmpty(name) ? "NO NAME" : name);
        }

        [UIValue("avatars-in-game")]
        public bool avatarsInGame
        { 
            get { return Config.Instance.ShowAvatarsInGame; }
            set { Config.Instance.ShowAvatarsInGame = value; }
        }

        [UIValue("blocks-in-game")]
        public bool blocksInGame
        {
            get { return Config.Instance.ShowOtherPlayersBlocks; }
            set { Config.Instance.ShowOtherPlayersBlocks = value; }
        }

        [UIValue("avatars-in-room")]
        public bool avatarsInRoom
        {
            get { return Config.Instance.ShowAvatarsInRoom; }
            set { Config.Instance.ShowAvatarsInRoom = value; }
        }

        [UIValue("download-avatars")]
        public bool downloadAvatars
        {
            get { return Config.Instance.DownloadAvatars; }
            set { Config.Instance.DownloadAvatars = value; }
        }

        [UIValue("separate-avatar")]
        public bool separateAvatar
        {
            get { return Config.Instance.SeparateAvatarForMultiplayer; }
            set { InGameOnlineController.Instance.SetSeparatePublicAvatarState(value); }
        }

        [UIValue("public-avatar-value")]
        public object publicAvater
        {
            get { return GetSelectedAvatar(); }
            set { InGameOnlineController.Instance.SetSeparatePublicAvatarHash(ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value == (value as CustomAvatar.CustomAvatar)).Key); }
        }

        [UIValue("public-avatar-options")]
        public List<object> publicAvatars = new List<object>() { null };

        [UIValue("spectator-mode")]
        public bool spectatorMode
        {
            get { return Config.Instance.SpectatorMode; }
            set { Config.Instance.SpectatorMode = value; }
        }
        
        [UIValue("submit-scores-options")]
        public List<object> submitScoresOptions = new List<object>() { "Never", "Only ranked", "Always" };

        [UIValue("submit-scores-value")]
        public object submitScores
        {
            get { return submitScoresOptions[Config.Instance.SubmitScores]; }
            set { Config.Instance.SubmitScores = submitScoresOptions.IndexOf(value); }
        }
        #endregion

        #region Voice settings

        public void UpdateMicrophoneList(bool deviceWasChanged)
        {
            micSelectOptions.Clear();
            micSelectOptions.Add("DEFAULT MIC");
            foreach (var mic in Microphone.devices)
            {
                micSelectOptions.Add(mic);
            }

            if (micSelectSetting)
            {
                micSelectSetting.tableView.ReloadData();
                micSelectSetting.ReceiveValue();
            }

            voiceChatMicrophoneChanged?.Invoke(Config.Instance.VoiceChatMicrophone);
        }

        [UIComponent("mic-select-setting")]
        public DropDownListSetting micSelectSetting;

        [UIValue("enable-voice-chat")]
        public bool enableVoiceChat
        {
            get { return Config.Instance.EnableVoiceChat; }
            set { Config.Instance.EnableVoiceChat = value; }
        }

        [UIValue("voice-chat-volume")]
        public int voiceChatVolume
        {
            get { return Mathf.RoundToInt(Config.Instance.VoiceChatVolume * 100); }
            set { Config.Instance.VoiceChatVolume = (value / 100f); }
        }

        [UIValue("mic-enabled")]
        public bool micEnabled
        {
            get { return Config.Instance.MicEnabled; }
            set { Config.Instance.MicEnabled = value; }
        }

        [UIValue("push-to-talk")]
        public bool pushToTalk
        {
            get { return Config.Instance.PushToTalk; }
            set { Config.Instance.PushToTalk = value; }
        }

        [UIValue("ptt-button-options")]
        public List<object> pttButtonOptions = new List<object>() { "L Grip", "R Grip", "L Trigger", "R Trigger", "L+R Grip", "L+R Trigger", "Any Grip", "Any Trigger" };

        [UIValue("ptt-button-value")]
        public object pttButton
        {
            get { return pttButtonOptions[Config.Instance.PushToTalkButton]; }
            set { Config.Instance.PushToTalkButton = pttButtonOptions.IndexOf(value); }
        }

        [UIValue("mic-select-options")]
        public List<object> micSelectOptions = new List<object>() { "DEFAULT MIC" };

        [UIValue("mic-select-value")]
        public object micSelect
        {
            get
            {
                if(!string.IsNullOrEmpty(Config.Instance.VoiceChatMicrophone) && micSelectOptions.Contains((object)Config.Instance.VoiceChatMicrophone))
                {
                    return (object)Config.Instance.VoiceChatMicrophone;
                }
                else
                    return "DEFAULT MIC";
            }
            set
            {
                if (string.IsNullOrEmpty(value as string) || (value as string) == "DEFAULT MIC")
                {
                    Config.Instance.VoiceChatMicrophone = null;
                }
                else
                    Config.Instance.VoiceChatMicrophone = (value as string);

                voiceChatMicrophoneChanged?.Invoke(Config.Instance.VoiceChatMicrophone);
            }
        }

        #endregion
    }
}
