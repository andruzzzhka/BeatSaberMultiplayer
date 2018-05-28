using Oculus.Platform;
using Oculus.Platform.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BeatSaberMultiplayer.Misc
{
    static class GetUserInfo
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
                Users.GetLoggedInUser().OnComplete((Message<User> msg) =>
                {
                    userID = msg.Data.ID;
                    userName = msg.Data.OculusID;
                });
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
