using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSaberMultiplayer.Misc
{
    class LoadScripts
    {
        static public Dictionary<string, Texture2D> _cachedTextures = new Dictionary<string, Texture2D>();

        static public IEnumerator LoadSpriteCoroutine(string spritePath, Action<Texture2D> done)
        {
            Texture2D tex;

            if (_cachedTextures.ContainsKey(spritePath))
            {
                done?.Invoke(_cachedTextures[spritePath]);
                yield break;
            }

            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(spritePath))
            {
                yield return www.SendWebRequest();

                if (www.isHttpError || www.isNetworkError)
                {
                    Plugin.log.Warn("Unable to download sprite! Exception: "+www.error);
                }
                else
                {
                    tex = DownloadHandlerTexture.GetContent(www);
                    _cachedTextures.Add(spritePath, tex);
                    done?.Invoke(tex);
                }
            }
        }

    }
}
