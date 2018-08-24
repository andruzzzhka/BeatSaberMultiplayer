using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using CustomAvatar;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace BeatSaberMultiplayer
{
    class SpectatingController : MonoBehaviour
    {
        public static SpectatingController Instance;

        private Scene _currentScene;

        private Dictionary<PlayerInfo, List<PlayerInfo>> _playerInfos = new Dictionary<PlayerInfo, List<PlayerInfo>>();

        private PlayerInfo _spectatedPlayer;
        private AvatarController _spectatedPlayerAvatar;

        public AudioTimeSyncController audioTimeSync;
        private AudioSource _songAudioSource;

        private OnlineVRController _leftController;
        private OnlineVRController _rightController;

        private float _offset = 0f;
        private bool _paused = false;

        public static void OnLoad()
        {
            if (Instance != null)
                return;
            new GameObject("SpectatingController").AddComponent<SpectatingController>();
        }

        public void Awake()
        {
            if (Instance != this)
            {
                Instance = this;
                DontDestroyOnLoad(this);

                SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
                Client.ClientCreated += ClientCreated;
                _currentScene = SceneManager.GetActiveScene();
            }
        }

        private void SceneManager_activeSceneChanged(Scene prev, Scene next)
        {
            if (next.name.EndsWith("Environment"))
            {
                _currentScene = next;
                TogglePlayerAvatar(!(Client.instance != null && Client.instance.Connected));
                DestroyAvatar();
                StartCoroutine(WaitForControllers());
            }
            else if(next.name == "Menu")
            {
                _currentScene = next;
                TogglePlayerAvatar(true);
                DestroyAvatar();
            }
        }

        IEnumerator WaitForControllers()
        {
            Log.Info("Waiting for controllers...");
            yield return new WaitWhile(delegate() { return !Resources.FindObjectsOfTypeAll<Saber>().Any(); });

            audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();
            _songAudioSource = audioTimeSync.GetPrivateField<AudioSource>("_audioSource");

            var saberB = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberB);
            _leftController = saberB.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            _leftController.forcePlayerInfo = true;
            saberB.SetPrivateField("_vrController", _leftController);

            var saberA = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberA);
            _rightController = saberA.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            _rightController.forcePlayerInfo = true;
            saberA.SetPrivateField("_vrController", _rightController);

            Log.Info("Controllers found!");
        }

        private void ClientCreated()
        {
            Client.instance.PacketReceived += PacketReceived;
        }

        private void PacketReceived(BasePacket packet)
        {
            if (Config.Instance.SpectatorMode && _currentScene.name.EndsWith("Environment"))
            {
                if (packet.commandType == CommandType.UpdatePlayerInfo)
                {
                    int playersCount = BitConverter.ToInt32(packet.additionalData, 8);

                    Stream byteStream = new MemoryStream(packet.additionalData, 12, packet.additionalData.Length - 12);
                    
                    for (int j = 0; j < playersCount; j++)
                    {
                        byte[] sizeBytes = new byte[4];
                        byteStream.Read(sizeBytes, 0, 4);

                        int playerInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                        byte[] playerInfoBytes = new byte[playerInfoSize];
                        byteStream.Read(playerInfoBytes, 0, playerInfoSize);

                        try
                        {
                            PlayerInfo player = new PlayerInfo(playerInfoBytes);
                            if (_playerInfos.ContainsKey(player))
                            {
                                if(_playerInfos[player].Count >= 3 * Client.instance.Tickrate)
                                {
                                    _playerInfos[player].RemoveAt(0);
                                }
                                _playerInfos[player].Add(player);
                            }
                            else
                            {
                                _playerInfos.Add(player, new List<PlayerInfo>() { player});
                            }
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            Log.Exception($"Can't parse PlayerInfo! Excpetion: {e}");
#endif
                        }
                    }


                    if (_playerInfos.Count > 1 && _spectatedPlayer == null)
                    {
                        _spectatedPlayer = _playerInfos.First(x => !x.Key.Equals(Client.instance.playerInfo)).Value.Last();
                        Log.Info("Spectating " + _spectatedPlayer.playerName);
                    }
                    
                    if (_spectatedPlayer != null)
                    {
                        float minOffset = _playerInfos[_spectatedPlayer].Min(x => Math.Abs(x.playerProgress + _offset - audioTimeSync.songTime));
                        _spectatedPlayer = _playerInfos[_spectatedPlayer].FirstOrDefault(x => Math.Abs(x.playerProgress + _offset - audioTimeSync.songTime) == minOffset);
                        
                        if (_spectatedPlayer != null)
                        {
                            if (_spectatedPlayerAvatar == null)
                            {
                                _spectatedPlayerAvatar = new GameObject("Avatar").AddComponent<AvatarController>();
                                _spectatedPlayerAvatar.forcePlayerInfo = true;
                            }

                            _spectatedPlayerAvatar.SetPlayerInfo(_spectatedPlayer, 0f, false);
                            
                            if (_leftController != null && _rightController != null)
                            {
                                _leftController.SetPlayerInfo(_spectatedPlayer);
                                _rightController.SetPlayerInfo(_spectatedPlayer);
                            }

                            if (_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime > 2.5f)
                            {
#if DEBUG
                                if(_playerInfos[_spectatedPlayer].Last().playerProgress > 2f)
                                    Log.Info($"Syncing song with a spectated player...\nOffset: {_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime}");
#endif
                                SetPositionInSong(_playerInfos[_spectatedPlayer].Last().playerProgress - 1f);
                                InGameOnlineController.Instance.PauseSong();
                                _paused = true;
                            }
                            else
                            if (_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime < 1.5f && (audioTimeSync.songLength - audioTimeSync.songTime) > 3f)
                            {
#if DEBUG
                                if (_playerInfos[_spectatedPlayer].Last().playerProgress > 2f)
                                    Log.Info($"Syncing song with a spectated player...\nOffset: {_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime}");
#endif
                                InGameOnlineController.Instance.PauseSong();
                                _paused = true;
                            }
                        }
                    }
                }
            }
        }

        public void DestroyAvatar()
        {
            if(_spectatedPlayerAvatar != null && _spectatedPlayerAvatar.gameObject != null)
            {
                Destroy(_spectatedPlayerAvatar.gameObject);
            }
        }

        public void TogglePlayerAvatar(bool enabled)
        {
            SetRendererInChilds(CustomAvatar.Plugin.Instance.PlayerAvatarManager.GetPrivateField<SpawnedAvatar>("_currentSpawnedPlayerAvatar").GameObject.transform, enabled);
        }

        private void SetRendererInChilds(Transform origin, bool enabled)
        {
            Renderer[] rends = origin.gameObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in rends)
            {
                rend.enabled = enabled;
            }
        }

        public void Update()
        {
            if (Config.Instance.SpectatorMode)
            {
                if (Input.GetKeyDown(KeyCode.KeypadMultiply))
                {
                    int index = _playerInfos.Keys.ToList().FindIndexInList(_spectatedPlayer);
                    if (index >= _playerInfos.Count - 1)
                    {
                        index = 0;
                    }

                    _spectatedPlayer = _playerInfos.Keys.ElementAt(index);
                }

                if (Input.GetKeyDown(KeyCode.KeypadDivide))
                {
                    int index = _playerInfos.Keys.ToList().FindIndexInList(_spectatedPlayer);
                    if (index <= 0)
                    {
                        index = _playerInfos.Count - 1;
                    }

                    _spectatedPlayer = _playerInfos.Keys.ElementAt(index);
                }

                if (Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    _offset += 0.025f;
                    Log.Info("New offset: " + _offset);
                }

                if (Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    _offset -= 0.025f;
                    Log.Info("New offset: "+_offset);
                }

                if (_paused)
                {
                    if(_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime > 1.9f)
                    {
                        Log.Info("Resuming song...");
                        InGameOnlineController.Instance.ResumeSong();
                        _paused = false;
                    }
                }
            }
        }

        private void SetPositionInSong(float time)
        {
            _songAudioSource.timeSamples = Mathf.RoundToInt(Mathf.Lerp(0, _songAudioSource.clip.samples, (time / audioTimeSync.songLength)));
            _songAudioSource.time = _songAudioSource.time - Mathf.Min(0f, _songAudioSource.time);
            SongSeekBeatmapHandler.OnSongTimeChanged(_songAudioSource.time, Mathf.Min(0f, _songAudioSource.time));

            Client.instance.RemovePacketsFromQueue(CommandType.UpdatePlayerInfo);
        }
    }
}
