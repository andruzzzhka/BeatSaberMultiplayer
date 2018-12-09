using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer
{
    class OnlinePauseAnimController : PauseAnimationController
    {

        public override void StartEnterPauseAnimation()
        {
            EnterPauseAnimationDidFinish();
        }

        public override void StartResumeFromPauseAnimation()
        {
            ResumeFromPauseAnimationDidFinish();
        }
    }
}
