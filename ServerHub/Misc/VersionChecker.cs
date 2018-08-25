using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace ServerHub.Misc
{
    public static class VersionChecker
    {

        static string releasesURL = "https://api.github.com/repos/andruzzzhka/BeatSaberMultiplayer/releases";


        public static void CheckForUpdates()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("user-agent",
                        $"BeatSaberMultiplayerServer-{Assembly.GetEntryAssembly().GetName().Version}");
                    client.DownloadStringCompleted += Client_DownloadStringCompleted;
                    client.DownloadStringAsync(new Uri(releasesURL));
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Warning($"Can't check for updates! Exception: {e}");
            }
        }

        private static void Client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            string jsonString = e.Result;

            GithubRelease[] releases = JsonConvert.DeserializeObject<GithubRelease[]>(jsonString);

            GithubRelease latestRelease = releases[0];

            List<string> assets = new List<string>();

            assets.AddRange(latestRelease.assets.Select(x => x.name));

            bool newTag = (!latestRelease.tag_name.StartsWith(Assembly.GetEntryAssembly().GetName().Version.ToString()));
            bool platformUpdated = newTag && (assets.Any(x => x.Contains(Assembly.GetEntryAssembly().GetName().Name)));

            if (platformUpdated)
            {
                Logger.Instance.Warning($"New version of {Assembly.GetEntryAssembly().GetName().Name} is available!");
                Logger.Instance.Warning($"Current version: {Assembly.GetEntryAssembly().GetName().Version.ToString()}");
                Logger.Instance.Warning($"Available version: {latestRelease.tag_name}");
            }
            else
            {
                Logger.Instance.Log($"You have the latest version of {Assembly.GetEntryAssembly().GetName().Name}");
            }
        }
    }

    public class Asset
    {
        public string url { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string name { get; set; }
        public object label { get; set; }
        public string content_type { get; set; }
        public string state { get; set; }
        public int size { get; set; }
        public int download_count { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string browser_download_url { get; set; }
    }

    public class GithubRelease
    {
        public string url { get; set; }
        public string assets_url { get; set; }
        public string upload_url { get; set; }
        public string html_url { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string tag_name { get; set; }
        public string target_commitish { get; set; }
        public string name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public DateTime created_at { get; set; }
        public DateTime published_at { get; set; }
        public List<Asset> assets { get; set; }
        public string tarball_url { get; set; }
        public string zipball_url { get; set; }
        public string body { get; set; }
    }
}
