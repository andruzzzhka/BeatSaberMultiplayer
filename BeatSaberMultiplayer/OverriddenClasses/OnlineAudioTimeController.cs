using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class OnlineAudioTimeController : AudioTimeSyncController
    {
        public OnlinePlayerController owner;
        private float offset = 0.15f;

        public void Init(OnlinePlayerController newOwner)
        {
            owner = newOwner;

            _songTime = owner.PlayerInfo.playerProgress;
        }

        public override void Update()
        {
            if(owner != null)
                _songTime = Mathf.Max(0f, owner.PlayerInfo.playerProgress - offset);
            
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Misc.Logger.Info("New offset: " + offset);
                offset += 0.025f;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                offset -= 0.025f;
                Misc.Logger.Info("New offset: "+offset);
            }
        }

        public override void Awake()
        {
        }

    }
}
