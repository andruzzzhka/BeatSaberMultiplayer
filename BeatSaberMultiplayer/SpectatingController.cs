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
using Lidgren.Network;
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

        private Dictionary<ulong, List<PlayerInfo>> _playerInfos = new Dictionary<ulong, List<PlayerInfo>>();

        private OnlinePlayerController _spectatedPlayer;
        
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

                Client.Instance.MessageReceived -= PacketReceived;
                Client.Instance.MessageReceived += PacketReceived;
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
            if (!Config.Instance.SpectatorMode || Client.Instance.InRadioMode)
                yield break;

            Misc.Logger.Info("Waiting for controllers...");
            yield return new WaitWhile(delegate() { return !Resources.FindObjectsOfTypeAll<Saber>().Any(); });

            audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();
            _songAudioSource = audioTimeSync.GetPrivateField<AudioSource>("_audioSource");

            var saberB = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberB);
            _leftController = saberB.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            saberB.SetPrivateField("_vrController", _leftController);

            var saberA = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberA);
            _rightController = saberA.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            saberA.SetPrivateField("_vrController", _rightController);

            _playerController = Resources.FindObjectsOfTypeAll<PlayerController>().First();

            Misc.Logger.Info("Controllers found!");

            _scoreController = FindObjectOfType<ScoreController>();

            Misc.Logger.Info("Score controller found!");
        }

        private void PacketReceived(NetIncomingMessage msg)
        {
            if (Config.Instance.SpectatorMode && !Client.Instance.InRadioMode && _currentScene.name == "GameCore")
            {
                msg.Position = 0;
                CommandType commandType = (CommandType)msg.ReadByte();
                if (commandType == CommandType.UpdatePlayerInfo)
                {
                    float currentTime = msg.ReadFloat();
                    float totalTime = msg.ReadFloat();

                    int playersCount = msg.ReadInt32();
                    
                    for (int j = 0; j < playersCount; j++)
                    {
                        try
                        {
                            PlayerInfo player = new PlayerInfo(msg);
                            if (_playerInfos.ContainsKey(player.playerId))
                            {
                                if(_playerInfos[player.playerId].Count >= 3 * Client.Instance.Tickrate)
                                {
                                    _playerInfos[player.playerId].RemoveAt(0);
                                }
                                _playerInfos[player.playerId].Add(player);
                            }
                            else
                            {
                                _playerInfos.Add(player.playerId, new List<PlayerInfo>() { player});
                            }
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            Misc.Logger.Exception($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                        }
                    }

                    if(_spectatedPlayer == null)
                    {
                        _spectatedPlayer = new GameObject("PlayerController").AddComponent<OnlinePlayerController>();
                        _spectatedPlayer.noInterpolation = true;
                    }

                    if (_playerInfos.Count > 1 && _spectatedPlayer == null)
                    {
                        _spectatedPlayer.PlayerInfo = _playerInfos.First(x => !x.Key.Equals(Client.Instance.playerInfo)).Value.Last();
                        Misc.Logger.Info("Spectating " + _spectatedPlayer.PlayerInfo.playerName);
                    }
                    
                    if (_spectatedPlayer.PlayerInfo != null)
                    {
                        float minOffset = _playerInfos[_spectatedPlayer.PlayerInfo.playerId].Min(x => Math.Abs(x.playerProgress - audioTimeSync.songTime));
                        
                        int index = _playerInfos[_spectatedPlayer.PlayerInfo.playerId].FindIndex(x => Math.Abs(x.playerProgress - audioTimeSync.songTime) == minOffset);
                        PlayerInfo lerpTo;
                        float lerpProgress;
                        
                        if(_playerInfos[_spectatedPlayer.PlayerInfo.playerId][index].playerProgress - audioTimeSync.songTime > 0f)
                        {
                            _spectatedPlayer.PlayerInfo = _playerInfos[_spectatedPlayer.PlayerInfo.playerId][index];
                            lerpTo = _playerInfos[_spectatedPlayer.PlayerInfo.playerId][index - 1];

                            lerpProgress = Remap(audioTimeSync.songTime, lerpTo.playerProgress, _spectatedPlayer.PlayerInfo.playerProgress, 0f, 1f);
                        }
                        else
                        {
                            _spectatedPlayer.PlayerInfo = _playerInfos[_spectatedPlayer.PlayerInfo.playerId][index + 1];
                            lerpTo = _playerInfos[_spectatedPlayer.PlayerInfo.playerId][index];

                            lerpProgress = Remap(audioTimeSync.songTime, lerpTo.playerProgress, _spectatedPlayer.PlayerInfo.playerProgress, 0f, 1f);
                        }
                        
                        _spectatedPlayer.PlayerInfo.leftHandPos = Vector3.Lerp(_spectatedPlayer.PlayerInfo.leftHandPos, lerpTo.leftHandPos, 0);
                        _spectatedPlayer.PlayerInfo.rightHandPos = Vector3.Lerp(_spectatedPlayer.PlayerInfo.rightHandPos, lerpTo.rightHandPos, 0);

                        _spectatedPlayer.PlayerInfo.leftHandRot = Quaternion.Lerp(_spectatedPlayer.PlayerInfo.leftHandRot, lerpTo.leftHandRot, 0);
                        _spectatedPlayer.PlayerInfo.rightHandRot = Quaternion.Lerp(_spectatedPlayer.PlayerInfo.rightHandRot, lerpTo.rightHandRot, 0);
                                                    
                        if (_leftController != null && _rightController != null)
                        {
                            _leftController.owner = _spectatedPlayer;
                            _rightController.owner = _spectatedPlayer;
                        }

                        if(_scoreController != null)
                        {
                            _scoreController.SetPrivateField("_prevFrameScore", (int)_spectatedPlayer.PlayerInfo.playerScore);
                            _scoreController.SetPrivateField("_baseScore", (int)lerpTo.playerScore);
                            _scoreController.SetPrivateField("_combo", (int)lerpTo.playerComboBlocks);
                        }

                        if(_playerController != null)
                        {
                            _playerController.OverrideHeadPos(_spectatedPlayer.PlayerInfo.headPos);
                        }

                        if (_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress - audioTimeSync.songTime > 2.5f)
                        {
#if DEBUG
                            if(_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress > 2f)
                                Misc.Logger.Info($"Syncing song with a spectated player...\nOffset: {_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress - audioTimeSync.songTime}");
#endif
                            SetPositionInSong(_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress - 1f);
                            InGameOnlineController.Instance.PauseSong();
                            _paused = true;
                        }
                        else
                        if (_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress - audioTimeSync.songTime < 1.5f && (audioTimeSync.songLength - audioTimeSync.songTime) > 3f)
                        {
#if DEBUG
                            if (_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress > 2f)
                                Misc.Logger.Info($"Syncing song with a spectated player...\nOffset: {_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress - audioTimeSync.songTime}");
#endif
                            InGameOnlineController.Instance.PauseSong();
                            _paused = true;
                        }
                    }
                }
            }
        }

        public void DestroyAvatar()
        {
            if(_spectatedPlayer != null)
            {
                Destroy(_spectatedPlayer.gameObject);
            }
        }

        public void Update()
        {
            if (Config.Instance.SpectatorMode)
            {
                if (Input.GetKeyDown(KeyCode.KeypadMultiply))
                {
                    int index = _playerInfos.Keys.ToList().FindIndexInList(_spectatedPlayer.PlayerInfo.playerId);
                    if (index >= _playerInfos.Count - 1)
                    {
                        index = 0;
                    }

                    _spectatedPlayer.PlayerInfo = _playerInfos[_playerInfos.Keys.ElementAt(index)].Last();
                }

                if (Input.GetKeyDown(KeyCode.KeypadDivide))
                {
                    int index = _playerInfos.Keys.ToList().FindIndexInList(_spectatedPlayer.PlayerInfo.playerId);
                    if (index <= 0)
                    {
                        index = _playerInfos.Count - 1;
                    }

                    _spectatedPlayer.PlayerInfo = _playerInfos[_playerInfos.Keys.ElementAt(index)].Last();
                }

                if (_paused)
                {
                    if(_playerInfos[_spectatedPlayer.PlayerInfo.playerId].Last().playerProgress - audioTimeSync.songTime > 1.9f)
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
        }

        public float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
    }
}
