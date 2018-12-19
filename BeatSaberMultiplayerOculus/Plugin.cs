using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using IllusionPlugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class Plugin : IPlugin
    {
        public string Name => "Beat Saber Multiplayer";

        public string Version => "0.5.3.0";
        public static uint pluginVersion = 530;

        public static Plugin instance;

        public void OnApplicationQuit()
        {
        }

        public void OnApplicationStart()
        {

            if (File.Exists("MPLog.txt"))
                File.Delete("MPLog.txt");
            
            instance = this;

#if DEBUG
            DebugForm.OnLoad();
#endif

            SceneManager.activeSceneChanged += ActiveSceneChanged;
            if (Config.Load())
                Logger.Info("Loaded config!");
            else
                Config.Create();
            try
            {
                PresetsCollection.ReloadPresets();
            }
            catch (Exception e)
            {
                Logger.Warning("Unable to load presets! Exception: "+e);
            }
            Base64Sprites.ConvertSprites();
            
        }

        private void ActiveSceneChanged(Scene from, Scene to)
        {
#if DEBUG
           Logger.Info($"Active scene changed from \"{from.name}\" to \"{to.name}\"");
#endif
            if (from.name == "EmptyTransition" && to.name == "Menu")
            {
                PluginUI.OnLoad();
                InGameOnlineController.OnLoad();
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
