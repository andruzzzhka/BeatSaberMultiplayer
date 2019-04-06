using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using BS_Utils.Gameplay;
using IllusionPlugin;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class Plugin : IPlugin
    {
        public string Name => "Beat Saber Multiplayer";

        public string Version => "0.6.2.1";

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

            BSEvents.OnLoad();
            BSEvents.menuSceneLoadedFresh += MenuSceneLoadedFresh;
            BSEvents.menuSceneLoaded += MenuSceneLoaded;
            BSEvents.gameSceneLoaded += GameSceneLoaded;

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

        private void MenuSceneLoadedFresh()
        {
            ModelSaberAPI.HashAllAvatars();
            PluginUI.OnLoad();
            InGameOnlineController.OnLoad();
            SpectatingController.OnLoad();
            GetUserInfo.UpdateUserInfo();
#if DEBUG
            DebugForm.MenuLoaded();
#endif
        }

        private void MenuSceneLoaded()
        {
            InGameOnlineController.Instance?.MenuSceneLoaded();
            if (Config.Instance.SpectatorMode)
                SpectatingController.Instance?.MenuSceneLoaded();
#if DEBUG
            DebugForm.MenuLoaded();
#endif
        }

        private void GameSceneLoaded()
        {
            InGameOnlineController.Instance?.GameSceneLoaded();
            if (Config.Instance.SpectatorMode)
                SpectatingController.Instance?.GameSceneLoaded();
#if DEBUG
            DebugForm.GameLoaded();
#endif
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
