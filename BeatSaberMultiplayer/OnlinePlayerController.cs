using BeatSaberMultiplayer.Data;
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

        public OnlineBeatmapCallbackController beatmapCallbackController;
        public OnlineBeatmapSpawnController beatmapSpawnController;
        public OnlineAudioTimeController audioTimeController;

        public float avatarOffset;
        public bool noInterpolation = false;
        public bool destroyed = false;
        
        private PlayerInfo _info;

        private float lastSynchronizationTime = 0f;
        private float syncDelay = 0f;
        private float syncTime = 0f;
        private float lerpProgress = 0f;

        private PlayerInfo syncStartInfo;
        private PlayerInfo syncEndInfo;

        public void Start()
        {
            if (_info != null)
            {
                avatar = new GameObject("AvatarController").AddComponent<AvatarController>();
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
            if(_info != null)
                _info.playerProgress += Time.deltaTime;

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

            }
        }

        public void OnDestroy()
        {
#if DEBUG
            Misc.Logger.Info("Destroying player controller");
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
            lastSynchronizationTime = Time.time;

            syncStartInfo = _info;
            syncEndInfo = newPlayerInfo;

            _info.playerProgress = syncEndInfo.playerProgress;
        }
    }
}
