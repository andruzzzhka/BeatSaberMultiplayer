using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.ViewControllers.RoomScreen;
using SimpleJSON;
using SongLoaderPlugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class DownloadFlowCoordinator : FlowCoordinator
    {

        private string beatSaverURL = "https://beatsaver.com";

        private DownloadQueueViewController _downloadQueueViewController;
        private RoomNavigationController _roomNavigationController;

        public event Action AllSongsDownloaded;

        public void Initialize(RoomNavigationController roomNavigationController)
        {
            _roomNavigationController = roomNavigationController;
            
        }

        public void SongsDownloaded()
        {
#if DEBUG
            Log.Info("All songs downloaded!");
#endif
            SongLoader.SongsLoadedEvent -= SongsLoadedEvent;
            SongLoader.SongsLoadedEvent += SongsLoadedEvent;
            SongLoader.Instance.RefreshSongs(false);
            if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_downloadQueueViewController) >= 0)
            {
                _roomNavigationController.PopViewControllerImmediately();
                Destroy(_downloadQueueViewController.gameObject);
            }
        }

        private void SongsLoadedEvent(SongLoader sender, List<SongLoaderPlugin.OverrideClasses.CustomLevel> loadedSongs)
        {
            AllSongsDownloaded?.Invoke();
        }

        public IEnumerator EnqueueSongByLevelID(string levelId)
        {
#if DEBUG
            Log.Info("Downloading " + levelId);
#endif
            if (_downloadQueueViewController == null)
            {
                _downloadQueueViewController = BeatSaberUI.CreateViewController<DownloadQueueViewController>();
            }
            if (_roomNavigationController.GetPrivateField<List<VRUIViewController>>("_viewControllers").IndexOf(_downloadQueueViewController) < 0)
            {
                _roomNavigationController.PushViewController(_downloadQueueViewController, false);
            }

            UnityWebRequest wwwId = UnityWebRequest.Get($"{beatSaverURL}/api/songs/search/hash/" + levelId.Substring(0, 32));
            wwwId.timeout = 10;

            yield return wwwId.SendWebRequest();


            if (wwwId.isNetworkError || wwwId.isHttpError)
            {
                Console.WriteLine(wwwId.error);
                _downloadQueueViewController.DisplayError(wwwId.error);
            }
            else
            {
#if DEBUG
                Console.WriteLine("Received response from BeatSaver");
#endif
                JSONNode node = JSON.Parse(wwwId.downloadHandler.text);

                if (node["songs"].Count == 0)
                {
                    _downloadQueueViewController.DisplayError($"Song {levelId} doesn't exist!");
                    yield break;
                }

                Song _tempSong = Song.FromSearchNode(node["songs"][0]);

                _downloadQueueViewController.EnqueueSong(_tempSong);
            }
        }

        public IEnumerator DownloadSongCoroutine(Song songInfo)
        {
            songInfo.songQueueState = SongQueueState.Downloading;

            UnityWebRequest www = UnityWebRequest.Get(songInfo.downloadUrl);

            bool timeout = false;
            float time = 0f;

            UnityWebRequestAsyncOperation asyncRequest = www.SendWebRequest();

            while (!asyncRequest.isDone || asyncRequest.progress < 1f)
            {
                yield return null;

                time += Time.deltaTime;

                if (time >= 15f && asyncRequest.progress == 0f)
                {
                    www.Abort();
                    timeout = true;
                }

                songInfo.downloadingProgress = asyncRequest.progress;
            }


            if (www.isNetworkError || www.isHttpError || timeout)
            {
                if (timeout)
                {
                    songInfo.songQueueState = SongQueueState.Error;
                    TextMeshProUGUI _errorText = BeatSaberUI.CreateText(_downloadQueueViewController.rectTransform, "Request timeout", new Vector2(18f, -64f));
                    Destroy(_errorText.gameObject, 2f);
                }
                else
                {
                    songInfo.songQueueState = SongQueueState.Error;
                    Log.Error($"Downloading error: {www.error}");
                    TextMeshProUGUI _errorText = BeatSaberUI.CreateText(_downloadQueueViewController.rectTransform, www.error, new Vector2(18f, -64f));
                    Destroy(_errorText.gameObject, 2f);
                }
                StartCoroutine(DownloadSongCoroutine(songInfo));
            }
            else
            {
#if DEBUG
                Log.Info("Received response from BeatSaver.com...");
#endif

                string zipPath = "";
                string docPath = "";
                string customSongsPath = "";

                byte[] data = www.downloadHandler.data;

                try
                {

                    docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    customSongsPath = docPath + "/CustomSongs/" + songInfo.id + "/";
                    zipPath = customSongsPath + songInfo.id + ".zip";
                    if (!Directory.Exists(customSongsPath))
                    {
                        Directory.CreateDirectory(customSongsPath);
                    }
                    File.WriteAllBytes(zipPath, data);
#if DEBUG
                    Log.Info("Downloaded zip file!");
#endif
                }
                catch (Exception e)
                {
                    Log.Exception("EXCEPTION: " + e);
                    songInfo.songQueueState = SongQueueState.Error;
                    yield break;
                }

#if DEBUG
                Log.Info("Extracting...");
#endif

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, customSongsPath);
                }
                catch (Exception e)
                {
                    Log.Exception($"Can't extract ZIP! Exception: {e}");
                }

                songInfo.path = Directory.GetDirectories(customSongsPath).FirstOrDefault();

                try
                {
                    File.Delete(zipPath);
                }
                catch (IOException e)
                {
                    Log.Warning($"Can't delete zip! Exception: {e}");
                }

                songInfo.songQueueState = SongQueueState.Downloaded;

                Log.Info($"Downloaded {songInfo.songName} {songInfo.songSubName}!");
                _downloadQueueViewController.Refresh();

                if(!_downloadQueueViewController._queuedSongs.Any(x => x.songQueueState == SongQueueState.Downloading || x.songQueueState == SongQueueState.Queued))
                {
                    SongsDownloaded();
                }
            }
        }
    }
}
