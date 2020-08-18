extern alias ScoreSaberGlobal;
using ScoreSaberGlobal.ScoreSaber;
using System;

namespace BeatSaberMultiplayer.Interop
{
    internal static class ScoreSaberInterop
    {
        public static void InitAndSignIn()
        {
            try
            {
                Handler.instance.Initialize();
                Handler.instance.SignIn();
            }
            catch(Exception e)
            {
                Plugin.log.Warn($"Unable to sign in to ScoreSaber! Score submission may not work properly.\nException: {e}");
            }
        }
    }
}
