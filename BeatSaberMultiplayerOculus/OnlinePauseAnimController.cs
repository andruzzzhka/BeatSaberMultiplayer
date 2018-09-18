using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer
{
    class OnlinePauseAnimController : ResumePauseAnimationController
    {

        public override void StartAnimation()
        {
            AnimationDidFinish();
        }

        public override void StopAnimation()
        {
        }
    }
}
