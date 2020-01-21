using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BeatSaverDownloader;
using BeatSaberMarkupLanguage;
using HMUI;
using BeatSaverDownloader.UI;
using BeatSaberMultiplayerLite.OverriddenClasses;

namespace BeatSaberMultiplayerLite.Misc
{
    internal class SongDownloaderInterop : ISongDownloader
    {
        private FlowCoordinator _coordinator;
        public bool CanCreate { get { return CustomMoreSongsFlowCoordinator.CanCreate; } }

        public FlowCoordinator PresentDownloaderFlowCoordinator(FlowCoordinator parent, Action dismissedCallback)
        {
            try
            {
                if (_coordinator == null)
                {
                    CustomMoreSongsFlowCoordinator moreSongsFlow = BeatSaberUI.CreateFlowCoordinator<CustomMoreSongsFlowCoordinator>();

                    moreSongsFlow.ParentFlowCoordinator = parent;
                    _coordinator = moreSongsFlow;
                }
                
                parent.PresentFlowCoordinator(_coordinator, dismissedCallback);
                return _coordinator;
            }catch(Exception ex)
            {
                Plugin.log.Error($"Error creating MoreSongsFlowCoordinator: {ex.Message}");
                Plugin.log.Debug(ex);
                return null;
            }
        }
    }
}
