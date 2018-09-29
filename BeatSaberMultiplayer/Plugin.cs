using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using IllusionPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class Plugin : IPlugin
    {
        public string Name => "Beat Saber Multiplayer";

        public string Version => "0.5.1.5";
        public static uint pluginVersion = 515;

        public static Plugin instance;

        public void OnApplicationQuit()
        {
        }

        public void OnApplicationStart()
        {
            instance = this;

#if DEBUG
            DebugForm.OnLoad();
#endif

            SceneManager.sceneLoaded += SceneLoaded;
            if (Config.Load())
                Log.Info("Loaded config!");
            else
                Config.Create();
            Base64Sprites.ConvertSprites();
        }

        private void SceneLoaded(Scene nextScene, LoadSceneMode loadMode)
        {
#if DEBUG
            Log.Info("Loaded scene " + nextScene.name);
#endif
            if (nextScene.name == "Menu")
            {
                BeatSaberUI.OnLoad();
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
