using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using CustomAvatar;
using CustomAvatar.Avatar;
using CustomAvatar.Tracking;
using SongCore.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer
{
    public class AvatarController : MonoBehaviour
    {
        static LoadedAvatar defaultAvatarInstance;

        PlayerUpdate playerInfo;
        ulong playerId;
        string playerAvatarHash;
        string playerName;

        SpawnedAvatar avatar;

        string currentAvatarHash;

        bool unableToSpawnAvatar;
        float retryTimeCounter;

        TextMeshPro playerNameText;
        Image playerSpeakerIcon;

        MultiplayerAvatarInput avatarInput;

        HSBColor nameColor;
        bool rainbowName;

        Camera playerCamera;

        VRCenterAdjust centerAdjust;

        public static void LoadAvatars()
        {
            if (defaultAvatarInstance == null)
            {
                if (Config.Instance.DownloadAvatars)
                {
                    defaultAvatarInstance = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value.fullPath.ToLower().Contains("loading.avatar")).Value;

                    if (defaultAvatarInstance == null)//fallback to multiplayer avatar
                    {
                        defaultAvatarInstance = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value.fullPath.ToLower().Contains("multiplayer.avatar")).Value;
                    }

                    if (defaultAvatarInstance == null)//fallback to default avatar
                    {
                        defaultAvatarInstance = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value.fullPath.ToLower().Contains("templatefullbody.avatar")).Value;
                    }

                    if (defaultAvatarInstance == null)//fallback to ANY avatar
                    {
                        defaultAvatarInstance = ModelSaberAPI.cachedAvatars.FirstOrDefault().Value;
                    }
                }
                else
                {
                    defaultAvatarInstance = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value.fullPath.ToLower().Contains("multiplayer.avatar")).Value;

                    if (defaultAvatarInstance == null)//fallback to default avatar
                    {
                        defaultAvatarInstance = ModelSaberAPI.cachedAvatars.FirstOrDefault(x => x.Value.fullPath.ToLower().Contains("templatefullbody.avatar")).Value;
                    }

                    if (defaultAvatarInstance == null)//fallback to ANY avatar
                    {
                        defaultAvatarInstance = ModelSaberAPI.cachedAvatars.FirstOrDefault().Value;
                    }
                }

            }
        }

        public void Awake()
        {
            InitializeAvatarController();
        }

        void InitializeAvatarController()
        {
#if DEBUG
            Plugin.log.Info("Spawning avatar");
#endif
            centerAdjust = GameObject.FindObjectOfType<VRCenterAdjust>();

            avatarInput = new MultiplayerAvatarInput();

            avatar = AvatarManager.SpawnAvatar(defaultAvatarInstance, avatarInput);

            /*
            exclusionScript = avatar.GameObject.GetComponentsInChildren<AvatarScriptPack.FirstPersonExclusion>().FirstOrDefault();
            if (exclusionScript != null)
                exclusionScript.SetVisible();
            */

            playerNameText = CustomExtensions.CreateWorldText(transform, "INVALID");
            playerNameText.rectTransform.anchoredPosition3D = new Vector3(0f, 0.25f, 0f);
            playerNameText.alignment = TextAlignmentOptions.Center;
            playerNameText.fontSize = 2.5f;

            playerSpeakerIcon = new GameObject("Player Speaker Icon", typeof(Canvas), typeof(CanvasRenderer)).AddComponent<Image>();
            playerSpeakerIcon.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            playerSpeakerIcon.rectTransform.SetParent(transform);
            playerSpeakerIcon.rectTransform.localScale = new Vector3(0.004f, 0.004f, 1f);
            playerSpeakerIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            playerSpeakerIcon.rectTransform.anchoredPosition3D = new Vector3(0f, 0.65f, 0f);
            playerSpeakerIcon.sprite = Sprites.speakerIcon;

            avatar.eventsPlayer.gameObject.transform.SetParent(centerAdjust.transform, false);
            transform.SetParent(centerAdjust.transform, false);

            if (ModelSaberAPI.cachedAvatars.Any(x => x.Value.fullPath == avatar.customAvatar.fullPath))
            {
                currentAvatarHash = ModelSaberAPI.cachedAvatars.First(x => x.Value.fullPath == avatar.customAvatar.fullPath).Key;
            }
            else
            {
                currentAvatarHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            }
        }

        void Update()
        {
            try
            {
                if (playerNameText != null)
                {

                    if (playerCamera == null)
                    {
                        playerCamera = Camera.main;
                    }

                    playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - playerCamera.transform.position);
                    playerSpeakerIcon.rectTransform.rotation = Quaternion.LookRotation(playerSpeakerIcon.rectTransform.position - playerCamera.transform.position);

                    if (rainbowName)
                    {
                        playerNameText.color = HSBColor.ToColor(nameColor);
                        nameColor.h += 0.125f * Time.deltaTime;
                        if (nameColor.h >= 1f)
                        {
                            nameColor.h = 0f;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.log.Warn($"Unable to rotate text to the camera! Exception: {e}");
            }

        }

        void OnDestroy()
        {
            Plugin.log.Debug("Destroying avatar");
            if (avatar != null && avatar.eventsPlayer != null)
            {
                avatar.Destroy();
                avatar = null;
            }
        }

        public void SetPlayerInfo(PlayerInfo _playerInfo, float offset, bool isLocal)
        {
            if (_playerInfo == default)
            {
                if (playerNameText != null) playerNameText.gameObject.SetActive(false);
                if (playerSpeakerIcon != null) playerSpeakerIcon.gameObject.SetActive(false);
                if (avatar != null && avatar.eventsPlayer != null)
                {
                    avatar.Destroy();
                    avatar = null;
                }
                return;
            }

            try
            {

                playerInfo = _playerInfo.updateInfo;
                playerId = _playerInfo.playerId;
                playerAvatarHash = _playerInfo.avatarHash;
                playerName = _playerInfo.playerName;

                if (playerNameText != null && playerSpeakerIcon != null)
                {
                    if (isLocal)
                    {
                        playerNameText.gameObject.SetActive(false);
                        playerSpeakerIcon.gameObject.SetActive(false);
#if !DEBUG
                        if (avatar != null && avatar.eventsPlayer != null)
                        {
                            avatar.Destroy();
                        }
#endif
                    }
                    else
                    {
                        playerNameText.gameObject.SetActive(true);
                        playerNameText.alignment = TextAlignmentOptions.Center;
                        playerSpeakerIcon.gameObject.SetActive(InGameOnlineController.Instance.VoiceChatIsTalking(playerId));
                    }
                }
                else
                {
                    return;
                }
#if !DEBUG
                if ((avatar == null || currentAvatarHash != playerAvatarHash) && !isLocal)
#else
                if ((avatar == null || currentAvatarHash != playerAvatarHash))
#endif
                {
                    if (unableToSpawnAvatar)
                    {
                        if (retryTimeCounter + 5f > Time.realtimeSinceStartup)
                        {
                            retryTimeCounter = -1f;
                            unableToSpawnAvatar = false;
                        }
                    }
                    else
                    {
                        if (ModelSaberAPI.cachedAvatars.ContainsKey(playerAvatarHash))
                        {
                            LoadedAvatar cachedAvatar = ModelSaberAPI.cachedAvatars[playerAvatarHash];

                            if (cachedAvatar != null)
                            {
                                if (avatar != null && avatar.eventsPlayer != null)
                                {
                                    avatar.Destroy();
                                    avatar = null;
                                }

                                avatar = AvatarManager.SpawnAvatar(cachedAvatar, avatarInput);
                                avatar.SetChildrenToLayer(10);

                                currentAvatarHash = playerAvatarHash;
                            }
                            else
                            {
                                unableToSpawnAvatar = true;
                                retryTimeCounter = Time.realtimeSinceStartup;
                            }
                        }
                        else
                        {
                            if (Config.Instance.DownloadAvatars)
                            {
                                if (!Config.Instance.DownloadNSFWAvatars && ModelSaberAPI.nsfwAvatars.Contains(playerAvatarHash))
                                {
                                    unableToSpawnAvatar = true;
                                    retryTimeCounter = Time.realtimeSinceStartup;
                                }
                                else
                                {
                                    ModelSaberAPI.avatarDownloaded -= AvatarDownloaded;
                                    ModelSaberAPI.avatarDownloaded += AvatarDownloaded;

                                    if (!ModelSaberAPI.queuedAvatars.Contains(playerAvatarHash))
                                    {
                                        SharedCoroutineStarter.instance.StartCoroutine(ModelSaberAPI.DownloadAvatarCoroutine(playerAvatarHash));

                                        if (avatar != null && avatar.eventsPlayer != null)
                                        {
                                            avatar.Destroy();
                                        }

                                        avatar = AvatarManager.SpawnAvatar(defaultAvatarInstance, avatarInput);
                                        avatar.SetChildrenToLayer(10);
                                    }
                                }
                            }
                        }
                    }
                }

                Vector3 offsetVector = new Vector3(offset, 0f, 0f);

                avatarInput.headPos = playerInfo.headPos + offsetVector;
                avatarInput.rightHandPos = playerInfo.rightHandPos + offsetVector;
                avatarInput.leftHandPos = playerInfo.leftHandPos + offsetVector;

                avatarInput.headRot = playerInfo.headRot;
                avatarInput.rightHandRot = playerInfo.rightHandRot;
                avatarInput.leftHandRot = playerInfo.leftHandRot;

                avatarInput.poseValid = true;

                avatarInput.fullBodyTracking = playerInfo.fullBodyTracking;
                if (playerInfo.fullBodyTracking)
                {
                    avatarInput.rightLegPos = playerInfo.rightLegPos + offsetVector;
                    avatarInput.leftLegPos = playerInfo.leftLegPos + offsetVector;
                    avatarInput.pelvisPos = playerInfo.pelvisPos + offsetVector;
                    avatarInput.rightLegRot = playerInfo.rightLegRot;
                    avatarInput.leftLegRot = playerInfo.leftLegRot;
                    avatarInput.pelvisRot = playerInfo.pelvisRot;

                }

                transform.position = avatarInput.headPos;

                playerNameText.text = playerName;

                if (playerInfo.playerFlags.rainbowName && !rainbowName)
                {
                    playerNameText.color = playerInfo.playerNameColor;
                    nameColor = HSBColor.FromColor(playerInfo.playerNameColor);
                }
                else if (!playerInfo.playerFlags.rainbowName && playerNameText.color != playerInfo.playerNameColor)
                {
                    playerNameText.color = playerInfo.playerNameColor;
                }

                rainbowName = playerInfo.playerFlags.rainbowName;
            }
            catch (Exception e)
            {
                Plugin.log.Critical(e);
            }

        }

        private void AvatarDownloaded(string hash)
        {
            ModelSaberAPI.avatarDownloaded -= AvatarDownloaded;

            if (this != null && playerAvatarHash == hash)
            {
                Plugin.log.Debug($"Avatar with hash \"{hash}\" loaded! (2)");

                if (avatar != null & avatar.eventsPlayer != null)
                {
                    avatar.Destroy();
                }

                avatar = AvatarManager.SpawnAvatar(ModelSaberAPI.cachedAvatars[hash], avatarInput);
                avatar.SetChildrenToLayer(10);

                currentAvatarHash = hash;
            }
        }


    }
}
