using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using IllusionPlugin;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using Harmony;
using System.Reflection;

namespace BeatSaberMultiplayer
{
    public class Plugin : IPlugin
    {
        public string Name => "Beat Saber Multiplayer";

        public string Version => "0.6.1.4";
        public static uint pluginVersion = 614;

        public static Plugin instance;

        private GameScenesManager _scenesManager;
        private GameScenesManager _gameScenesManager
        {
            get
            {
                if (_scenesManager == null)
                {
                    _scenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();
                }
                return _scenesManager;
            }
        }
        public void OnApplicationQuit()
        {
        }

        public void OnApplicationStart()
        {
#if DEBUG
            if (Environment.CommandLine.Contains("fpfc"))
                QualitySettings.vSyncCount = 1;
#endif

            if (File.Exists("MPLog.txt"))
                File.Delete("MPLog.txt");
            
            instance = this;

            try
            {
                var harmony = HarmonyInstance.Create("com.github.andruzzzhka.BeatSaberMultiplayer");
                harmony.PatchAll(Assembly.GetExecutingAssembly());

            }
            catch (Exception ex)
            {
                Misc.Logger.Exception("This plugin requires Harmony. Make sure you " +
                    "installed the plugin properly, as the Harmony DLL should have been installed with it.\n" + ex);
            }
#if DEBUG
            DebugForm.OnLoad();
#endif

            SceneManager.activeSceneChanged += ActiveSceneChanged;
            if (Config.Load())
                Misc.Logger.Info("Loaded config!");
            else
                Config.Create();
            try
            {
                PresetsCollection.ReloadPresets();
            }
            catch (Exception e)
            {
                Misc.Logger.Warning("Unable to load presets! Exception: "+e);
            }
            Sprites.ConvertSprites();

            ScrappedData.Instance.DownloadScrappedData(null);
            
        }

        private void ActiveSceneChanged(Scene from, Scene to)
        {
#if DEBUG
           Misc.Logger.Info($"Active scene changed from \"{from.name}\" to \"{to.name}\"");
#endif
            if (to.name == "GameCore")
            {
                _gameScenesManager.transitionDidFinishEvent += OnSceneTransitionDidFinish;
                BailOutController.numFails = 0;
            }
            try
            {
                if (from.name == "EmptyTransition" && to.name == "Menu")
                {
                    ModelSaberAPI.HashAllAvatars();
                    PluginUI.OnLoad();
                    InGameOnlineController.OnLoad(to);
                    SpectatingController.OnLoad();
                }
                else
                {
                    InGameOnlineController.Instance?.ActiveSceneChanged(from, to);
                    if (Config.Instance.SpectatorMode)
                        SpectatingController.Instance?.ActiveSceneChanged(from, to);
                }
            }catch(Exception e)
            {
                Misc.Logger.Exception("Exception on active scene change: "+e);
            }
        }

        private void OnSceneTransitionDidFinish()
        {
            if (BailOutController.BailOutEnabled)
                new GameObject("BailOutController").AddComponent<BailOutController>();

        }

        public void OnFixedUpdate()
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnUpdate()
        {
        }
    }
}
