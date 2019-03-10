using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Harmony;
using BS_Utils;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    [HarmonyPatch(typeof(GameEnergyCounter), "AddEnergy",
        new Type[] {
        typeof(float)})]
    class OnlineEnergyCounter
    {
        static bool Prefix(GameEnergyCounter __instance, ref float value)
        {
            if (BailOutController.BailOutEnabled && value < 0f)
            {
                if (__instance.energy + value <= 0)
                {
                    if(BailOutController.numFails == 0)
                        Misc.Logger.Info("Score submission disabled by BailOutMode");
                    BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Plugin.instance.Name);
                    BailOutController.numFails++;
                    value = (BailOutController.EnergyResetAmount / 100f) - __instance.energy;
                    BailOutController.Instance.ShowLevelFailed();
                }
            }
            return true;
        }
    }
}
