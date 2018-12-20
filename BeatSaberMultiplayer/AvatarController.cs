using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using CustomAvatar;
using CustomUI.BeatSaber;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

namespace BeatSaberMultiplayer
{
    public class AvatarController : MonoBehaviour, IAvatarInput
    {
        static CustomAvatar.CustomAvatar avatarInstance;

        PlayerInfo playerInfo;

        SpawnedAvatar avatar;

        TextMeshPro playerNameText;

        Vector3 targetHeadPos;
        Vector3 interpHeadPos;
        Vector3 lastHeadPos;

        Vector3 targetLeftHandPos;
        Vector3 interpLeftHandPos;
        Vector3 lastLeftHandPos;

        Vector3 targetRightHandPos;
        Vector3 interpRightHandPos;
        Vector3 lastRightHandPos;

        Quaternion targetHeadRot;
        Quaternion interpHeadRot;
        Quaternion lastHeadRot;

        Quaternion targetLeftHandRot;
        Quaternion interpLeftHandRot;
        Quaternion lastLeftHandRot;

        Quaternion targetRightHandRot;
        Quaternion interpRightHandRot;
        Quaternion lastRightHandRot;

        float interpolationProgress = 0f;

        bool rendererEnabled = true;
        Camera _camera;
        public bool forcePlayerInfo = false;

        public PosRot HeadPosRot => new PosRot(interpHeadPos, interpHeadRot);

        public PosRot LeftPosRot => new PosRot(interpLeftHandPos, interpLeftHandRot);

        public PosRot RightPosRot => new PosRot(interpRightHandPos, interpRightHandRot);

        public static void LoadAvatar()
        {
            if (avatarInstance == null)
            {
#if DEBUG
                CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.ToList().ForEach(x => Misc.Logger.Info(x.FullPath));
#endif
                avatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault(x => x.FullPath.ToLower().Contains("multiplayer.avatar"));

                if(avatarInstance == null)//fallback to default avatar
                {
                    avatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault(x => x.FullPath.ToLower().Contains("templatefullbody.avatar"));
                }

                if (avatarInstance == null)//fallback to ANY avatar
                {
                    avatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.FirstOrDefault();
                }

            }
#if DEBUG
            Misc.Logger.Info($"Found avatar, isLoaded={avatarInstance.IsLoaded}");
#endif
            if (!avatarInstance.IsLoaded)
            {
                avatarInstance.Load(AvatarLoaded);
            }
        }

        public AvatarController()
        {
            StartCoroutine(InitializeAvatarController());
        }

        private static void AvatarLoaded(CustomAvatar.CustomAvatar avatar, AvatarLoadResult result)
        {
            if (result == AvatarLoadResult.Completed)
            {
#if DEBUG
                Misc.Logger.Info("Loaded avatar");
#endif
            }
            else
            {
                Misc.Logger.Error($"Unable to load avatar! Error: {result}");
            }
        }

        IEnumerator InitializeAvatarController()
        {
            if (!avatarInstance.IsLoaded)
            {
#if DEBUG
                Misc.Logger.Info("Waiting for avatar to load");
#endif
                yield return new WaitWhile(delegate () { return !avatarInstance.IsLoaded; });
            }
            else
            {
                yield return null;
            }

#if DEBUG
            Misc.Logger.Info("Spawning avatar");
#endif
            avatar = AvatarSpawner.SpawnAvatar(avatarInstance, this);
            
            playerNameText = CustomExtensions.CreateWorldText(transform, "INVALID");
            playerNameText.rectTransform.anchoredPosition3D = new Vector3(0f, 0.25f, 0f);
            playerNameText.alignment = TextAlignmentOptions.Center;
            playerNameText.fontSize = 2.5f;
        }

