using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.OverriddenClasses;
using BS_Utils.Gameplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class OnlinePlayerController : PlayerController
    {
        public PlayerInfo PlayerInfo {

            get
            {
                return _info;
            }

            set
            {
                UpdatePrevPosRot(value);
                if (_info != null && value != null && !noInterpolation)
                {
                    _info.playerName = value.playerName;
                    _info.playerName = value.playerName;
                    _info.avatarHash = value.avatarHash;
                    _info.playerComboBlocks = value.playerComboBlocks;
                    _info.playerCutBlocks = value.playerCutBlocks;
                    _info.playerEnergy = value.playerEnergy;
                    _info.playerScore = value.playerScore;
                    _info.playerState = value.playerState;
                    _info.playerTotalBlocks = value.playerTotalBlocks;
                }
                else
                {
                    _info = value;
                }
            }
        }

        public AvatarController avatar;
        public AudioSource voipSource;

        public OnlineBeatmapCallbackController beatmapCallbackController;
        public OnlineBeatmapSpawnController beatmapSpawnController;
        public OnlineAudioTimeController audioTimeController;

        public float avatarOffset;
        public bool noInterpolation = false;
        public bool destroyed = false;
        
        private PlayerInfo _info;
        private AudioClip _voipClip;
        private float[] _voipBuffer;
        private int _lastVoipFragIndex;
        private int _silentFrames;

        private float lastSynchronizationTime = 0f;
        private float syncDelay = 0f;
        private float syncTime = 0f;
        private float lerpProgress = 0f;

        private PlayerInfo syncStartInfo;
        private PlayerInfo syncEndInfo;

        public void Start()
        {
#if DEBUG
            Misc.Logger.Info($"Player controller created!");
#endif
            voipSource = gameObject.AddComponent<AudioSource>();

            _voipClip = AudioClip.Create("VoIP Clip", 65535, 1, 16000, false);
            voipSource.clip = _voipClip;
            voipSource.spatialize = Config.Instance.SpatialAudio;

            if (_info != null)
            {
#if DEBUG
                Misc.Logger.Info($"Starting player controller for {_info.playerName}:{_info.playerId}...");
#endif
                syncStartInfo = _info;
                syncEndInfo = _info;
                
                if (SceneManager.GetActiveScene().name != "Menu" && Config.Instance.ShowOtherPlayersBlocks && !Client.Instance.playerInfo.Equals(PlayerInfo))
                {
                    SpawnBeatmapControllers();
                    SpawnSabers();
                }
            }
        }

        void SpawnBeatmapControllers()
        {
            Misc.Logger.Info("Creating beatmap controllers...");

            beatmapCallbackController = new GameObject("OnlineBeatmapCallbackController").AddComponent<OnlineBeatmapCallbackController>();
            Misc.Logger.Info("Created beatmap callback controller!");
            beatmapCallbackController.Init(this);
            Misc.Logger.Info("Initialized beatmap callback controller!");
            
            audioTimeController = new GameObject("OnlineAudioTimeController").AddComponent<OnlineAudioTimeController>();
            Misc.Logger.Info("Created audio time controller!");
            audioTimeController.Init(this);
            Misc.Logger.Info("Initialized audio time controller!");

            beatmapSpawnController = new GameObject("OnlineBeatmapSpawnController").AddComponent<OnlineBeatmapSpawnController>();
            Misc.Logger.Info("Created beatmap spawn controller!");
            beatmapSpawnController.Init(this, beatmapCallbackController, audioTimeController);
            Misc.Logger.Info("Initialized beatmap spawn controller!");
        }

        void SpawnSabers()
        {
            Misc.Logger.Info("Spawning left saber...");
            _leftSaber = Instantiate(Resources.FindObjectsOfTypeAll<Saber>().First(x => x.name == "LeftSaber"), transform, false);
            var leftController = _leftSaber.gameObject.AddComponent<OnlineVRController>();
            leftController.owner = this;

            Misc.Logger.Info("Spawning right saber...");
            _rightSaber = Instantiate(Resources.FindObjectsOfTypeAll<Saber>().First(x => x.name == "RightSaber"), transform, false);
            var rightController = _rightSaber.gameObject.AddComponent<OnlineVRController>();
            rightController.owner = this;

            Misc.Logger.Info("Sabers spawned!");
        }

        public override void Update()
        {
            if (avatar != null)
            {
                avatar.SetPlayerInfo(_info, avatarOffset, Client.Instance.playerInfo.Equals(_info));
            }

            if(voipSource != null && !voipSource.isPlaying)
            {
                _silentFrames++;
            }
            else
            {
                _silentFrames = 0;
            }

        }

        public void FixedUpdate()
        {
            if (syncStartInfo != null && syncEndInfo != null && _info != null && !noInterpolation)
            {
                syncTime += Time.fixedDeltaTime;

                lerpProgress = syncTime / syncDelay;

                _info.headPos = Vector3.Lerp(syncStartInfo.headPos, syncEndInfo.headPos, lerpProgress);
                _info.leftHandPos = Vector3.Lerp(syncStartInfo.leftHandPos, syncEndInfo.leftHandPos, lerpProgress);
                _info.rightHandPos = Vector3.Lerp(syncStartInfo.rightHandPos, syncEndInfo.rightHandPos, lerpProgress);

                _overrideHeadPos = true;
                _overriddenHeadPos = _info.headPos;
                _headPos = _info.headPos + Vector3.right * avatarOffset;
                transform.position = _headPos;

                _info.headRot = Quaternion.Lerp(syncStartInfo.headRot, syncEndInfo.headRot, lerpProgress);
                _info.leftHandRot = Quaternion.Lerp(syncStartInfo.leftHandRot, syncEndInfo.leftHandRot, lerpProgress);
                _info.rightHandRot = Quaternion.Lerp(syncStartInfo.rightHandRot, syncEndInfo.rightHandRot, lerpProgress);
                
                _info.playerProgress = Mathf.Lerp(syncStartInfo.playerProgress, syncEndInfo.playerProgress, lerpProgress);
            }
        }

        public void OnDestroy()
        {
#if DEBUG
            if(_info == null)
                Misc.Logger.Info("Destroying player controller!");
            else
                Misc.Logger.Info($"Destroying player controller! Name: {_info.playerName}, ID: {_info.playerId}");
#endif
            destroyed = true;
            
            if (avatar != null)
            {
                Destroy(avatar.gameObject);
            }

            if (beatmapCallbackController != null && beatmapSpawnController != null)
            {
                Destroy(beatmapCallbackController.gameObject);
                Destroy(beatmapSpawnController.gameObject);
            }
        }

        public void UpdatePrevPosRot(PlayerInfo newPlayerInfo)
        {
            if (newPlayerInfo == null || _info == null || noInterpolation)
                return;

            syncTime = 0;
            syncDelay = Time.time - lastSynchronizationTime;

            if(syncDelay > 0.5f)
            {
                syncDelay = 0.5f;
            }

            lastSynchronizationTime = Time.time;
            
            syncStartInfo = _info;
            if (syncStartInfo.IsRotNaN())
            {
                syncStartInfo.headRot = Quaternion.identity;
                syncStartInfo.leftHandRot = Quaternion.identity;
                syncStartInfo.rightHandRot = Quaternion.identity;
                Misc.Logger.Warning("Start rotation is NaN!");
            }

            syncEndInfo = newPlayerInfo;
            if (syncEndInfo.IsRotNaN())
            {
                syncEndInfo.headRot = Quaternion.identity;
                syncEndInfo.leftHandRot = Quaternion.identity;
                syncEndInfo.rightHandRot = Quaternion.identity;
                Misc.Logger.Warning("Target rotation is NaN!");
            }

            _info.playerProgress = syncEndInfo.playerProgress;
        }

        public void SetAvatarState(bool enabled)
        {
            if(enabled && (object)avatar == null)
            {
                avatar = new GameObject("AvatarController").AddComponent<AvatarController>();
                avatar.SetPlayerInfo(_info, avatarOffset, Client.Instance.playerInfo.Equals(_info));
            }
            else if(!enabled && avatar != null)
            {
                Destroy(avatar.gameObject);
                avatar = null;
            }
        }

        public void PlayVoIPFragment(float[] data, int fragIndex)
        {
            if(voipSource != null)
            {
                if (_voipBuffer == null || (_lastVoipFragIndex + 1) != fragIndex || _silentFrames > 20)
                {
                    float[] tempBuffer = new float[data.Length + 1024];

                    Buffer.BlockCopy(data, 0, tempBuffer, 1023 * sizeof(float), data.Length * sizeof(float));

                    _voipBuffer = tempBuffer;
                    _lastVoipFragIndex = fragIndex;
                    _voipClip.SetData(_voipBuffer, 0);
                    voipSource.Play();
                    _silentFrames = 0;

                }
                else
                {
                    int currentPos = voipSource.timeSamples;

                    if (currentPos >= _voipBuffer.Length)
                        currentPos = _voipBuffer.Length - 1;
                    if (currentPos < 1)
                        currentPos = 1;

                    float[] tempBuffer = new float[_voipBuffer.Length - currentPos - 1 + data.Length];

                    Buffer.BlockCopy(_voipBuffer, (currentPos - 1) * sizeof(float), tempBuffer, 0,  (_voipBuffer.Length - currentPos - 1) * sizeof(float));
                    Buffer.BlockCopy(data, 0, tempBuffer, (_voipBuffer.Length - currentPos - 1) * sizeof(float), data.Length * sizeof(float));

                    _voipBuffer = tempBuffer;
                    _lastVoipFragIndex = fragIndex;
                    _voipClip.SetData(_voipBuffer, 0);
                    voipSource.Play();
                    _silentFrames = 0;
                }
            }
        }

        public void SetVoIPVolume(float newVolume)
        {
            if(voipSource != null)
            {
                voipSource.volume = newVolume;
            }
        }

        public void SetSpatialAudioState(bool spatialAudio)
        {
            if (voipSource != null)
            {
                voipSource.spatialize = spatialAudio;
            }
        }

        public bool IsTalking()
        {
            return _silentFrames < 20;
        }
    }
}
