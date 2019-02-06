using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.Misc
{
    static class ControllersHelper
    {

        public static bool GetRightGrip()
        {
            if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
            {
                return OpenVRRightGrip();
            }
            else if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                return OculusRightGrip();
            }
            else if (Environment.CommandLine.Contains("fpfc") && VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Unknown)
            {
                return Input.GetKey(KeyCode.G);
            }
            else
            {
                return false;
            }
        }

        public static bool GetLeftGrip()
        {
            if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
            {
                return OpenVRLeftGrip();
            }
            else if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                return OculusLeftGrip();
            }
            else if (Environment.CommandLine.Contains("fpfc") && VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Unknown)
            {
                return Input.GetKey(KeyCode.G);
            }
            else
            {
                return false;
            }
        }

        private static bool OculusRightGrip()
        {
            return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > 0.85f;
        }

        private static bool OculusLeftGrip()
        {
            return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > 0.85f;
        }

        private static bool OpenVRRightGrip()
        {
            return SteamVR_Controller.Input(SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost)).GetPress(Valve.VR.EVRButtonId.k_EButton_Grip);
        }

        private static bool OpenVRLeftGrip()
        {
            return SteamVR_Controller.Input(SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost)).GetPress(Valve.VR.EVRButtonId.k_EButton_Grip);
        }




    }





}
