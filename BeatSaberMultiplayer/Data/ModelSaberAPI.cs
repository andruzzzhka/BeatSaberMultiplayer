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

        public static IEnumerator DownloadAvatarCoroutine(string hash)
        {
            queuedAvatars.Add(hash);
            string downloadUrl = "";
            string avatarName = "";
            UnityWebRequest www = UnityWebRequest.Get("https://modelsaber.com/api/v1/avatar/get.php?filter=hash:" + hash);
            
            www.timeout = 10;

            yield return www.SendWebRequest();
            
            if (www.isNetworkError || www.isHttpError)
            {
                Plugin.log.Error($"Unable to download avatar! {(www.isNetworkError ? $"Network error: " + www.error : (www.isHttpError ? $"HTTP error: "+www.error : "Unknown error"))}");
                queuedAvatars.Remove(hash);
                yield break;
            }
            else
            {
#if DEBUG
                Plugin.log.Info("Received response from ModelSaber...");
#endif
                JSONNode node = JSON.Parse(www.downloadHandler.text);

                if (node.Count == 0)
                {
                    Plugin.log.Error($"Avatar with hash {hash} doesn't exist on ModelSaber!");
                    cachedAvatars.Add(hash, null);
                    queuedAvatars.Remove(hash);
                    yield break;
                }

                downloadUrl = node[0]["download"].Value;
                avatarName = downloadUrl.Substring(downloadUrl.LastIndexOf("/")+1);
            }

            if(string.IsNullOrEmpty(downloadUrl))
            {
                queuedAvatars.Remove(hash);
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
                Plugin.log.Error($"Unable to download avatar! Exception: {e}");
                queuedAvatars.Remove(hash);
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
                    Plugin.log.Error("Connection timed out!");
                }
            }


            if (www.isNetworkError || www.isHttpError || timeout)
            {
                queuedAvatars.Remove(hash);
                Plugin.log.Error("Unable to download avatar! " + (www.isNetworkError ? $"Network error: {www.error}" : (www.isHttpError ? $"HTTP error: {www.error}" : "Unknown error")));
            }
            else
            {
#if DEBUG
                Plugin.log.Info("Received response from ModelSaber...");
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
                    Plugin.log.Info($"Saving avatar to \"{customAvatarPath}\"...");

                    CustomAvatar.CustomAvatar downloadedAvatar = CustomExtensions.CreateInstance<CustomAvatar.CustomAvatar>(customAvatarPath);

                    Plugin.log.Info("Downloaded avatar!");
                    Plugin.log.Info($"Loading avatar...");

                    downloadedAvatar.Load(
                        (CustomAvatar.CustomAvatar avatar, CustomAvatar.AvatarLoadResult result) => 
                        {
                            if (result == CustomAvatar.AvatarLoadResult.Completed)
                            {
                                queuedAvatars.Remove(hash);
                                cachedAvatars.Add(hash, downloadedAvatar);
                                avatarDownloaded?.Invoke(hash);
                            }
                            else
                            {
                                Plugin.log.Error("Unable to load avatar! " + result.ToString());
                            }
                        });
                    
                }
                catch (Exception e)
                {
                    Plugin.log.Critical(e);
                    queuedAvatars.Remove(hash);
                    yield break;
                }
            }
        }

        public static void HashAllAvatars()
        {
            if (cachedAvatars.Count != CustomAvatar.Plugin.Instance.AvatarLoader.Avatars.Count)
            {
                isCalculatingHashes = true;
                Plugin.log.Info("Hashing all avatars...");
                try
                {
                    cachedAvatars.Clear();
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
                                    Plugin.log.Info("Hashed avatar "+avatar.Name+"! Hash: "+hash);
#endif
                                }
                            }
                        }).ConfigureAwait(false);

                    }
                    Plugin.log.Info("All avatars hashed!");
                    hashesCalculated?.Invoke();
                }
                catch(Exception e)
                {
                    Plugin.log.Error($"Unable to hash avatars! Exception: {e}");
                }
                isCalculatingHashes = false;
            }
        }


    }
}
