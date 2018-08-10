using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using CustomAvatar;
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
    class AvatarController : MonoBehaviour, IAvatarInput
    {
        PlayerInfo playerInfo;

        SpawnedAvatar avatar;
        CustomAvatar.CustomAvatar avatarInstance;

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

        public PosRot HeadPosRot => new PosRot(interpHeadPos, interpHeadRot);

        public PosRot LeftPosRot => new PosRot(interpLeftHandPos, interpLeftHandRot);

        public PosRot RightPosRot => new PosRot(interpRightHandPos, interpRightHandRot);

        public AvatarController()
        {
            CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.ToList().ForEach(x => Log.Info(x.FullPath));
            avatarInstance = CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.First(x => x.FullPath.Contains("TemplateFullBody"));
            if (!avatarInstance.IsLoaded)
            {
                avatarInstance.Load(AvatarLoaded);
            }
            else
            {
                InitializeAvatarController();
            }
        }

        private void AvatarLoaded(CustomAvatar.CustomAvatar avatar, AvatarLoadResult result)
        {
            if (result == AvatarLoadResult.Completed)
            {
#if DEBUG
                Log.Info("Loaded avatar");
#endif
                InitializeAvatarController();
            }
        }

        void InitializeAvatarController()
        {
#if DEBUG
            Log.Info("Spawning avatar");
#endif
            avatar = AvatarSpawner.SpawnAvatar(avatarInstance, this);

            playerNameText = BeatSaberUI.CreateWorldText(avatar.CustomAvatar.ViewPoint, "INVALID");
            playerNameText.rectTransform.anchoredPosition3D = new Vector3(0f, 0.25f, 0f);
            playerNameText.alignment = TextAlignmentOptions.Center;
            playerNameText.fontSize = 2.5f;

        }

        void Update()
        {

            if (avatar != null)
            {
                interpolationProgress += Time.deltaTime * 30;
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

                playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - InputTracking.GetLocalPosition(XRNode.Head));
            }

        }

        void OnDestroy()
        {
            Log.Info("Destroying avatar");
            Destroy(avatar.GameObject);
        }

        public void SetPlayerInfo(PlayerInfo _playerInfo, float offset, bool isLocal)
        {
            if (_playerInfo == null)
                return;

            try
            {

                playerInfo = _playerInfo;

                if (playerNameText == null || avatar == null)
                {
                    return;
                }

#if DEBUG
                if (false)
#else
                if (isLocal)
#endif
                {
                    playerNameText.gameObject.SetActive(false);
                    if (rendererEnabled)
                    {
                        SetRendererInChilds(avatar.GameObject.transform, false);
                        rendererEnabled = false;
                    }
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

            }
            catch (Exception e)
            {
                Log.Exception($"AVATAR EXCEPTION: {_playerInfo.playerName}: {e}");
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
