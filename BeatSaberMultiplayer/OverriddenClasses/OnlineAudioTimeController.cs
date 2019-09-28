using UnityEngine;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class OnlineAudioTimeController : AudioTimeSyncController
    {
        public OnlinePlayerController owner;

        public void Init(OnlinePlayerController newOwner)
        {
            owner = newOwner;

            _songTime = owner.playerInfo.updateInfo.playerProgress;
        }

        public override void Update()
        {
            if (owner != null)
            {
                _songTime = Mathf.Max(0f, owner.playerInfo.updateInfo.playerProgress);
            }
        }

        public override void Awake()
        {
        }

    }
}
