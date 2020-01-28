using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HMUI;

namespace BeatSaberMultiplayer.Misc
{
    public interface ISongDownloader
    {
        bool CanCreate { get; }
        FlowCoordinator PresentDownloaderFlowCoordinator(FlowCoordinator parent, Action dismissedCallback);
    }
}
