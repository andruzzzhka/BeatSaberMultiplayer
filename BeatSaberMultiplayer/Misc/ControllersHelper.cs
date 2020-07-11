using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

namespace BeatSaberMultiplayer.Misc
{
    static class ControllersHelper
    {
        private static bool initialized;
        private static InputDevice leftController;
        private static InputDevice rightController;
        private static InputDevice head;

        private static int rightFailedTries = 0;
        private static int leftFailedTries = 0;
        private static int headFailedTries = 0;
        public static float TriggerThreshold = 0.85f;
        public static float GripThreshold = 0.85f;
        public static bool EnableHaptics = true;
        public static float HapticAmplitude = 0.5f;
        public static float HapticDuration = 0.01f;
        public static uint HapticChannel = 0;
        internal static InputDevice LeftController
        {
            get
            {
                if (leftController.isValid)
                {
                    leftFailedTries = 0;
                    return leftController;
                }
                leftFailedTries++;
                if (leftFailedTries > 100)
                {
                    leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                    leftFailedTries = 0;
                }
                return leftController;
            }
            set => leftController = value;
        }
        internal static InputDevice RightController
        {
            get
            {
                if (rightController.isValid)
                {
                    rightFailedTries = 0;
                    return rightController;
                }
                rightFailedTries++;
                if (rightFailedTries > 100)
                {
                    rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                    rightFailedTries = 0;
                }
                return rightController;
            }
            set => rightController = value;
        }
        internal static InputDevice Head
        {
            get
            {
                if (head.isValid)
                {
                    headFailedTries = 0;
                    return head;
                }
                headFailedTries++;
                if (headFailedTries > 100)
                {
                    head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                    headFailedTries = 0;
                }
                return head;
            }
            set => head = value;
        }

        /// <summary>
        /// MultiplayerLite's InputConfig: https://github.com/Zingabopp/BeatSaberMultiplayer/blob/MultiplayerLite/BeatSaberMultiplayer/Misc/Config.cs#L188
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        //public static void ReloadConfig(InputConfig config)
        //{
        //    if (config == null)
        //    {
        //        Plugin.log.Warn($"Unable to reload config, it's null.");
        //        return;
        //    }
        //    TriggerThreshold = config.TriggerInputThreshold;
        //    GripThreshold = config.GripInputThreshold;
        //    EnableHaptics = config.EnableHaptics;
        //    HapticAmplitude = config.HapticAmplitude;
        //    HapticDuration = config.HapticDuration;
        //}

        public static InputDevice GetInputDevice(XRNode node)
        {
            InputDevice device;
            switch (node)
            {
                case XRNode.LeftHand:
                    return LeftController;
                case XRNode.RightHand:
                    return RightController;
                case XRNode.Head:
                    return Head;
                //case XRNode.LeftEye:
                //    break;
                //case XRNode.RightEye:
                //    break;
                //case XRNode.CenterEye:
                //    break;
                //case XRNode.GameController:
                //    break;
                //case XRNode.TrackingReference:
                //    break;
                //case XRNode.HardwareTracker:
                //    break;
                default:
                    device = InputDevices.GetDeviceAtXRNode(node);
                    break;
            }
            return device;
        }

        public static void Init()
        {
            if (initialized)
                return;
            //ReloadConfig(Config.Instance?.VoiceChatSettings?.InputSettings);
            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;
            LeftController = GetInputDevice(XRNode.LeftHand);
            RightController = GetInputDevice(XRNode.RightHand);

            initialized = true;
        }

