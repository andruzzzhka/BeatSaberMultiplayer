using System;
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
        private List<PlayerInfo> _playerInfos = new List<PlayerInfo>();

        private PlayerInfo _spectatedPlayer;
        private AvatarController _spectatedPlayerAvatar;

        public AudioTimeSyncController audioTimeSync;
        private AudioSource _songAudioSource;

        private OnlineVRController _leftController;
        private OnlineVRController _rightController;
        
        private bool _paused = false;

        private TextMeshPro _syncText;

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

                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                Client.ClientCreated += ClientCreated;
                _currentScene = SceneManager.GetActiveScene();
            }
        }

        private void ClientCreated()
        {
            Client.instance.PacketReceived += PacketReceived;
        }

        private void PacketReceived(BasePacket packet)
        {
            if (Config.Instance.SpectatorMode && _currentScene.name == "StandardLevel")
            {
                if (packet.commandType == CommandType.UpdatePlayerInfo)
                {
                    int playersCount = BitConverter.ToInt32(packet.additionalData, 8);

                    Stream byteStream = new MemoryStream(packet.additionalData, 12, packet.additionalData.Length - 12);

                    _playerInfos.Clear();
                    for (int j = 0; j < playersCount; j++)
                    {
                        byte[] sizeBytes = new byte[4];
                        byteStream.Read(sizeBytes, 0, 4);

                        int playerInfoSize = BitConverter.ToInt32(sizeBytes, 0);

                        byte[] playerInfoBytes = new byte[playerInfoSize];
                        byteStream.Read(playerInfoBytes, 0, playerInfoSize);

                        try
                        {
                            _playerInfos.Add(new PlayerInfo(playerInfoBytes));
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
                        _spectatedPlayer = _playerInfos.First(x => !x.Equals(Client.instance.playerInfo));
                        Log.Info("Spectating " + _spectatedPlayer.playerName);
                    }

                    if(_spectatedPlayer != null)
                    {
                        _spectatedPlayer = _playerInfos.FirstOrDefault(x => x.Equals(_spectatedPlayer));
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

                            if (_spectatedPlayer.playerProgress - audioTimeSync.songTime > 0.05f)
                            {
#if DEBUG
                                Log.Info($"Syncing song with a spectated player...\nOffset: {_spectatedPlayer.playerProgress - audioTimeSync.songTime}\nSpectated player: {_spectatedPlayer.playerProgress}\nActual song time: {audioTimeSync.songTime}");
#endif
                                SetPositionInSong(_spectatedPlayer.playerProgress + 0.2f);
                                InGameOnlineController.Instance.PauseSong();
                                _paused = true;
                            }
                            else if(_spectatedPlayer.playerProgress - audioTimeSync.songTime < -0.05f && (audioTimeSync.songLength - audioTimeSync.songTime) > 3f)
                            {
#if DEBUG
                                Log.Info($"Syncing song with a spectated player...\nOffset: {_spectatedPlayer.playerProgress - audioTimeSync.songTime}\nSpectated player: {_spectatedPlayer.playerProgress}\nActual song time: {audioTimeSync.songTime}");
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

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            _currentScene = next;
            if (Config.Instance.SpectatorMode)
            {
                if (_currentScene.name == "StandardLevel")
                {
                    TogglePlayerAvatar(!(Client.instance != null && Client.instance.Connected));
                    DestroyAvatar();
                    audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();
                    _songAudioSource = audioTimeSync.GetPrivateField<AudioSource>("_audioSource");
                    _leftController = FindObjectsOfType<VRController>().First(x => x.node == XRNode.LeftHand).gameObject.AddComponent<OnlineVRController>();
                    _leftController.forcePlayerInfo = true;
                    _rightController = FindObjectsOfType<VRController>().First(x => x.node == XRNode.RightHand).gameObject.AddComponent<OnlineVRController>();
                    _rightController.forcePlayerInfo = true;
                }
                else if (_currentScene.name == "Menu")
                {
                    TogglePlayerAvatar(true);
                    DestroyAvatar();
                }
            }
        }

        public void Update()
        {
            if (Config.Instance.SpectatorMode)
            {
                if (Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    int index = _playerInfos.FindIndexInList(_spectatedPlayer);
                    if (index >= _playerInfos.Count - 1)
                    {
                        index = 0;
                    }

                    _spectatedPlayer = _playerInfos[index];
                }

                if (Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    int index = _playerInfos.FindIndexInList(_spectatedPlayer);
                    if (index <= 0)
                    {
                        index = _playerInfos.Count - 1;
                    }

                    _spectatedPlayer = _playerInfos[index];
                }

                if (_paused)
                {
                    if(_spectatedPlayer.playerProgress - audioTimeSync.songTime < 0.025f && _spectatedPlayer.playerProgress - audioTimeSync.songTime > -0.025f)
                    {
                        InGameOnlineController.Instance.ResumeSong();
                        _paused = false;
                    }
                }
            }
        }

        private void SetPositionInSong(float time)
        {
            _songAudioSource.timeSamples = Mathf.RoundToInt(Mathf.Lerp(0, _songAudioSource.clip.samples, (time / audioTimeSync.songLength)));
            _songAudioSource.time = _songAudioSource.time - Mathf.Min(1f, _songAudioSource.time);
            SongSeekBeatmapHandler.OnSongTimeChanged(_songAudioSource.time, Mathf.Min(1f, _songAudioSource.time));

            Client.instance.RemovePacketsFromQueue(CommandType.UpdatePlayerInfo);
        }
    }
}
