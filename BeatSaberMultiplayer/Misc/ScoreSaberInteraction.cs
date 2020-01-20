using ScoreSaber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.Misc
{
    static class ScoreSaberInteraction
    {
        public static void FixScoreSaber(IDifficultyBeatmap difficultyBeatmap)
        {
            Resources.FindObjectsOfTypeAll<LevelSelectionNavigationController>().First().GetPrivateField<StandardLevelDetailViewController>("_levelDetailViewController").GetPrivateField<StandardLevelDetailView>("_standardLevelDetailView").SetPrivateField("_selectedDifficultyBeatmap", difficultyBeatmap);
        }

        public static void InitAndSignIn()
        {
            Handler.instance.Initialize();
            Handler.instance.SignIn();
        }
    }
}
