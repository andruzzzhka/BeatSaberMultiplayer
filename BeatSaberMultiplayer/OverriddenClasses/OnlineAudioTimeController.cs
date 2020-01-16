using UnityEngine;
using System.IO;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class OnlineAudioTimeController : AudioTimeSyncController
    {
        public OnlinePlayerController owner;

        //public StreamWriter writer;

        public void Init(OnlinePlayerController newOwner)
        {
            owner = newOwner;

            _songTime = owner.playerInfo.updateInfo.playerProgress;

            //writer = new StreamWriter(File.Create($"AudioSyncLog_{owner.playerInfo.playerName}.csv"));

            //writer.WriteLine($"{Time.time},{_songTime}");
            //writer.Flush();
        }

        public override void Start()
        {
            
        }

        public override void Update()
        {
            if (owner != null)
            {
                _songTime = Mathf.Max(0f, owner.playerInfo.updateInfo.playerProgress);
                //writer.WriteLine($"{Time.time},{_songTime}");
                //writer.Flush();
            }
        }

        public override void Awake()
        {
        }

    }
}
