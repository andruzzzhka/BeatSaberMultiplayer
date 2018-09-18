using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;

namespace BeatSaberMultiplayer
{
    class OnlineVRController : VRController
    {
        Vector3 targetPos;
        Vector3 interpPos;
        Vector3 lastPos;

        Quaternion targetRot;
        Quaternion interpRot;
        Quaternion lastRot;

        float interpolationProgress;

        public bool forcePlayerInfo;

        public OnlineVRController()
        {
            VRController original = GetComponent<VRController>();

            foreach (FieldInfo info in original.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                info.SetValue(this, info.GetValue(original));
            }

            Destroy(original);
        }

        public override void Update()
        {
            if (!Config.Instance.SpectatorMode)
            {
                DefaultUpdate();
            }
            else
            {
                if(Client.instance != null && Client.instance.Connected)
                {
                    if (!forcePlayerInfo)
                    {
                        interpolationProgress += Time.deltaTime * Client.instance.Tickrate;


                        if (interpolationProgress > 1f)
                        {
                            interpolationProgress = 1f;
                        }

                        interpPos = Vector3.Lerp(lastPos, targetPos, interpolationProgress);
                        interpRot = Quaternion.Lerp(lastRot, targetRot, interpolationProgress);

                        transform.position = interpPos;
                        transform.rotation = interpRot;

                        PersistentSingleton<VRPlatformHelper>.instance.AdjustPlatformSpecificControllerTransform(transform);
                    }
                }
                else
                {
                    DefaultUpdate();
                }
            }
        }

        private void DefaultUpdate()
        {
            transform.localPosition = InputTracking.GetLocalPosition(_node);
            transform.localRotation = InputTracking.GetLocalRotation(_node);
            PersistentSingleton<VRPlatformHelper>.instance.AdjustPlatformSpecificControllerTransform(transform);
        }

        public void SetPlayerInfo(PlayerInfo _playerInfo)
        {
            if (_playerInfo == null)
                return;

            interpolationProgress = 0f;

            lastPos = targetPos;
            targetPos = (_node == XRNode.LeftHand ? _playerInfo.leftHandPos : _playerInfo.rightHandPos);

            lastRot = targetRot;
            targetRot = (_node == XRNode.LeftHand ? _playerInfo.leftHandRot : _playerInfo.rightHandRot);

            if (forcePlayerInfo)
            {
                transform.position = targetPos;
                transform.rotation = targetRot;
            }
        }
    }
}
