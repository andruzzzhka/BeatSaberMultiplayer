using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using CustomAvatar;
using SongCore.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer
{
    public class AvatarController : MonoBehaviour, IAvatarFullBodyInput
    {
        static CustomAvatar.CustomAvatar defaultAvatarInstance;

        static List<CustomAvatar.CustomAvatar> pendingAvatars = new List<CustomAvatar.CustomAvatar>();
        static event Action<string> AvatarLoaded;

        PlayerUpdate playerInfo;
        ulong playerId;
        string playerAvatarHash;
        string playerName;

        SpawnedAvatar avatar;
        AvatarScriptPack.FirstPersonExclusion exclusionScript;

        string currentAvatarHash;

        TextMeshPro playerNameText;
        Image playerSpeakerIcon;

        Vector3 HeadPos;
        Vector3 LeftHandPos;
        Vector3 RightHandPos;
        Vector3 LeftLegPos;
        Vector3 RightLegPos;
        Vector3 PelvisPos;

        Quaternion HeadRot;
        Quaternion LeftHandRot;
        Quaternion RightHandRot;
        Quaternion LeftLegRot;
        Quaternion RightLegRot;
        Quaternion PelvisRot;

        HSBColor nameColor;
        bool rainbowName;

        Camera camera;

        VRCenterAdjust centerAdjust;

        public PosRot HeadPosRot => new PosRot(HeadPos, HeadRot);

        public PosRot LeftPosRot => new PosRot(LeftHandPos, LeftHandRot);

        public PosRot RightPosRot => new PosRot(RightHandPos, RightHandRot);

        public PosRot LeftLegPosRot => new PosRot(LeftLegPos, LeftLegRot);

        public PosRot RightLegPosRot => new PosRot(RightLegPos, RightLegRot);

        public PosRot PelvisPosRot => new PosRot(PelvisPos, PelvisRot);

        public static void LoadAvatars()
        {
            if (defaultAvatarInstance == null)
            {
                if (Config.Instance.DownloadAvatars)
                {
                    defaultAvatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault(x => x.FullPath.ToLower().Contains("loading.avatar"));

                    if (defaultAvatarInstance == null)//fallback to multiplayer avatar
                    {
                        defaultAvatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault(x => x.FullPath.ToLower().Contains("multiplayer.avatar"));
                    }

                    if (defaultAvatarInstance == null)//fallback to default avatar
                    {
                        defaultAvatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault(x => x.FullPath.ToLower().Contains("templatefullbody.avatar"));
                    }

                    if (defaultAvatarInstance == null)//fallback to ANY avatar
                    {
                        defaultAvatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault();
                    }
                }
                else
                {
                    defaultAvatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault(x => x.FullPath.ToLower().Contains("multiplayer.avatar"));

                    if (defaultAvatarInstance == null)//fallback to default avatar
                    {
                        defaultAvatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault(x => x.FullPath.ToLower().Contains("templatefullbody.avatar"));
                    }

                    if (defaultAvatarInstance == null)//fallback to ANY avatar
                    {
                        defaultAvatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault();
                    }
                }

            }
#if DEBUG
            Plugin.log.Info($"Found avatar, isLoaded={defaultAvatarInstance.IsLoaded}");
#endif
            if (defaultAvatarInstance != null && !defaultAvatarInstance.IsLoaded)
            {
                defaultAvatarInstance.Load(null);
            }
        }

        public void Awake()
        {
            StartCoroutine(InitializeAvatarController());
        }

        IEnumerator InitializeAvatarController()
        {
            if (!defaultAvatarInstance.IsLoaded)
            {
#if DEBUG
                Plugin.log.Info("Waiting for avatar to load");
#endif
                yield return new WaitWhile(delegate () { return !defaultAvatarInstance.IsLoaded; });
            }
            else
            {
                yield return null;
            }

#if DEBUG
            Plugin.log.Info("Spawning avatar");
#endif
            centerAdjust = FindObjectOfType<VRCenterAdjust>();

            avatar = AvatarSpawner.SpawnAvatar(defaultAvatarInstance, this);
            exclusionScript = avatar.GameObject.GetComponentsInChildren<AvatarScriptPack.FirstPersonExclusion>().FirstOrDefault();
            if (exclusionScript != null)
                exclusionScript.SetVisible();
            
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
            
            avatar.GameObject.transform.SetParent(centerAdjust.transform, false);
            transform.SetParent(centerAdjust.transform, false);

            if (ModelSaberAPI.cachedAvatars.Any(x => x.Value == avatar.CustomAvatar))
            {
                currentAvatarHash = ModelSaberAPI.cachedAvatars.First(x => x.Value == avatar.CustomAvatar).Key;
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
                    if (Config.Instance.SpectatorMode)
                    {
                        if (IPA.Loader.PluginManager.AllPlugins.Select(x => x.Metadata.Name) //BSIPA Plugins
                            .Concat(IPA.Loader.PluginManager.Plugins.Select(x => x.Name))    //Old IPA Plugins 
                            .Any(x => x == "CameraPlus") && (camera == null || !camera.isActiveAndEnabled))
                        {
                            camera = FindObjectsOfType<Camera>().FirstOrDefault(x => (x.name.StartsWith("CamPlus_") || x.name.Contains("cameraplus")) && x.isActiveAndEnabled);
                        }

                        if (camera != null)
                        {
                            playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - camera.transform.position);
                            playerSpeakerIcon.rectTransform.rotation = Quaternion.LookRotation(playerSpeakerIcon.rectTransform.position - camera.transform.position);
                        }
                        else
                        {
                            playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - CustomAvatar.Plugin.Instance.PlayerAvatarManager._playerAvatarInput.HeadPosRot.Position);
                            playerSpeakerIcon.rectTransform.rotation = Quaternion.LookRotation(playerSpeakerIcon.rectTransform.position - CustomAvatar.Plugin.Instance.PlayerAvatarManager._playerAvatarInput.HeadPosRot.Position);
                        }
                    }
                    else
                    {
                        playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - CustomAvatar.Plugin.Instance.PlayerAvatarManager._playerAvatarInput.HeadPosRot.Position);
                        playerSpeakerIcon.rectTransform.rotation = Quaternion.LookRotation(playerSpeakerIcon.rectTransform.position - CustomAvatar.Plugin.Instance.PlayerAvatarManager._playerAvatarInput.HeadPosRot.Position);
                    }

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
            catch(Exception e)
            {
                Plugin.log.Warn($"Unable to rotate text to the camera! Exception: {e}");
            }

        }

        void OnDestroy()
        {
            Plugin.log.Debug("Destroying avatar");
            if(avatar != null && avatar.GameObject != null)
                Destroy(avatar.GameObject);
        }

        public void SetPlayerInfo(PlayerInfo _playerInfo, float offset, bool isLocal)
        {
            if (_playerInfo == default)
            {
                if (playerNameText != null) playerNameText.gameObject.SetActive(false);
                if (playerSpeakerIcon != null) playerSpeakerIcon.gameObject.SetActive(false);
                if (avatar != null && avatar.GameObject != null) Destroy(avatar.GameObject);
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
                        if (avatar != null)
                        {
                            Destroy(avatar.GameObject);
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
                    if (ModelSaberAPI.cachedAvatars.ContainsKey(playerAvatarHash))
                    {
                        CustomAvatar.CustomAvatar cachedAvatar = ModelSaberAPI.cachedAvatars[playerAvatarHash];
                        
                        if (cachedAvatar != null)
                        {
                            if (pendingAvatars.Contains(cachedAvatar))
                            {
                                AvatarLoaded -= AvatarController_AvatarLoaded;
                                AvatarLoaded += AvatarController_AvatarLoaded;
                            }
                            else if (!pendingAvatars.Contains(cachedAvatar) && !cachedAvatar.IsLoaded)
                            {
                                if (avatar != null)
                                {
                                    Destroy(avatar.GameObject);
                                }

                                avatar = AvatarSpawner.SpawnAvatar(defaultAvatarInstance, this);
                                exclusionScript = avatar.GameObject.GetComponentsInChildren<AvatarScriptPack.FirstPersonExclusion>().FirstOrDefault();
                                if (exclusionScript != null)
                                    exclusionScript.SetVisible();
                                
                                pendingAvatars.Add(cachedAvatar);
                                AvatarLoaded -= AvatarController_AvatarLoaded;
                                AvatarLoaded += AvatarController_AvatarLoaded;
                                cachedAvatar.Load((CustomAvatar.CustomAvatar loadedAvatar, AvatarLoadResult result) =>
                               {
                                   if (result == AvatarLoadResult.Completed)
                                   {
                                       pendingAvatars.Remove(ModelSaberAPI.cachedAvatars[playerAvatarHash]);
                                       AvatarLoaded?.Invoke(ModelSaberAPI.cachedAvatars.First(x => x.Value == loadedAvatar).Key);
                                   }
                               });
                            }
                            else
                            {
                                if (avatar != null)
                                {
                                    Destroy(avatar.GameObject);
                                }
                                
                                avatar = AvatarSpawner.SpawnAvatar(cachedAvatar, this);
                                exclusionScript = avatar.GameObject.GetComponentsInChildren<AvatarScriptPack.FirstPersonExclusion>().FirstOrDefault();
                                if (exclusionScript != null)
                                    exclusionScript.SetVisible();
                                
                                currentAvatarHash = playerAvatarHash;
                            }
                        }
                    }
                    else
                    {
                        if (Config.Instance.DownloadAvatars)
                        {
                            if (ModelSaberAPI.queuedAvatars.Contains(playerAvatarHash))
                            {
                                ModelSaberAPI.avatarDownloaded -= AvatarDownloaded;
                                ModelSaberAPI.avatarDownloaded += AvatarDownloaded;
                            }
                            else
                            {
                                ModelSaberAPI.avatarDownloaded -= AvatarDownloaded;
                                ModelSaberAPI.avatarDownloaded += AvatarDownloaded;
                                SharedCoroutineStarter.instance.StartCoroutine(ModelSaberAPI.DownloadAvatarCoroutine(playerAvatarHash));

                                if (avatar != null)
                                {
                                    Destroy(avatar.GameObject);
                                }

                                avatar = AvatarSpawner.SpawnAvatar(defaultAvatarInstance, this);
                                exclusionScript = avatar.GameObject.GetComponentsInChildren<AvatarScriptPack.FirstPersonExclusion>().FirstOrDefault();
                                if (exclusionScript != null)
                                    exclusionScript.SetVisible();
                            }
                        }
                    }
                }

                Vector3 offsetVector = new Vector3(offset, 0f, 0f);

                HeadPos = playerInfo.headPos + offsetVector;
                RightHandPos = playerInfo.rightHandPos + offsetVector;
                LeftHandPos = playerInfo.leftHandPos + offsetVector;
                
                HeadRot = playerInfo.headRot;
                RightHandRot = playerInfo.rightHandRot;
                LeftHandRot = playerInfo.leftHandRot;

                if (playerInfo.fullBodyTracking)
                {
                    RightLegPos = playerInfo.rightLegPos + offsetVector;
                    LeftLegPos = playerInfo.leftLegPos + offsetVector;
                    PelvisPos = playerInfo.pelvisPos + offsetVector;
                    RightLegRot = playerInfo.rightLegRot;
                    LeftLegRot = playerInfo.leftLegRot;
                    PelvisRot = playerInfo.pelvisRot;
                }
                else
                {
                    RightLegPos = new Vector3();
                    LeftLegPos = new Vector3();
                    PelvisPos = new Vector3();
                    RightLegRot = new Quaternion();
                    LeftLegRot = new Quaternion();
                    PelvisRot = new Quaternion();
                }

                transform.position = HeadPos;

                playerNameText.text = playerName;

                if (playerInfo.playerFlags.rainbowName && !rainbowName)
                {
                    playerNameText.color = playerInfo.playerNameColor;
                    nameColor = HSBColor.FromColor(playerInfo.playerNameColor);
                }
                else if(!playerInfo.playerFlags.rainbowName && playerNameText.color != playerInfo.playerNameColor)
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

        private void AvatarController_AvatarLoaded(string hash)
        {
            if (this != null && (!ModelSaberAPI.cachedAvatars.ContainsValue(avatar.CustomAvatar) || ModelSaberAPI.cachedAvatars.First(x => x.Value == avatar.CustomAvatar).Key != playerAvatarHash) && playerAvatarHash == hash)
            {
                Plugin.log.Debug($"Avatar with hash \"{hash}\" loaded! (1)");
                AvatarLoaded -= AvatarController_AvatarLoaded;

                if (avatar != null)
                {
                    Destroy(avatar.GameObject);
                }

                avatar = AvatarSpawner.SpawnAvatar(ModelSaberAPI.cachedAvatars[hash], this);
                exclusionScript = avatar.GameObject.GetComponentsInChildren<AvatarScriptPack.FirstPersonExclusion>().FirstOrDefault();
                if (exclusionScript != null)
                    exclusionScript.SetVisible();
                currentAvatarHash = hash;
            }
        }

        private void AvatarDownloaded(string hash)
        {
            if (this != null && (!ModelSaberAPI.cachedAvatars.ContainsValue(avatar.CustomAvatar) || ModelSaberAPI.cachedAvatars.First(x => x.Value == avatar.CustomAvatar).Key != playerAvatarHash) && playerAvatarHash == hash)
            {
                Plugin.log.Debug($"Avatar with hash \"{hash}\" loaded! (2)");
                ModelSaberAPI.avatarDownloaded -= AvatarDownloaded;

                if (avatar != null)
                {
                    Destroy(avatar.GameObject);
                }
                
                avatar = AvatarSpawner.SpawnAvatar(ModelSaberAPI.cachedAvatars[hash], this);
                exclusionScript = avatar.GameObject.GetComponentsInChildren<AvatarScriptPack.FirstPersonExclusion>().FirstOrDefault();
                if (exclusionScript != null)
                    exclusionScript.SetVisible();
                currentAvatarHash = hash;
            }
        }

        private void SetRendererInChilds(Transform origin, bool enabled)
        {
            Renderer[] rends = origin.gameObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in rends)
            {
                rend.enabled = enabled;
            }
        }


    }
}
