using System;
using BeatSaberMarkupLanguage;
using HMUI;
using BeatSaberMultiplayer.OverriddenClasses;

namespace BeatSaberMultiplayer.Misc
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
                Plugin.log.Error($"Error creating MoreSongsFlowCoordinator: {ex}");
                return null;
            }
        }
    }
}
