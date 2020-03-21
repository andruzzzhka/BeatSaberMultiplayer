using CustomAvatar;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
        public static int calculatedHashesCount;
        public static int totalAvatarsCount;

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
                Plugin.log.Error($"Unable to download avatar! {(www.isNetworkError ? $"Network error: " + www.error : (www.isHttpError ? $"HTTP error: " + www.error : "Unknown error"))}");
                queuedAvatars.Remove(hash);
                yield break;
            }
            else
            {
                Plugin.log.Debug("Received response from ModelSaber...");
                JSONNode node = JSON.Parse(www.downloadHandler.text);

                if (node.Count == 0)
                {
                    Plugin.log.Error($"Avatar with hash {hash} doesn't exist on ModelSaber!");
                    cachedAvatars.Add(hash, null);
                    queuedAvatars.Remove(hash);
                    yield break;
                }

                downloadUrl = node[0]["download"].Value;
                avatarName = downloadUrl.Substring(downloadUrl.LastIndexOf("/") + 1);
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                queuedAvatars.Remove(hash);
                yield break;
            }


            bool timeout = false;
            float time = 0f;
            UnityWebRequestAsyncOperation asyncRequest;

            try
            {
                www = SongDownloader.GetRequestForUrl(downloadUrl);
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
                Plugin.log.Debug("Received response from ModelSaber...");
                string docPath = "";
                string customAvatarPath = "";

                byte[] data = www.downloadHandler.data;

                try
                {
                    docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    customAvatarPath = docPath + "/CustomAvatars/" + avatarName;

                    Plugin.log.Debug($"Saving avatar to \"{customAvatarPath}\"...");

                    File.WriteAllBytes(customAvatarPath, data);

                    Plugin.log.Debug("Downloaded avatar!");
                    Plugin.log.Debug($"Loading avatar...");

                    SharedCoroutineStarter.instance.StartCoroutine(CustomAvatar.CustomAvatar.FromFileCoroutine(avatarName,
                        (CustomAvatar.CustomAvatar avatar) =>
                        {
                            queuedAvatars.Remove(hash);
                            cachedAvatars.Add(hash, avatar);
                            avatarDownloaded?.Invoke(hash);
                        }, (Exception ex) =>
                        {
                            Plugin.log.Error($"Unable to load avatar! Exception: {ex}");
                            queuedAvatars.Remove(hash);
                        }));


                }
                catch (Exception e)
                {
                    Plugin.log.Critical(e);
                    queuedAvatars.Remove(hash);
                    yield break;
                }
            }
        }

        public static async void HashAllAvatars()
        {
            int totalAvatarsCount = Directory.GetFiles("CustomAvatars", "*.avatar").Length;

            if (totalAvatarsCount != cachedAvatars.Count && !isCalculatingHashes)
            {
                isCalculatingHashes = true;
                Plugin.log.Debug($"Hashing avatars... {totalAvatarsCount} avatars found");
                try
                {
                    cachedAvatars.Clear();
                    calculatedHashesCount = 0;

                    AvatarManager.instance.GetAvatarsAsync(
                        async (CustomAvatar.CustomAvatar avatar) =>
                        {
                            try
                            {
                                string calculatedHash = await AvatarsHashCache.GetHashForAvatar(avatar).ConfigureAwait(false);
                                cachedAvatars.Add(calculatedHash, avatar);
                                Plugin.log.Debug($"Hashed avatar \"{avatar.descriptor.name}\"! Hash: {calculatedHash}");
                            }
                            catch (Exception ex)
                            {
                                Plugin.log.Error($"Unable to hash avatar \"{avatar.descriptor.name}\"! Exception: {ex}");
                            }
                            calculatedHashesCount++;
                        }, (Exception ex) =>
                        {
                            Plugin.log.Error($"Unable to load avatar! Exception: {ex}");
                            calculatedHashesCount++;
                        });

                    while (totalAvatarsCount != calculatedHashesCount)
                    {
                        await Task.Delay(11);
                    }

                    Plugin.log.Debug("All avatars hashed and loaded!");

                    HMMainThreadDispatcher.instance.Enqueue(() =>
                    {
                        hashesCalculated?.Invoke();
                    });
                }
                catch (Exception e)
                {
                    Plugin.log.Error($"Unable to hash and load avatars! Exception: {e}");
                }
                isCalculatingHashes = false;
            }
        }
    }
}
