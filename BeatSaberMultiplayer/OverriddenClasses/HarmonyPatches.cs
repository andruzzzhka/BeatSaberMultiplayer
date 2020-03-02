using BeatSaberMultiplayer.Data;
using Harmony;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public static class HarmonyPatcher
    {
        public static HarmonyInstance instance;

        public static void Patch()
        {   
            if(instance == null)
                instance = HarmonyInstance.Create("com.andruzzzhka.BeatSaberMultiplayer");

            Plugin.log.Debug("Patching...");
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(x => x.IsClass && x.Namespace == "BeatSaberMultiplayer.OverriddenClasses"))
            {
                List<HarmonyMethod> harmonyMethods = type.GetHarmonyMethods();
                if (harmonyMethods != null && harmonyMethods.Count > 0)
                {
                    HarmonyMethod attributes = HarmonyMethod.Merge(harmonyMethods);
                    PatchProcessor patchProcessor = new PatchProcessor(instance, type, attributes);
                    patchProcessor.Patch();
                    Plugin.log.Debug($"Patched {attributes.declaringType}.{attributes.methodName}!");
                }
            }
            Plugin.log.Info("Applied Harmony patches!");
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectSpawnController))]
    [HarmonyPatch("HandleNoteWasCut")]
    [HarmonyPatch(new Type[] { typeof(NoteController), typeof(NoteCutInfo) })]
    class SpectatorNoteWasCutEventPatch
    {
        static bool Prefix(BeatmapObjectSpawnController __instance, NoteController noteController, NoteCutInfo noteCutInfo)
        {
            try
            {
                if (Config.Instance.SpectatorMode && SpectatingController.Instance != null && SpectatingController.active && Client.Instance != null && Client.Instance.connected && SpectatingController.Instance.spectatedPlayer != null && SpectatingController.Instance.spectatedPlayer.playerInfo != null)
                {
                    ulong playerId = SpectatingController.Instance.spectatedPlayer.playerInfo.playerId;

                    if (SpectatingController.Instance.playerUpdates.ContainsKey(playerId) && SpectatingController.Instance.playerUpdates[playerId].hits.Count > 0)
                    {
                        if (SpectatingController.Instance.playerUpdates[playerId].hits.TryGetValue(noteController.noteData.id, out HitData hit))
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
                if (Config.Instance.SpectatorMode && SpectatingController.Instance != null && SpectatingController.active && Client.Instance != null && Client.Instance.connected && SpectatingController.Instance.spectatedPlayer != null && SpectatingController.Instance.spectatedPlayer.playerInfo != null)
                {
                    ulong playerId = SpectatingController.Instance.spectatedPlayer.playerInfo.playerId;

                    if (SpectatingController.Instance.playerUpdates.ContainsKey(playerId) && SpectatingController.Instance.playerUpdates[playerId].hits.Count > 0)
                    {
                        if (SpectatingController.Instance.playerUpdates[playerId].hits.TryGetValue(noteController.noteData.id, out HitData hit))
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
                if (Config.Instance.SpectatorMode && SpectatingController.Instance != null && SpectatingController.active && Client.Instance != null && Client.Instance.connected && SpectatingController.Instance.spectatedPlayer != null && SpectatingController.Instance.spectatedPlayer.playerInfo != null)
                {
                    if (__instance.energy + value <= 1E-05f && SpectatingController.Instance.spectatedPlayer.playerInfo.updateInfo.playerEnergy > 1E-04f)
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

    [HarmonyPatch(typeof(PauseController))]
    [HarmonyPatch("Pause")]
    class GameplayManagerPausePatch
    {
        static bool Prefix(StandardLevelGameplayManager __instance, PauseMenuManager ____pauseMenuManager)
        {
            try
            {
                if (Client.Instance.connected)
                {
                    ____pauseMenuManager.ShowMenu();
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Plugin.log.Error("Exception in Harmony patch StandardLevelGameplayManager.Pause: " + e);
                return true;
            }
        }
    }
}
