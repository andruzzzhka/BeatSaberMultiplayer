using ScoreSaber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.Interop
{
    internal static class ScoreSaberInterop
    {
        public static void InitAndSignIn()
        {
            Handler.instance.Initialize();
            Handler.instance.SignIn();
        }
    }
}