        private static void OnDeviceDisconnected(InputDevice device)
        {
            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right))
            {
                Plugin.log.Debug("Right controller disconnected.");
            }
            else if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left))
            {
                Plugin.log.Debug("Left controller disconnected.");
            }
        }

        private static void OnDeviceConnected(InputDevice device)
        {

            if (device.characteristics.HasFlag(InputDeviceCharacteristics.Right))
            {
                RightController = device;
                Plugin.log.Debug("Right controller connected.");
            }
            else if (device.characteristics.HasFlag(InputDeviceCharacteristics.Left))
            {
                LeftController = device;
                Plugin.log.Debug("Left controller connected.");
            }
        }
        public static bool RightTriggerActive
        {
            get
            {
                InputDevice device = RightController;
                if (device.isValid)
                {
                    if (device.TryGetFeatureValue(CommonUsages.trigger, out float value))
                    {
                        return value > TriggerThreshold;
                    }
                }
                return false;
            }
        }
        public static bool RightGripActive
        {
            get
            {
                InputDevice device = RightController;
                if (device.isValid)
                {
                    if (device.TryGetFeatureValue(CommonUsages.grip, out float value))
                    {
                        return value > GripThreshold;
                    }
                }
                return false;
            }
        }

        public static bool LeftTriggerActive
        {
            get
            {
                InputDevice device = LeftController;
                if (device.isValid)
                {
                    if (device.TryGetFeatureValue(CommonUsages.trigger, out float value))
                    {
                        return value > TriggerThreshold;
                    }
                }
                return false;
            }
        }

        public static bool LeftGripActive
        {
            get
            {
                InputDevice device = LeftController;
                if (device.isValid)
                {
                    if (device.TryGetFeatureValue(CommonUsages.grip, out float value))
                    {
                        return value > GripThreshold;
                    }
                }
                return false;
            }
        }

        public static void TriggerShortRumble(InputDevice device)
        {
            if (EnableHaptics && device.isValid)
                device.SendHapticImpulse(HapticChannel, HapticAmplitude, HapticDuration);
        }

        public static void TriggerShortRumble(XRNode node)
        {
            if (!EnableHaptics)
                return;
            if (node == XRNode.LeftHand)
            {
                InputDevice device = LeftController;
                if (device.isValid)
                    device.SendHapticImpulse(HapticChannel, HapticAmplitude, HapticDuration);
            }
            else if (node == XRNode.RightHand)
            {
                InputDevice device = RightController;
                if (device.isValid)
                    device.SendHapticImpulse(HapticChannel, HapticAmplitude, HapticDuration);
            }
        }

        public static float GetRightTrigger()
        {
            if (RightController.isValid)
            {
                if (RightController.TryGetFeatureValue(CommonUsages.trigger, out float value))
                {
                    return value;
                }
#if DEBUG
                else
                {
                    Plugin.log.Warn("RightController failed to get feature.");
                }
#endif
            }
#if DEBUG
            //else
            //{
            //    Plugin.log.Warn("RightController not valid.");
            //}
#endif
            return 0;
        }

        public static float GetRightGrip()
        {
            if (RightController.isValid)
            {
                if (RightController.TryGetFeatureValue(CommonUsages.grip, out float value))
                {
                    return value;
                }
#if DEBUG
                else
                {
                    Plugin.log.Warn("RightController failed to get feature.");
                }
#endif
            }
#if DEBUG
            //else
            //{
            //    Plugin.log.Warn("RightController not valid.");
            //}
#endif
            return 0;
        }
        public static float GetLeftTrigger()
        {
            if (LeftController.isValid)
            {
                if (LeftController.TryGetFeatureValue(CommonUsages.trigger, out float value))
                {
                    return value;
                }
#if DEBUG
                else
                {
                    Plugin.log.Warn("LeftController failed to get feature.");
                }
#endif
            }
#if DEBUG
            //else
            //{
            //    Plugin.log.Warn("LeftController not valid.");
            //}
#endif
            return 0;
        }
        public static float GetLeftGrip()
        {
            if (LeftController.isValid)
            {
                if (LeftController.TryGetFeatureValue(CommonUsages.grip, out float value))
                {
                    return value;
                }
#if DEBUG
                else
                {
                    Plugin.log.Warn("LeftController failed to get feature.");
                }
#endif
            }
#if DEBUG
            //else
            //{
            //    Plugin.log.Warn("LeftController not valid.");
            //}
#endif
            return 0;
        }

        public static void PrintDeviceInfo(InputDevice device)
        {
            Plugin.log.Error($"{device.name}");
            Plugin.log.Info($"Characteristics: {device.characteristics}");
            Plugin.log.Info($"isValid: {device.isValid}");
#pragma warning disable CS0618 // Type or member is obsolete
            Plugin.log.Info($"Role: {device.role}");
#pragma warning restore CS0618 // Type or member is obsolete
            var featureList = new List<InputFeatureUsage>();

            if (device.TryGetFeatureUsages(featureList))
            {
                Plugin.log.Info($"Features: {string.Join(", ", featureList.Select(f => $"{f.name}|{f.type}"))}");
            }
            else
                Plugin.log.Error("Unable to get features");
            if (device.TryGetHapticCapabilities(out HapticCapabilities hcap))
            {
                Plugin.log.Info($"HapticCapabilities: {hcap.HapticFeaturesString()}");
                Plugin.log.Info($"BufferValues: {hcap.HapticBufferValues()}");
            }
            else
                Plugin.log.Warn("Unable to get HapticCapabilities");
        }

        public static string HapticFeaturesString(this HapticCapabilities hcap)
        {
            return $"{nameof(hcap.numChannels)}: {hcap.numChannels} | {nameof(hcap.supportsBuffer)}: {hcap.supportsBuffer} | {nameof(hcap.supportsImpulse)}: {hcap.supportsImpulse}";
        }

        public static string HapticBufferValues(this HapticCapabilities hcap)
        {
            return $"bufferFrequencyHz: {hcap.bufferFrequencyHz} | {nameof(hcap.bufferMaxSize)}: {hcap.bufferMaxSize} | {nameof(hcap.bufferOptimalSize)}: {hcap.bufferOptimalSize}";
        }
    }





}