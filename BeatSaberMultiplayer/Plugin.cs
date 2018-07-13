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

        public string Version => "0.4.4.2";

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
        }

        public void OnApplicationQuit()
        {
        }

        public void OnApplicationStart()
        {
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            Console.WriteLine("Loading scene " + level);
            if (level == 1) {
                Config.Instance.Save();
                BSMultiplayerUI.OnLoad();
                BSMultiplayerClient.OnLoad(level, Version);
            }
            else if (level > 1)
            {
                BSMultiplayerClient.OnLoad(level, Version);
            }
        }

        public void OnUpdate()
        {
        }
    }
}
