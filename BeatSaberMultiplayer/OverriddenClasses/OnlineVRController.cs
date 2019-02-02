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
    public class OnlineVRController : VRController
    {
        public OnlinePlayerController owner;

        Vector3 targetPos;
        Quaternion targetRot;

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
            if (Client.Instance == null || !Client.Instance.Connected)
            {
                DefaultUpdate();
            }
            else
            {
                if (owner != null)
                {
                    targetPos = (_node == XRNode.LeftHand ? owner.PlayerInfo.leftHandPos : owner.PlayerInfo.rightHandPos);
                    targetRot = (_node == XRNode.LeftHand ? owner.PlayerInfo.leftHandRot : owner.PlayerInfo.rightHandRot);

                    transform.position = targetPos + Vector3.right * owner.avatarOffset;
                    transform.rotation = targetRot;
                }
            }
        }

        private void DefaultUpdate()
        {
            transform.localPosition = InputTracking.GetLocalPosition(_node);
            transform.localRotation = InputTracking.GetLocalRotation(_node);
            PersistentSingleton<VRPlatformHelper>.instance.AdjustPlatformSpecificControllerTransform(transform);
        }
    }
}
