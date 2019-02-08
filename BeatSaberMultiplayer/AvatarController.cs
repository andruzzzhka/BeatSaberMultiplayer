using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using CustomAvatar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer
{
    public class AvatarController : MonoBehaviour, IAvatarInput
    {
        static CustomAvatar.CustomAvatar defaultAvatarInstance;

        static List<CustomAvatar.CustomAvatar> pendingAvatars = new List<CustomAvatar.CustomAvatar>();
        static event Action<string> AvatarLoaded;

        PlayerInfo playerInfo;

        SpawnedAvatar avatar;
        AvatarScriptPack.FirstPersonExclusion exclusionScript;

        string currentAvatarHash;

        TextMeshPro playerNameText;
        Image playerSpeakerIcon;

        Vector3 HeadPos;
        Vector3 LeftHandPos;
        Vector3 RightHandPos;
        Quaternion HeadRot;
        Quaternion LeftHandRot;
        Quaternion RightHandRot;
        
        Camera _camera;

        VRCenterAdjust _centerAdjust;

        public PosRot HeadPosRot => new PosRot(HeadPos, HeadRot);

        public PosRot LeftPosRot => new PosRot(LeftHandPos, LeftHandRot);

        public PosRot RightPosRot => new PosRot(RightHandPos, RightHandRot);

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
            Misc.Logger.Info($"Found avatar, isLoaded={defaultAvatarInstance.IsLoaded}");
#endif
            if (!defaultAvatarInstance.IsLoaded)
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
                Misc.Logger.Info("Waiting for avatar to load");
#endif
                yield return new WaitWhile(delegate () { return !defaultAvatarInstance.IsLoaded; });
            }
            else
            {
                yield return null;
            }

#if DEBUG
            Misc.Logger.Info("Spawning avatar");
#endif
            _centerAdjust = FindObjectOfType<VRCenterAdjust>();

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
            
            avatar.GameObject.transform.SetParent(_centerAdjust.transform, false);
            transform.SetParent(_centerAdjust.transform, false);

            currentAvatarHash = ModelSaberAPI.cachedAvatars.First(x => x.Value == avatar.CustomAvatar).Key;
        }

        void Update()
        {
            try
            {
                if (playerNameText != null)
                {
                    if (IllusionInjector.PluginManager.Plugins.Any(x => x.Name == "CameraPlus") && _camera == null)
                    {
                        _camera = FindObjectsOfType<Camera>().FirstOrDefault(x => x.name.StartsWith("CamPlus_"));
                    }

                    if (_camera != null)
                    {
                        if (Config.Instance.SpectatorMode)
                        {
                            playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - _camera.transform.position);
                            playerSpeakerIcon.rectTransform.rotation = Quaternion.LookRotation(playerSpeakerIcon.rectTransform.position - _camera.transform.position);
                        }
                    }
                    else
                    {
                        playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - InGameOnlineController.GetXRNodeWorldPosRot(XRNode.Head).Position);
                        playerSpeakerIcon.rectTransform.rotation = Quaternion.LookRotation(playerSpeakerIcon.rectTransform.position - InGameOnlineController.GetXRNodeWorldPosRot(XRNode.Head).Position);
                    }
                }
            }
            catch(Exception e)
            {
                Misc.Logger.Warning($"Unable to rotate text to the camera! Exception: {e}");
            }

        }

        void OnDestroy()
        {
#if DEBUG
            Misc.Logger.Info("Destroying avatar");
#endif
            Destroy(avatar.GameObject);
        }

        public void SetPlayerInfo(PlayerInfo _playerInfo, float offset, bool isLocal)
        {
            if (_playerInfo == null)
            {
                playerNameText.gameObject.SetActive(false);
                playerSpeakerIcon.gameObject.SetActive(false);
                if (avatar != null)
                {
                    Destroy(avatar.GameObject);
                }
                return;
            }

            try
            {

                playerInfo = _playerInfo;

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
                        playerSpeakerIcon.gameObject.SetActive(true);
                    }
                }
                
                if (playerNameText == null || playerSpeakerIcon == null)
                {
                    return;
                }
                
                if((avatar == null || currentAvatarHash != playerInfo.avatarHash) && !isLocal)
                {
                    if (ModelSaberAPI.cachedAvatars.ContainsKey(playerInfo.avatarHash))
                    {
                        CustomAvatar.CustomAvatar cachedAvatar = ModelSaberAPI.cachedAvatars[playerInfo.avatarHash];
                        
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
                                cachedAvatar.Load((CustomAvatar.CustomAvatar loadedAvatar, AvatarLoadResult result) =>
                               {
                                   if (result == AvatarLoadResult.Completed)
                                   {
                                       pendingAvatars.Remove(ModelSaberAPI.cachedAvatars[playerInfo.avatarHash]);
                                       AvatarLoaded?.Invoke(ModelSaberAPI.cachedAvatars.First(x => x.Value == loadedAvatar).Key);
                                   }
                               });
                                AvatarLoaded += AvatarController_AvatarLoaded;
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
                                
                                currentAvatarHash = playerInfo.avatarHash;
                            }
                        }
                    }
                    else
                    {
                        if (Config.Instance.DownloadAvatars)
                        {
                            if (ModelSaberAPI.queuedAvatars.Contains(playerInfo.avatarHash))
                            {
                                ModelSaberAPI.avatarDownloaded += AvatarDownloaded;
                            }
                            else
                            {
                                SharedCoroutineStarter.instance.StartCoroutine(ModelSaberAPI.DownloadAvatarCoroutine(playerInfo.avatarHash, (string hash) => { AvatarDownloaded(hash); }));

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
                
                transform.position = HeadPos;

                playerNameText.text = playerInfo.playerName;
                playerNameText.color = playerInfo.playerNameColor;

                playerSpeakerIcon.gameObject.SetActive(InGameOnlineController.Instance.VoiceChatIsTalking(playerInfo.playerId));
            }
            catch (Exception e)
            {
                Misc.Logger.Exception($"Avatar controller exception: {playerInfo.playerName}: {e}");
            }

        }

        private void AvatarController_AvatarLoaded(string hash)
        {
            if (ModelSaberAPI.cachedAvatars.First(x => x.Value == avatar.CustomAvatar).Key != playerInfo.avatarHash && playerInfo.avatarHash == hash)
            {
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
            if (ModelSaberAPI.cachedAvatars.First(x => x.Value == avatar.CustomAvatar).Key != playerInfo.avatarHash && playerInfo.avatarHash == hash)
            {
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
