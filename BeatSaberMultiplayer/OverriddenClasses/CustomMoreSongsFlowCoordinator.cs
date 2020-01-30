using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage;
using BeatSaverDownloader.UI.ViewControllers;
using BeatSaverDownloader.UI;
using HMUI;
using BeatSaberMultiplayer.IPAUtilities;
using System.Reflection;
using BeatSaberMultiplayer.Interop;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class CustomMoreSongsFlowCoordinator : MoreSongsFlowCoordinator, IDismissable
    {
        public static bool CanCreate { get; private set; }
        public FlowCoordinator ParentFlowCoordinator { get; set; }

        private static FieldAccessor<MoreSongsFlowCoordinator, SongDetailViewController>.Accessor SongDetailViewController;
        private static FieldAccessor<MoreSongsFlowCoordinator, NavigationController>.Accessor MoreSongsNavigationController;
        private static FieldAccessor<MoreSongsFlowCoordinator, MoreSongsListViewController>.Accessor MoreSongsView;
        private static FieldAccessor<MoreSongsFlowCoordinator, DownloadQueueViewController>.Accessor DownloadQueueView;
        private static MethodInfo AbortAllDownloadsMethod;

        static CustomMoreSongsFlowCoordinator()
        {

            try
            {
                SongDetailViewController = FieldAccessor<MoreSongsFlowCoordinator, SongDetailViewController>.GetAccessor("_songDetailView");
                MoreSongsNavigationController = FieldAccessor<MoreSongsFlowCoordinator, NavigationController>.GetAccessor("_moreSongsNavigationcontroller");
                MoreSongsView = FieldAccessor<MoreSongsFlowCoordinator, MoreSongsListViewController>.GetAccessor("_moreSongsView");
                DownloadQueueView = FieldAccessor<MoreSongsFlowCoordinator, DownloadQueueViewController>.GetAccessor("_downloadQueueView");
                string abortDownloadsMethodName = "AbortAllDownloads";
                AbortAllDownloadsMethod = typeof(DownloadQueueViewController).GetMethod(abortDownloadsMethodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (AbortAllDownloadsMethod == null) throw new MissingMethodException($"Method {abortDownloadsMethodName} does not exist.", abortDownloadsMethodName);
                CanCreate = true;
            }
            catch (Exception ex)
            {
                CanCreate = false;
                Plugin.log.Error($"Error creating accessors for MoreSongsFlowCoordinator, Downloader will be unavailable in Multiplayer: {ex.Message}");
                Plugin.log.Debug(ex);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            Dismiss(false);
        }

        public void Dismiss(bool immediately)
        {
            MoreSongsFlowCoordinator thisCoordinator = (MoreSongsFlowCoordinator)this;
            if (SongDetailViewController(ref thisCoordinator).isInViewControllerHierarchy)
            {
                PopViewControllersFromNavigationController(MoreSongsNavigationController(ref thisCoordinator), 1, null, true);
            }
            MoreSongsView(ref thisCoordinator).Cleanup();
            AbortAllDownloadsMethod.Invoke(DownloadQueueView(ref thisCoordinator), null);

            ParentFlowCoordinator.DismissFlowCoordinator(this, null, immediately);
        }
    }
}
