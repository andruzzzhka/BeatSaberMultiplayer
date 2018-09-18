using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer.Misc
{
    static class SceneEvents
    {
        static AsyncScenesLoader loader = null;
        public static AsyncScenesLoader GetSceneLoader()
        {
            if (SceneManager.GetActiveScene().name == "StandardLevelLoader")
            {
                if (loader == null)
                {
                    loader = Resources.FindObjectsOfTypeAll<AsyncScenesLoader>().FirstOrDefault();
                }
                return loader;
            }
            return null;
        }
    }
}
