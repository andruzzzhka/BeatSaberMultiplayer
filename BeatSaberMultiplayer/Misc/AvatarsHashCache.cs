using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.Misc
{
    public struct HashData
    {
        public DateTime lastWriteTime;
        public string hash;
    }

    public static class AvatarsHashCache
    {
        public static string hashesFilePath { get; private set; } = Path.Combine(Application.persistentDataPath, "AvatarHashData.dat");

        private static Dictionary<string, HashData> _hashes = new Dictionary<string, HashData>();

        public static void Save()
        {
            if (File.Exists(hashesFilePath))
            {
                if (File.Exists(hashesFilePath + ".bak"))
                    File.Delete(hashesFilePath + ".bak");

                File.Move(hashesFilePath, hashesFilePath + ".bak");
            }

            File.WriteAllText(hashesFilePath, JsonConvert.SerializeObject(_hashes, Formatting.Indented));
        }

        public static void Load()
        {
            if (File.Exists(hashesFilePath))
            {
                _hashes = JsonConvert.DeserializeObject<Dictionary<string, HashData>>(File.ReadAllText(hashesFilePath));
            }
        }

        public static async Task<string> GetHashForAvatar(CustomAvatar.CustomAvatar info, bool save = true)
        {
            if (_hashes.TryGetValue(info.fullPath, out var data))
            {
                FileInfo file = new FileInfo(Path.Combine(Path.GetFullPath("CustomAvatars"), info.fullPath));

                if (file.LastWriteTimeUtc != data.lastWriteTime)
                {
                    Plugin.log.Info($"Calculating hash for avatar \"{info.descriptor.name}\"...");

                    var newHash = await CalculateHash(Path.Combine(Path.GetFullPath("CustomAvatars"), info.fullPath));

                    data.lastWriteTime = file.LastWriteTimeUtc;
                    data.hash = newHash;
                    _hashes[info.fullPath] = data;

                    if (save)
                        Save();

                    return newHash;
                }
                else
                {
                    return data.hash;
                }
            }
            else
            {
                FileInfo file = new FileInfo(Path.Combine(Path.GetFullPath("CustomAvatars"), info.fullPath));

                Plugin.log.Info($"Calculating hash for avatar \"{info.descriptor.name}\"...");

                var newHash = await CalculateHash(Path.Combine(Path.GetFullPath("CustomAvatars"), info.fullPath));

                data.lastWriteTime = file.LastWriteTimeUtc;
                data.hash = newHash;
                _hashes[info.fullPath] = data;

                if (save)
                    Save();

                return newHash;
            }
        }

        public static async Task<string> RecalculateHashForAvatar(CustomAvatar.CustomAvatar info)
        {
            FileInfo file = new FileInfo(Path.Combine(Path.GetFullPath("CustomAvatars"), info.fullPath));

            Plugin.log.Info($"Calculating hash for avatar \"{info.descriptor.name}\"...");

            var newHash = await CalculateHash(Path.Combine(Path.GetFullPath("CustomAvatars"), info.fullPath));
            HashData data = new HashData() { lastWriteTime = file.LastWriteTimeUtc, hash = newHash };

            if (_hashes.ContainsKey(info.fullPath))
                _hashes[info.fullPath] = data;
            else
                _hashes.Add(info.fullPath, data);

            Save();

            return newHash;
        }

        private static Task<string> CalculateHash(string path)
        {
            return Task.Run(() => {
                return BitConverter.ToString(MD5.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            });
        }
    }
}
