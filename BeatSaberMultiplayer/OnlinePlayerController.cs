using BeatSaberMultiplayer.Data;
using BS_Utils.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    class OnlinePlayerController : MonoBehaviour
    {
        public PlayerInfo PlayerInfo {

            get
            {
                return _info;
            }

            set
            {
                UpdatePrevPosRot(value);
                if (_info != null)
                {
                    _info.playerName = value.playerName;
                    _info.playerName = value.playerName;
                    _info.avatarHash = value.avatarHash;
                    _info.playerComboBlocks = value.playerComboBlocks;
                    _info.playerCutBlocks = value.playerCutBlocks;
                    _info.playerEnergy = value.playerEnergy;
                    _info.playerProgress = value.playerProgress;
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

        public float avatarOffset;
        public bool noInterpolation = false;

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
            }
        }

        public void Update()
        {

            if (avatar != null)
            {
                avatar.SetPlayerInfo(_info, avatarOffset, Client.Instance.playerInfo.Equals(_info));
            }
        }

        public void FixedUpdate()
        {
            if (syncStartInfo != null && syncEndInfo != null && _info != null)
            {
                syncTime += Time.fixedDeltaTime;

                lerpProgress = Mathf.Clamp(syncTime / syncDelay, 0f, 2f);

                _info.headPos = Vector3.LerpUnclamped(syncStartInfo.headPos, syncEndInfo.headPos, lerpProgress);
                _info.leftHandPos = Vector3.LerpUnclamped(syncStartInfo.leftHandPos, syncEndInfo.leftHandPos, lerpProgress);
                _info.rightHandPos = Vector3.LerpUnclamped(syncStartInfo.rightHandPos, syncEndInfo.rightHandPos, lerpProgress);

                _info.headRot = Quaternion.LerpUnclamped(syncStartInfo.headRot, syncEndInfo.headRot, lerpProgress);
                _info.leftHandRot = Quaternion.LerpUnclamped(syncStartInfo.leftHandRot, syncEndInfo.leftHandRot, lerpProgress);
                _info.rightHandRot = Quaternion.LerpUnclamped(syncStartInfo.rightHandRot, syncEndInfo.rightHandRot, lerpProgress);

                Misc.Logger.Info($"SyncDelay: {(syncDelay * 1000).ToString("0.0")}, FUPS: {1f/Time.fixedDeltaTime}");
            }
        }

        public void OnDestroy()
        {
#if DEBUG
            Misc.Logger.Info("Destroying player controller");
#endif
            Destroy(avatar.gameObject);
        }

        public void UpdatePrevPosRot(PlayerInfo newPlayerInfo)
        {
            if (newPlayerInfo == null || _info == null)
                return;

            syncTime = 0;
            syncDelay = Time.time - lastSynchronizationTime;
            lastSynchronizationTime = Time.time;

            syncStartInfo = _info;
            syncEndInfo = newPlayerInfo;
        }
    }
}
