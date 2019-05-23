using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using BS_Utils.Gameplay;
using CustomUI.Utilities;
using IPA;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class Plugin : IBeatSaberPlugin
    {
        public static Plugin instance;
        public static IPA.Logging.Logger log;

        public void Init(object nullObject, IPA.Logging.Logger logger)
        {
            log = logger;
        }

        public void OnApplicationStart()
        {
#if DEBUG
            if (Environment.CommandLine.Contains("fpfc"))
            {
                QualitySettings.vSyncCount = 2;
                Application.targetFrameRate = 60;
            }
#endif
            
            instance = this;

#if DEBUG
            DebugForm.OnLoad();
#endif

            BSEvents.OnLoad();
            BSEvents.menuSceneLoadedFresh += MenuSceneLoadedFresh;
            BSEvents.menuSceneLoaded += MenuSceneLoaded;
            BSEvents.gameSceneLoaded += GameSceneLoaded;

            if (Config.Load())
                log.Info("Loaded config!");
            else
                Config.Create();

            try
            {
                PresetsCollection.ReloadPresets();
            }
            catch (Exception e)
            {
                log.Warn("Unable to load presets! Exception: "+e);
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

        public void OnApplicationQuit()
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
        }

        public void OnSceneUnloaded(Scene scene)
        {
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
        }
    }
}
