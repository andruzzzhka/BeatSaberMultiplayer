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

        public string Version => "0.5.1.7";
        public static uint pluginVersion = 517;

        public static Plugin instance;

        public void OnApplicationQuit()
        {
        }

        public void OnApplicationStart()
        {
            if(File.Exists("MPLog.txt"))
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
            Base64Sprites.ConvertSprites();
        }

        private void ActiveSceneChanged(Scene from, Scene to)
        {
#if DEBUG
            Logger.Log($"Active scene changed from \"{from.name}\" to \"{to.name}\"");
#endif
            if (from.name == "EmptyTransition" && to.name == "Menu")
            {
                PluginUI.OnLoad();
                InGameOnlineController.OnLoad();
                SpectatingController.OnLoad();
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
