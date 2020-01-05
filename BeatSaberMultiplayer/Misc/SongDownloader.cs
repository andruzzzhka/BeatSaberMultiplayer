using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSaberMultiplayer.Misc
{
    public class SongDownloader : MonoBehaviour
    {
        public event Action<Song> songDownloaded;

        private static SongDownloader _instance = null;
        public static SongDownloader Instance
        {
            get
            {
                if (!_instance)
                    _instance = new GameObject("SongDownloader").AddComponent<SongDownloader>();
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        private List<Song> _alreadyDownloadedSongs;
        private static bool _extractingZip;

        private static BeatmapLevelsModel _beatmapLevelsModel;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (!SongCore.Loader.AreSongsLoaded)
            {
                SongCore.Loader.SongsLoadedEvent += SongLoader_SongsLoadedEvent;
            }
            else
            {
                SongLoader_SongsLoadedEvent(null, SongCore.Loader.CustomLevels);
            }

        }

        private void SongLoader_SongsLoadedEvent(SongCore.Loader sender, Dictionary<string, CustomPreviewBeatmapLevel> levels)
        {
            _alreadyDownloadedSongs = levels.Values.Select(x => new Song(x)).ToList();
        }

        public void DownloadSong(Song songInfo, Action<bool> downloadedCallback, Action<float> progressChangedCallback)
        {
            StartCoroutine(DownloadSongCoroutine(songInfo, downloadedCallback, progressChangedCallback));
        }

        public IEnumerator DownloadSongCoroutine(Song songInfo, Action<bool> downloadedCallback, Action<float> progressChangedCallback)
        {
            songInfo.songQueueState = SongQueueState.Downloading;

            if (SongCore.Collections.songWithHashPresent(songInfo.hash.ToUpper()))
            {
                songInfo.downloadingProgress = 1f;
                yield return new WaitForSeconds(0.1f);
                songInfo.songQueueState = SongQueueState.Downloaded;
                songDownloaded?.Invoke(songInfo);
                downloadedCallback?.Invoke(true);
                yield break; 
            }
            UnityWebRequest www;
            bool timeout = false;
            float time = 0f;
            UnityWebRequestAsyncOperation asyncRequest;

            try
            {
                www = UnityWebRequest.Get(songInfo.downloadURL);

                asyncRequest = www.SendWebRequest();
            }
            catch (Exception e)
            {
                Plugin.log.Error(e);
                songInfo.songQueueState = SongQueueState.Error;
                songInfo.downloadingProgress = 1f;
                downloadedCallback?.Invoke(false);
                yield break;
            }

            while ((!asyncRequest.isDone || songInfo.downloadingProgress < 1f) && songInfo.songQueueState != SongQueueState.Error)
            {
                yield return null;

                time += Time.deltaTime;

                if (time >= 5f && asyncRequest.progress <= float.Epsilon)
                {
                    www.Abort();
                    timeout = true;
                    Plugin.log.Error("Connection timed out!");
                }

                songInfo.downloadingProgress = asyncRequest.progress;
                progressChangedCallback?.Invoke(asyncRequest.progress);
            }

            if (songInfo.songQueueState == SongQueueState.Error && (!asyncRequest.isDone || songInfo.downloadingProgress < 1f))
                www.Abort();

            if (www.isNetworkError || www.isHttpError || timeout || songInfo.songQueueState == SongQueueState.Error)
            {
                songInfo.songQueueState = SongQueueState.Error;
                Plugin.log.Error("Unable to download song! " + (www.isNetworkError ? $"Network error: {www.error}" : (www.isHttpError ? $"HTTP error: {www.error}" : "Unknown error")));
                downloadedCallback?.Invoke(false);
            }
            else
            {
                Plugin.log.Debug("Received response from BeatSaver.com...");
                string customSongsPath = "";

                byte[] data = www.downloadHandler.data;

                Stream zipStream = null;

                try
                {
                    customSongsPath = CustomLevelPathHelper.customLevelsDirectoryPath;
                    if (!Directory.Exists(customSongsPath))
                    {
                        Directory.CreateDirectory(customSongsPath);
                    }
                    zipStream = new MemoryStream(data);
                    Plugin.log.Debug("Downloaded zip!");
                }
                catch (Exception e)
                {
                    Plugin.log.Critical(e);
                    songInfo.songQueueState = SongQueueState.Error;
                    downloadedCallback?.Invoke(false);
                    yield break;
                }

                yield return new WaitWhile(() => _extractingZip); //because extracting several songs at once sometimes hangs the game

                Task extract = ExtractZipAsync(songInfo, zipStream, customSongsPath);
                yield return new WaitWhile(() => !extract.IsCompleted);
                songDownloaded?.Invoke(songInfo);
                downloadedCallback?.Invoke(true);
            }
        }

        private async Task ExtractZipAsync(Song songInfo, Stream zipStream, string customSongsPath)
        {
            try
            {
                Plugin.log.Debug("Extracting...");
                _extractingZip = true;
                ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                string basePath = songInfo.key + " (" + songInfo.songName + " - " + songInfo.levelAuthorName + ")";
                basePath = string.Join("", basePath.Split((Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray())));
                string path = customSongsPath + "/" + basePath;

                if (Directory.Exists(path))
                {
                    int pathNum = 1;
                    while (Directory.Exists(path + $" ({pathNum})")) ++pathNum;
                    path += $" ({pathNum})";
                }
                await Task.Run(() => archive.ExtractToDirectory(path)).ConfigureAwait(false);
                archive.Dispose();
                songInfo.path = path;
            }
            catch (Exception e)
            {
                Plugin.log.Critical($"Unable to extract ZIP! Exception: {e}");
                songInfo.songQueueState = SongQueueState.Error;
                _extractingZip = false;
                return;
            }
            zipStream.Close();

            if (string.IsNullOrEmpty(songInfo.path))
            {
                songInfo.path = customSongsPath;
            }

            _extractingZip = false;
            songInfo.songQueueState = SongQueueState.Downloaded;
            _alreadyDownloadedSongs.Add(songInfo);
            Plugin.log.Debug($"Extracted {songInfo.songName} {songInfo.songSubName}!");

        }

        public bool IsSongDownloaded(Song song)
        {
            if (SongCore.Loader.AreSongsLoaded)
                return _alreadyDownloadedSongs.Any(x => x.Compare(song));
            else
                return false;
        }

        public static CustomPreviewBeatmapLevel GetLevel(string levelId)
        {
            if(_beatmapLevelsModel == null)
            {
                _beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();
            }
            return _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels).FirstOrDefault(x => x.levelID == levelId) as CustomPreviewBeatmapLevel;
        }

        public static bool CreateMD5FromFile(string path, out string hash)
        {
            hash = "";
            if (!File.Exists(path)) return false;
            using (MD5 md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    byte[] hashBytes = md5.ComputeHash(stream);

                    StringBuilder sb = new StringBuilder();
                    foreach (byte hashByte in hashBytes)
                    {
                        sb.Append(hashByte.ToString("X2"));
                    }

                    hash = sb.ToString();
                    return true;
                }
            }
        }

        public void RequestSongByLevelID(string levelId, Action<Song> callback)
        {
            StartCoroutine(RequestSongByLevelIDCoroutine(levelId, callback));
        }

        public IEnumerator RequestSongByLevelIDCoroutine(string levelId, Action<Song> callback)
        {
            UnityWebRequest wwwId = UnityWebRequest.Get($"{Config.Instance.BeatSaverURL}/api/maps/by-hash/" + levelId.ToLower());
            wwwId.timeout = 10;

            yield return wwwId.SendWebRequest();


            if (wwwId.isNetworkError || wwwId.isHttpError)
            {
                Plugin.log.Error($"Unable to fetch song by hash! {wwwId.error}\nURL:" + $"{Config.Instance.BeatSaverURL}/api/maps/by-hash/" + levelId.ToLower());
            }
            else
            {
                JObject jNode = JObject.Parse(wwwId.downloadHandler.text);
                if (jNode.Children().Count() == 0)
                {
                    Plugin.log.Error($"Song {levelId} doesn't exist on BeatSaver!");
                    callback?.Invoke(null);
                    yield break;
                }

                Song _tempSong = Song.FromSearchNode((JObject)jNode);
                callback?.Invoke(_tempSong);
            }
        }

        public void RequestSongByKey(string key, Action<Song> callback)
        {
            StartCoroutine(RequestSongByKeyCoroutine(key, callback));
        }

        public IEnumerator RequestSongByKeyCoroutine(string key, Action<Song> callback)
        {
            UnityWebRequest wwwId = UnityWebRequest.Get($"{Config.Instance.BeatSaverURL}/api/maps/detail/" + key.ToLower());
            wwwId.timeout = 10;

            yield return wwwId.SendWebRequest();


            if (wwwId.isNetworkError || wwwId.isHttpError)
            {
                Plugin.log.Error(wwwId.error);
            }
            else
            {
                JObject node = JObject.Parse(wwwId.downloadHandler.text);

                Song _tempSong = new Song((JObject)node, false);
                callback?.Invoke(_tempSong);
            }
        }
    }
}
