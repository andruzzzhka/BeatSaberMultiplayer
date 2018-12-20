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
    public class SpectatingController : MonoBehaviour
    {
        public static SpectatingController Instance;

        private Scene _currentScene;

        private Dictionary<PlayerInfo, List<PlayerInfo>> _playerInfos = new Dictionary<PlayerInfo, List<PlayerInfo>>();

        private PlayerInfo _spectatedPlayer;
        private AvatarController _spectatedPlayerAvatar;
        
        private ScoreController _scoreController;
        public AudioTimeSyncController audioTimeSync;
        private AudioSource _songAudioSource;

        private OnlineVRController _leftController;
        private OnlineVRController _rightController;

        private PlayerController _playerController;
        
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
                
                Client.ClientCreated += ClientCreated;
                _currentScene = SceneManager.GetActiveScene();
            }
        }

        public void ActiveSceneChanged(Scene from, Scene to)
        {
            try
            {
                if (to.name == "GameCore" || (from.name == "EmptyTransition" && to.name == "Menu"))
                {
                    _currentScene = to;
                    if (to.name == "GameCore")
                    {
                        DestroyAvatar();
                        StartCoroutine(WaitForControllers());
                    }
                    else if (to.name == "Menu")
                    {
                        DestroyAvatar();
                    }
                }
            }catch(Exception e)
            {
                Misc.Logger.Warning($"(Spectator) Exception on {to.name} scene activation! Exception: {e}");
            }
        }

        IEnumerator WaitForControllers()
        {
            Misc.Logger.Info("Waiting for controllers...");
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

            _playerController = Resources.FindObjectsOfTypeAll<PlayerController>().First();

            Misc.Logger.Info("Controllers found!");

            _scoreController = FindObjectOfType<ScoreController>();

            Misc.Logger.Info("Score controller found!");
        }

        private void ClientCreated()
        {
            Client.instance.PacketReceived -= PacketReceived;
            Client.instance.PacketReceived += PacketReceived;
        }

        private void PacketReceived(BasePacket packet)
        {
            if (Config.Instance.SpectatorMode && _currentScene.name == "GameCore")
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
                            Misc.Logger.Exception($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                        }
                    }


                    if (_playerInfos.Count > 1 && _spectatedPlayer == null)
                    {
                        _spectatedPlayer = _playerInfos.First(x => !x.Key.Equals(Client.instance.playerInfo)).Value.Last();
                        Misc.Logger.Info("Spectating " + _spectatedPlayer.playerName);
                    }
                    
                    if (_spectatedPlayer != null)
                    {
                        float minOffset = _playerInfos[_spectatedPlayer].Min(x => Math.Abs(x.playerProgress - audioTimeSync.songTime));
                        
                        int index = _playerInfos[_spectatedPlayer].FindIndex(x => Math.Abs(x.playerProgress - audioTimeSync.songTime) == minOffset);
                        PlayerInfo lerpTo;
                        float lerpProgress;
                        
                        if(_playerInfos[_spectatedPlayer][index].playerProgress - audioTimeSync.songTime > 0f)
                        {
                            _spectatedPlayer = _playerInfos[_spectatedPlayer][index];
                            lerpTo = _playerInfos[_spectatedPlayer][index - 1];

                            lerpProgress = Remap(audioTimeSync.songTime, lerpTo.playerProgress, _spectatedPlayer.playerProgress, 0f, 1f);
                        }
                        else
                        {
                            _spectatedPlayer = _playerInfos[_spectatedPlayer][index + 1];
                            lerpTo = _playerInfos[_spectatedPlayer][index];

                            lerpProgress = Remap(audioTimeSync.songTime, lerpTo.playerProgress, _spectatedPlayer.playerProgress, 0f, 1f);
                        }
                        
                        if (_spectatedPlayer != null)
                        {
                            _spectatedPlayer.leftHandPos = Vector3.Lerp(_spectatedPlayer.leftHandPos, lerpTo.leftHandPos, 0);
                            _spectatedPlayer.rightHandPos = Vector3.Lerp(_spectatedPlayer.rightHandPos, lerpTo.rightHandPos, 0);

                            _spectatedPlayer.leftHandRot = Quaternion.Lerp(_spectatedPlayer.leftHandRot, lerpTo.leftHandRot, 0);
                            _spectatedPlayer.rightHandRot = Quaternion.Lerp(_spectatedPlayer.rightHandRot, lerpTo.rightHandRot, 0);

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

                            if(_scoreController != null)
                            {
                                _scoreController.SetPrivateField("_prevFrameScore", (int)_spectatedPlayer.playerScore);
                                _scoreController.SetPrivateField("_baseScore", (int)lerpTo.playerScore);
                                _scoreController.SetPrivateField("_combo", (int)lerpTo.playerComboBlocks);
                            }

                            if(_playerController != null)
                            {
                                _playerController.OverrideHeadPos(_spectatedPlayer.headPos);
                            }

                            if (_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime > 2.5f)
                            {
#if DEBUG
                                if(_playerInfos[_spectatedPlayer].Last().playerProgress > 2f)
                                    Misc.Logger.Info($"Syncing song with a spectated player...\nOffset: {_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime}");
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
                                    Misc.Logger.Info($"Syncing song with a spectated player...\nOffset: {_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime}");
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

                if (_paused)
                {
                    if(_playerInfos[_spectatedPlayer].Last().playerProgress - audioTimeSync.songTime > 1.9f)
                    {
                        Misc.Logger.Info("Resuming song...");
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

        public float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
    }
}
