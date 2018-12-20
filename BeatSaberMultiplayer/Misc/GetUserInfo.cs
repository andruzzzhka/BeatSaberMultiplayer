using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeatSaberMultiplayer.Misc
{
    public static class GetUserInfo
    {
        static string userName;
        static ulong userID;

        static GetUserInfo()
        {
            UpdateUserInfo();
        }

        public static void UpdateUserInfo()
        {
            if (userID == 0 || userName == null)
            {
                userName = SteamFriends.GetPersonaName();
                userID = SteamUser.GetSteamID().m_SteamID;
            }
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
