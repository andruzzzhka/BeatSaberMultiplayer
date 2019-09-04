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
        private static bool initialized = false;
        private static bool rumbleEnahncerInstalled = false;
        private static int platform = -1;
        private static VRPlatformHelper platformHelper;

        public static void Init()
        {
            rumbleEnahncerInstalled = IPA.Loader.PluginManager.AllPlugins.Any(x => x.Metadata.Id == "rumbleenhancer");
            Plugin.log.Info("Rumble Enahncer installed: "+ rumbleEnahncerInstalled);

            if(platformHelper == null)
            {
                platformHelper = Resources.FindObjectsOfTypeAll<VRPlatformHelper>().FirstOrDefault();
            }

            if (platformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
                platform = 0;
            else if (platformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
                platform = 1;
            else if (Environment.CommandLine.Contains("fpfc") && platformHelper.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Unknown)
                platform = 2;
            else
                platform = -1;

            initialized = true;
        }

        public static bool GetRightGrip()
        {
            if (!initialized)
            {
                Init();
            }

            switch (platform)
            {
                case 0:
                    return rumbleEnahncerInstalled ? OpenVRRightGripWithLock() : OpenVRRightGrip();
                case 1:
                    return OculusRightGrip();
                case 2:
                    return Input.GetKey(KeyCode.H);
                default:
                    return false;
            }
        }

        public static bool GetLeftGrip()
        {
            if (!initialized)
            {
                Init();
            }

            switch (platform)
            {
                case 0:
                    return rumbleEnahncerInstalled ? OpenVRLeftGripWithLock() : OpenVRLeftGrip();
                case 1:
                    return OculusLeftGrip();
                case 2:
                    return Input.GetKey(KeyCode.H);
                default:
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

        private static int _leftControllerIndex = -1;
        private static int _rightControllerIndex = -1;

        private static bool OpenVRLeftGrip()
        {
            if (_leftControllerIndex == -1)
            {
                _leftControllerIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);
            }

            return SteamVR_Controller.Input(_leftControllerIndex).GetPress(SteamVR_Controller.ButtonMask.Grip);
        }

        private static bool OpenVRLeftGripWithLock()
        {
            lock (Rumbleenhancer.Plugin.asyncRumbleLock)
            {
                if (_leftControllerIndex == -1)
                {
                    _leftControllerIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);
                }

                return SteamVR_Controller.Input(_leftControllerIndex).GetPress(SteamVR_Controller.ButtonMask.Grip);
            }
        }

        private static bool OpenVRRightGrip()
        {
            if (_rightControllerIndex == -1)
            {
                _rightControllerIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);
            }

            return SteamVR_Controller.Input(_rightControllerIndex).GetPress(SteamVR_Controller.ButtonMask.Grip);
        }

        private static bool OpenVRRightGripWithLock()
        {
            lock (Rumbleenhancer.Plugin.asyncRumbleLock)
            {
                if (_rightControllerIndex == -1)
                {
                    _rightControllerIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);
                }

                return SteamVR_Controller.Input(_rightControllerIndex).GetPress(SteamVR_Controller.ButtonMask.Grip);
            }
        }




    }





}
