using Oculus.Platform;
using Oculus.Platform.Models;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeatSaberMultiplayer.Misc
{
    public static class GetUserInfo
    {
        static string userName = null;
        static ulong userID = 0;
        
        public static void UpdateUserInfo()
        {
            if (userID == 0 || string.IsNullOrEmpty(userName))
            {
                if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR || Environment.CommandLine.Contains("-vrmode oculus"))
                {
                    Logger.Info("Attempting to Grab Steam User");
                    GetSteamUser();
                }
                else if (VRPlatformHelper.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
                {
                    Logger.Info("Attempting to Grab Oculus User");
                    GetOculusUser();
                }
                else
                {
                    Logger.Info("Unknown platform SDK: "+ VRPlatformHelper.instance.vrPlatformSDK+ "\nAttempting to Grab Steam User");
                    GetSteamUser();
                }
            }
        }


        internal static void GetSteamUser()
        {
            userName = SteamFriends.GetPersonaName();
            userID = SteamUser.GetSteamID().m_SteamID;
        }

        internal static void GetOculusUser()
        {
            Users.GetLoggedInUser().OnComplete((Message<User> msg) =>
            {
                if (!msg.IsError)
                {
                    userID = msg.Data.ID;
                    userName = msg.Data.OculusID;
                }
            });
        }
        public static string GetUserName()
        {
            return userName;
        }

        public static ulong GetUserID()
        {
            return userID;
        }

    }
}