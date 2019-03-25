using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSaberMultiplayer.Misc
{
    public static class ModelSaberAPI
    {
        public static event Action<string> avatarDownloaded;
        public static event Action hashesCalculated;

        public static Dictionary<string, CustomAvatar.CustomAvatar> cachedAvatars = new Dictionary<string, CustomAvatar.CustomAvatar>();
        public static List<string> queuedAvatars = new List<string>();

        public static bool isCalculatingHashes;

        public static IEnumerator DownloadAvatarCoroutine(string hash, Action<string> callback)
        {
            queuedAvatars.Add(hash);
            string downloadUrl = "";
            string avatarName = "";
            UnityWebRequest www = UnityWebRequest.Get("https://modelsaber.com/api/v1/avatar/get.php?filter=hash:" + hash);
            
            www.timeout = 10;

            yield return www.SendWebRequest();
            
            if (www.isNetworkError || www.isHttpError)
            {
                Logger.Error(www.error);
                queuedAvatars.Remove(hash);
                callback?.Invoke(null);
                yield break;
            }
            else
            {
#if DEBUG
                Logger.Info("Received response from ModelSaber...");
#endif
                JSONNode node = JSON.Parse(www.downloadHandler.text);

                if (node.Count == 0)
                {
                    Logger.Error($"Avatar with hash {hash} doesn't exist on ModelSaber!");
                    cachedAvatars.Add(hash, null);
                    queuedAvatars.Remove(hash);
                    callback?.Invoke(null);
                    yield break;
                }

                downloadUrl = node[0]["download"].Value;
                avatarName = downloadUrl.Substring(downloadUrl.LastIndexOf("/")+1);
            }

            if(string.IsNullOrEmpty(downloadUrl))
            {
                queuedAvatars.Remove(hash);
                callback?.Invoke(null);
                yield break;
            }


            bool timeout = false;
            float time = 0f;
            UnityWebRequestAsyncOperation asyncRequest;

            try
            {
                www = UnityWebRequest.Get(downloadUrl);
                www.timeout = 0;

                asyncRequest = www.SendWebRequest();
            }
            catch (Exception e)
            {
                Logger.Error(e);
                queuedAvatars.Remove(hash);
                callback?.Invoke(null);
                yield break;
            }

            while (!asyncRequest.isDone)
            {
                yield return null;

                time += Time.deltaTime;

                if ((time >= 5f && asyncRequest.progress <= float.Epsilon))
                {
                    www.Abort();
                    timeout = true;
                    Logger.Error("Connection timed out!");
                }
            }


            if (www.isNetworkError || www.isHttpError || timeout)
            {
                queuedAvatars.Remove(hash);
                Logger.Error("Unable to download avatar! " + (www.isNetworkError ? $"Network error: {www.error}" : (www.isHttpError ? $"HTTP error: {www.error}" : "Unknown error")));
                callback?.Invoke(null);
            }
            else
            {
#if DEBUG
                Logger.Info("Received response from ModelSaber...");
#endif
                string docPath = "";
                string customAvatarPath = "";

                byte[] data = www.downloadHandler.data;
                
                try
                {
                    docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    customAvatarPath = docPath + "/CustomAvatars/" + avatarName;

                    File.WriteAllBytes(customAvatarPath, data);

                    CustomAvatar.CustomAvatar downloadedAvatar = CustomExtensions.CreateInstance<CustomAvatar.CustomAvatar>(customAvatarPath);
                    
                    queuedAvatars.Remove(hash);
                    cachedAvatars.Add(hash, downloadedAvatar);

                    downloadedAvatar.Load((CustomAvatar.CustomAvatar avatar, CustomAvatar.AvatarLoadResult result) => { if (result == CustomAvatar.AvatarLoadResult.Completed) { callback?.Invoke(hash); avatarDownloaded?.Invoke(hash); } else callback?.Invoke(null);  });
                    
                    Logger.Info("Downloaded avatar!");
                }
                catch (Exception e)
                {
                    Logger.Exception(e);
                    queuedAvatars.Remove(hash);
                    callback?.Invoke(null);
                    yield break;
                }
            }
        }

        public static void HashAllAvatars()
        {
            if (cachedAvatars.Count != CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.Count)
            {
                isCalculatingHashes = true;
                Misc.Logger.Info("Hashing all avatars...");
                try
                {
                    foreach (CustomAvatar.CustomAvatar avatar in CustomAvatar.Plugin.Instance.AvatarLoader.Avatars)
                    {
                        Task.Run(() =>
                        {
                            string hash;
                            if (SongDownloader.CreateMD5FromFile(avatar.FullPath, out hash))
                            {
                                if (!cachedAvatars.ContainsKey(hash))
                                {
                                    cachedAvatars.Add(hash, avatar);
#if DEBUG
                                Misc.Logger.Info("Hashed avatar "+avatar.Name+"! Hash: "+hash);
#endif
                            }
                            }
                        }).ConfigureAwait(false);

                    }
                    Misc.Logger.Info("All avatars hashed!");
                    hashesCalculated?.Invoke();
                }
                catch(Exception e)
                {
                    Misc.Logger.Warning($"Unable to hash avatars! Exception: {e}");
                }
                isCalculatingHashes = false;
            }
        }


    }
}
