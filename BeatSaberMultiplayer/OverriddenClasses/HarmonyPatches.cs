using BeatSaberMultiplayer.Data;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    [HarmonyPatch(typeof(BeatmapObjectSpawnController))]
    [HarmonyPatch("HandleNoteWasCut")]
    [HarmonyPatch(new Type[] { typeof(NoteController), typeof(NoteCutInfo) })]
    class SpectatorNoteWasCutEventPatch
    {
        static bool Prefix(BeatmapObjectSpawnController __instance, NoteController noteController, NoteCutInfo noteCutInfo)
        {
            try
            {
                if (Config.Instance.SpectatorMode && SpectatingController.Instance != null && SpectatingController.active && Client.Instance != null && Client.Instance.connected)
                {
                    ulong playerId = SpectatingController.Instance.spectatedPlayer.PlayerInfo.playerId;

                    if (SpectatingController.Instance.playersHits.ContainsKey(playerId) && SpectatingController.Instance.playersHits[playerId].Count > 0)
                    {
                        if (SpectatingController.Instance.playersHits[playerId].TryGetValue(noteController.noteData.id, out HitData hit))
                        {
                            bool allIsOKExpected = hit.noteWasCut && hit.speedOK && hit.saberTypeOK && hit.directionOK && !hit.wasCutTooSoon;

                            if (hit.noteWasCut)
                            {
                                if (noteCutInfo.allIsOK == allIsOKExpected)
                                {
                                    return true;
                                }
                                else if (!noteCutInfo.allIsOK && allIsOKExpected)
                                {
#if DEBUG
                                Plugin.log.Warn("Oopsie, we missed it, let's forget about that");
#endif
                                    __instance.Despawn(noteController);

                                    return false;
                                }
                                else if (noteCutInfo.allIsOK && !allIsOKExpected)
                                {
#if DEBUG
                                Plugin.log.Warn("We cut the note, but the player cut it wrong");
#endif

                                    noteCutInfo.SetPrivateProperty("wasCutTooSoon", hit.wasCutTooSoon);
                                    noteCutInfo.SetPrivateProperty("directionOK", hit.directionOK);
                                    noteCutInfo.SetPrivateProperty("saberTypeOK", hit.saberTypeOK);
                                    noteCutInfo.SetPrivateProperty("speedOK", hit.speedOK);

                                    return true;
                                }
                            }
                            else
                            {
#if DEBUG
                            Plugin.log.Warn("We cut the note, but the player missed it");
#endif
                                __instance.HandleNoteWasMissed(noteController);

                                return false;
                            }
                        }
                    }

                    return true;
                }
                else
                {
                    return true;
                }
            }catch(Exception e)
            {
                Plugin.log.Error("Exception in Harmony patch BeatmapObjectSpawnController.NoteWasCut: " + e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectSpawnController))]
    [HarmonyPatch("HandleNoteWasMissed")]
    [HarmonyPatch(new Type[] { typeof(NoteController) })]
    class SpectatorNoteWasMissedEventPatch
    {
        static bool Prefix(BeatmapObjectSpawnController __instance, NoteController noteController)
        {
            try
            {
                if (Config.Instance.SpectatorMode && SpectatingController.Instance != null && SpectatingController.active && Client.Instance != null && Client.Instance.connected)
                {
                    ulong playerId = SpectatingController.Instance.spectatedPlayer.PlayerInfo.playerId;

                    if (SpectatingController.Instance.playersHits.ContainsKey(playerId) && SpectatingController.Instance.playersHits[playerId].Count > 0)
                    {
                        if (SpectatingController.Instance.playersHits[playerId].TryGetValue(noteController.noteData.id, out HitData hit))
                        {
                            if (hit.noteWasCut)
                            {
#if DEBUG
                            Plugin.log.Warn("We missed the note, but the player cut it");
#endif
                                __instance.Despawn(noteController);
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }

                    return true;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Plugin.log.Error("Exception in Harmony patch BeatmapObjectSpawnController.NoteWasMissed: " + e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(GameEnergyCounter))]
    [HarmonyPatch("AddEnergy")]
    [HarmonyPatch(new Type[] { typeof(float) })]
    class SpectatorGameEnergyCounterPatch
    {
        static bool Prefix(GameEnergyCounter __instance, float value)
        {
            try
            {
                if (Config.Instance.SpectatorMode && SpectatingController.Instance != null && SpectatingController.active && Client.Instance != null && Client.Instance.connected)
                {
                    if (__instance.energy + value <= 1E-05f && SpectatingController.Instance.spectatedPlayer.PlayerInfo.playerEnergy > 1E-04f)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Plugin.log.Error("Exception in Harmony patch GameEnergyCounter.AddEnergy: " + e);
                return true;
            }
        }
    }
}
