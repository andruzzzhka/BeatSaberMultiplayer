using System.Reflection;
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
            if (Client.Instance == null || !Client.Instance.connected)
            {
                DefaultUpdate();
            }
            else
            {
                if (owner != null && owner.playerInfo != null)
                {
                    targetPos = (_node == XRNode.LeftHand ? owner.playerInfo.updateInfo.leftHandPos : owner.playerInfo.updateInfo.rightHandPos);
                    targetRot = (_node == XRNode.LeftHand ? owner.playerInfo.updateInfo.leftHandRot : owner.playerInfo.updateInfo.rightHandRot);

                    transform.position = targetPos + Vector3.right * owner.avatarOffset;
                    transform.rotation = targetRot;
                }
                else
                {
                    if(Time.frameCount % 90 == 0)
                    {
                        Plugin.log.Warn(owner == null ? "Controller owner is null!" : "Controller owner's player info is null!");
                    }
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
