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
            if (_hashes.TryGetValue(GetRelativePath(info), out var data))
            {
                FileInfo file = new FileInfo(info.FullPath);

                if (file.LastWriteTimeUtc != data.lastWriteTime)
                {
                    Plugin.log.Info($"Calculating hash for avatar \"{info.Name}\"...");

                    var newHash = await CalculateHash(info.FullPath);

                    data.lastWriteTime = file.LastWriteTimeUtc;
                    data.hash = newHash;
                    _hashes[GetRelativePath(info)] = data;

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
                FileInfo file = new FileInfo(info.FullPath);

                Plugin.log.Info($"Calculating hash for avatar \"{info.Name}\"...");

                var newHash = await CalculateHash(info.FullPath);

                data.lastWriteTime = file.LastWriteTimeUtc;
                data.hash = newHash;
                _hashes[GetRelativePath(info)] = data;

                if (save)
                    Save();

                return newHash;
            }
        }

        public static async Task<string> RecalculateHashForAvatar(CustomAvatar.CustomAvatar info)
        {
            FileInfo file = new FileInfo(info.FullPath);

            Plugin.log.Info($"Calculating hash for avatar \"{info.Name}\"...");

            var newHash = await CalculateHash(info.FullPath);
            HashData data = new HashData() { lastWriteTime = file.LastWriteTimeUtc, hash = newHash };

            if (_hashes.ContainsKey(GetRelativePath(info)))
                _hashes[GetRelativePath(info)] = data;
            else
                _hashes.Add(GetRelativePath(info), data);

            Save();

            return newHash;
        }

        private static Task<string> CalculateHash(string path)
        {
            return Task.Run(() => {
                return BitConverter.ToString(MD5.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            });
        }

        public static string GetRelativePath(CustomAvatar.CustomAvatar info)
        {
            var toFile = info.FullPath;

            Uri pathUri = new Uri(toFile);

            var avatarsFolder = Path.Combine(Application.dataPath, "..", "CustomAvatars");

            if (!avatarsFolder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                avatarsFolder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(avatarsFolder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
