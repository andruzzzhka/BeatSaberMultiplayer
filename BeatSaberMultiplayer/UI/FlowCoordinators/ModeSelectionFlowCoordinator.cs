using BeatSaberMarkupLanguage;
using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.ModeSelectionScreen;
using BS_Utils.Utilities;
using HMUI;
using System.Linq;
using UnityEngine;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class ModeSelectionFlowCoordinator : FlowCoordinator
    {
        ViewControllers.ModeSelectionScreen.ModeSelectionViewController _selectionViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                title = "Select Mode";

                AvatarController.LoadAvatars();

                _selectionViewController = BeatSaberUI.CreateViewController<ViewControllers.ModeSelectionScreen.ModeSelectionViewController>();
                _selectionViewController.didSelectRooms += () =>
                {
                    PresentFlowCoordinator(PluginUI.instance.serverHubFlowCoordinator);
                };
                _selectionViewController.didSelectRadio += () =>
                {
                    PresentFlowCoordinator(PluginUI.instance.channelSelectionFlowCoordinator);
                };
            }

            showBackButton = true;

            ProvideInitialViewControllers(_selectionViewController, null, null);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if(topViewController == _selectionViewController)
            {
                Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First().InvokeMethod("DismissFlowCoordinator", this, null, false);
            }
        }
    }
}
