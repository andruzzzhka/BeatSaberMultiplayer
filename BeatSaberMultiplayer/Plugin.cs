using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using IllusionPlugin;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class Plugin : IPlugin
    {
        public string Name => "Beat Saber Multiplayer";

        public string Version => "0.6.0.1";
        public static uint pluginVersion = 601;

        public static Plugin instance;

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
            if (from.name == "EmptyTransition" && to.name == "Menu")
            {
                PluginUI.OnLoad();
                InGameOnlineController.OnLoad(to);
                SpectatingController.OnLoad();
            }
            else
            {
                InGameOnlineController.Instance?.ActiveSceneChanged(from, to);
                if(Config.Instance.SpectatorMode)
                    SpectatingController.Instance?.ActiveSceneChanged(from, to);
            }
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