        void Update()
        {
            try
            {
                if (avatar != null && !forcePlayerInfo)
                {
                    if (Client.instance.Tickrate < (1f/Time.smoothDeltaTime))
                    {
                        interpolationProgress += Time.deltaTime * Client.instance.Tickrate;
                    }
                    else
                    {
                        interpolationProgress = 1f;
                    }
                    if (interpolationProgress > 1f)
                    {
                        interpolationProgress = 1f;
                    }

                    interpHeadPos = Vector3.Lerp(lastHeadPos, targetHeadPos, interpolationProgress);
                    interpLeftHandPos = Vector3.Lerp(lastLeftHandPos, targetLeftHandPos, interpolationProgress);
                    interpRightHandPos = Vector3.Lerp(lastRightHandPos, targetRightHandPos, interpolationProgress);

                    interpHeadRot = Quaternion.Lerp(lastHeadRot, targetHeadRot, interpolationProgress);
                    interpLeftHandRot = Quaternion.Lerp(lastLeftHandRot, targetLeftHandRot, interpolationProgress);
                    interpRightHandRot = Quaternion.Lerp(lastRightHandRot, targetRightHandRot, interpolationProgress);

                    transform.position = interpHeadPos;
                }
            }catch(Exception e)
            {
                Misc.Logger.Error("Unable to lerp avatar position! Exception: "+e);
            }

            try
            {
                if (IllusionInjector.PluginManager.Plugins.Any(x => x.Name == "CameraPlus") && _camera == null)
                {
                    _camera = FindObjectsOfType<Camera>().FirstOrDefault(x => x.name == "Camera Plus");
                }

                if (_camera != null)
                {
                    if (Config.Instance.SpectatorMode)
                    {
                        playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - _camera.transform.position);
                    }
                }
                else
                {
                    playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - InGameOnlineController.GetXRNodeWorldPosRot(XRNode.Head).Position);
                }
            }
            catch(Exception e)
            {
                Misc.Logger.Warning("Unable to rotate text to the camera! Exception: "+e);
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
                if (rendererEnabled)
                {
                    SetRendererInChilds(avatar.GameObject.transform, false);
                    rendererEnabled = false;
                }
                return;
            }

            try
            {

                playerInfo = _playerInfo;

                if (playerNameText == null || avatar == null)
                {
                    return;
                }

                if (isLocal)
                {
                    playerNameText.gameObject.SetActive(false);
#if !DEBUG
                    if (rendererEnabled)
                    {
                        SetRendererInChilds(avatar.GameObject.transform, false);
                        rendererEnabled = false;
                    }
#endif
                }
                else
                {
                    playerNameText.gameObject.SetActive(true);
                    if (!rendererEnabled)
                    {
                        SetRendererInChilds(avatar.GameObject.transform, true);
                        rendererEnabled = true;
                    }
                }

                interpolationProgress = 0f;

                Vector3 offsetVector = new Vector3(offset, 0f, 0f);

                lastHeadPos = targetHeadPos;
                targetHeadPos = _playerInfo.headPos + offsetVector;

                lastRightHandPos = targetRightHandPos;
                targetRightHandPos = _playerInfo.rightHandPos + offsetVector;

                lastLeftHandPos = targetLeftHandPos;
                targetLeftHandPos = _playerInfo.leftHandPos + offsetVector;

                lastHeadRot = targetHeadRot;
                targetHeadRot = _playerInfo.headRot;

                lastRightHandRot = targetRightHandRot;
                targetRightHandRot = _playerInfo.rightHandRot;

                lastLeftHandRot = targetLeftHandRot;
                targetLeftHandRot = _playerInfo.leftHandRot;

                playerNameText.text = playerInfo.playerName;

                if (forcePlayerInfo)
                {
                    interpHeadPos = targetHeadPos;
                    interpLeftHandPos = targetLeftHandPos;
                    interpRightHandPos = targetRightHandPos;

                    interpHeadRot = targetHeadRot;
                    interpLeftHandRot = targetLeftHandRot;
                    interpRightHandRot = targetRightHandRot;

                    transform.position = interpHeadPos;
                }

            }
            catch (Exception e)
            {
                Misc.Logger.Exception($"Avatar controller exception: {_playerInfo.playerName}: {e}");
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
