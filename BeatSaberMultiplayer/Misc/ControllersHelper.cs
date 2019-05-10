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

        public static void Init()
        {
            rumbleEnahncerInstalled = IPA.Loader.PluginManager.AllPlugins.Any(x => x.Metadata.Id == "rumbleenhancer");
            Plugin.log.Info("Rumble Enahncer installed: "+ rumbleEnahncerInstalled);
            initialized = true;
        }

        public static bool GetRightGrip()
        {
            if (!initialized)
            {
                Init();
            }

            if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
            {
                return rumbleEnahncerInstalled ? OpenVRRightGrip() : OpenVRRightGripWithLock();
            }
            else if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                return OculusRightGrip();
            }
            else if (Environment.CommandLine.Contains("fpfc") && VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Unknown)
            {
                return Input.GetKey(KeyCode.H);
            }
            else
            {
                return false;
            }
        }

        public static bool GetLeftGrip()
        {
            if (!initialized)
            {
                Init();
            }

            if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR)
            {
                return rumbleEnahncerInstalled ? OpenVRLeftGrip() : OpenVRLeftGripWithLock();
            }
            else if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                return OculusLeftGrip();
            }
            else if (Environment.CommandLine.Contains("fpfc") && VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Unknown)
            {
                return Input.GetKey(KeyCode.H);
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

        private static int _leftControllerIndex = -1;
        private static int _rightControllerIndex = -1;

        private static bool OpenVRLeftGrip()
        {
            if (_leftControllerIndex == -1)
            {
                _leftControllerIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);
                Plugin.log.Info("Leftmost controller index: " + _leftControllerIndex);
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
                    Plugin.log.Info("Leftmost controller index: " + _leftControllerIndex);
                }

                return SteamVR_Controller.Input(_leftControllerIndex).GetPress(SteamVR_Controller.ButtonMask.Grip);
            }
        }

        private static bool OpenVRRightGrip()
        {
            if (_rightControllerIndex == -1)
            {
                _rightControllerIndex = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);
                Plugin.log.Info("Rightmost controller index: " + _rightControllerIndex);
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
                    Plugin.log.Info("Rightmost controller index: " + _rightControllerIndex);
                }

                return SteamVR_Controller.Input(_rightControllerIndex).GetPress(SteamVR_Controller.ButtonMask.Grip);
            }
        }




    }





}
