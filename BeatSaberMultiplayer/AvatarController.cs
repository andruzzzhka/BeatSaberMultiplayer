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
    class AvatarController : MonoBehaviour
    {
        PlayerInfo playerInfo;

        AvatarScript avatar;
        AvatarBodyManager bodyManager;

        TextMeshPro playerNameText;

        Vector3 targetHeadPos;
        Vector3 lastHeadPos;

        Vector3 targetLeftHandPos;
        Vector3 lastLeftHandPos;

        Vector3 targetRightHandPos;
        Vector3 lastRightHandPos;

        Quaternion targetHeadRot;
        Quaternion lastHeadRot;

        Quaternion targetLeftHandRot;
        Quaternion lastLeftHandRot;

        Quaternion targetRightHandRot;
        Quaternion lastRightHandRot;

        float interpolationProgress = 0f;

        bool rendererEnabled = true;

        public AvatarController()
        {
            CreateGameObjects();
        }

        void CreateGameObjects()
        {

            if (avatar == null)
            {
                Console.WriteLine("Spawning avatar");
                avatar = AvatarScript.SpawnAvatar(".\\CustomAvatars\\TemplateFullBody.avatar", true);
                
                StartCoroutine(WaitForAvatar());
            }            
        }

        private IEnumerator WaitForAvatar()
        {
            yield return new WaitUntil(delegate () { return avatar.getInstance() != null; });

            AvatarLoaded();
        }

        private void AvatarLoaded()
        {
            if (bodyManager == null)
            {
                bodyManager = avatar.GetBodyManager();
            }

            if (ReflectionUtil.GetPrivateField<GameObject>(avatar, "fpsAvatarInstance") != null)
            {
                Destroy(ReflectionUtil.GetPrivateField<GameObject>(avatar, "fpsAvatarInstance"));
                Console.WriteLine("Destroyed fps avatar instance");
            }

            if (playerNameText == null)
            {
                playerNameText = BSMultiplayerUI._instance.CreateWorldText(bodyManager.GetHeadTransform(), "INVALID");
                playerNameText.rectTransform.anchoredPosition3D = new Vector3(0f, 0.25f, 0f);
                playerNameText.alignment = TextAlignmentOptions.Center;
                playerNameText.fontSize = 2.5f;
            }

            Console.WriteLine("Avatar loaded");
        }

        void Update()
        {
            if (playerNameText != null)
            {
                interpolationProgress += Time.deltaTime * 20;
                if (interpolationProgress > 1f)
                {
                    interpolationProgress = 1f;
                }

                bodyManager.SetHeadPosRot(Vector3.Lerp(lastHeadPos, targetHeadPos, interpolationProgress), Quaternion.Lerp(lastHeadRot, targetHeadRot, interpolationProgress));
                bodyManager.SetLeftHandPosRot(Vector3.Lerp(lastLeftHandPos, targetLeftHandPos, interpolationProgress), Quaternion.Lerp(lastLeftHandRot, targetLeftHandRot, interpolationProgress));
                bodyManager.SetRightHandPosRot(Vector3.Lerp(lastRightHandPos, targetRightHandPos, interpolationProgress), Quaternion.Lerp(lastRightHandRot, targetRightHandRot, interpolationProgress));
                
                playerNameText.rectTransform.rotation = Quaternion.LookRotation(playerNameText.rectTransform.position - InputTracking.GetLocalPosition(XRNode.Head));
            }
        }

        void OnDestroy()
        {
            Destroy(avatar.getInstance());
            Destroy(avatar.gameObject);
        }

        public void SetPlayerInfo(PlayerInfo _playerInfo, float offset, bool isLocal)
        {
            

            if (_playerInfo == null)
            {
                Destroy(gameObject);
                return;
            }
            try
            {

                playerInfo = _playerInfo;

                if(playerNameText == null || avatar == null || bodyManager == null)
                {
                    return;
                }

                if (isLocal)
                {
                    playerNameText.gameObject.SetActive(false);
                    if (rendererEnabled)
                    {
                        SetRendererInChilds(avatar.getInstance().transform, false);
                        rendererEnabled = false;
                    }
                }
                else
                {
                    playerNameText.gameObject.SetActive(true);
                    if (!rendererEnabled)
                    {
                        SetRendererInChilds(avatar.getInstance().transform, true);
                        rendererEnabled = true;
                    }
                }

                interpolationProgress = 0f;

                lastHeadPos = targetHeadPos;
                targetHeadPos = _playerInfo.headPos + (new Vector3(offset, 0f, 0f) + BSMultiplayerClient._instance.roomPositionOffset);

                lastRightHandPos = targetRightHandPos;
                targetRightHandPos = _playerInfo.rightHandPos + (new Vector3(offset, 0f, 0f) + BSMultiplayerClient._instance.roomPositionOffset);

                lastLeftHandPos = targetLeftHandPos;
                targetLeftHandPos = _playerInfo.leftHandPos + (new Vector3(offset, 0f, 0f) + BSMultiplayerClient._instance.roomPositionOffset);

                lastHeadRot = targetHeadRot;
                targetHeadRot = _playerInfo.headRot;

                lastRightHandRot = targetRightHandRot;
                targetRightHandRot = _playerInfo.rightHandRot;

                lastLeftHandRot = targetLeftHandRot;
                targetLeftHandRot = _playerInfo.leftHandRot;
                
                playerNameText.text = playerInfo.playerName;
                
            }
            catch(Exception e)
            {
                Console.WriteLine($"AVATAR EXCEPTION: {_playerInfo.playerName}: {e}");
            }
        }

        private void SetRendererInChilds(Transform origin, bool enabled)
        {
            Renderer[] rends = origin.gameObject.GetComponentsInChildren<Renderer>();
            foreach(Renderer rend in rends)
            {
                rend.enabled = enabled;
            }
        }


    }
}
