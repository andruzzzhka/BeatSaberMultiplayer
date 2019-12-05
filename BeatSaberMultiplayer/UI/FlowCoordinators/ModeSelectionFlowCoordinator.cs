using BeatSaberMultiplayer.UI.ViewControllers;
using BeatSaberMultiplayer.UI.ViewControllers.ModeSelectionScreen;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using System.Linq;
using UnityEngine;
using VRUI;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class ModeSelectionFlowCoordinator : FlowCoordinator
    {
        ModeSelectionViewController _selectionViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                title = "Select Mode";

                AvatarController.LoadAvatars();

                _selectionViewController = BeatSaberUI.CreateViewController<ModeSelectionViewController>();
                _selectionViewController.didSelectRooms += () =>
                {
                    if (!mainScreenViewControllers.Any(x => x.GetPrivateField<bool>("_isInTransition")))
                    { PresentFlowCoordinator(PluginUI.instance.serverHubFlowCoordinator); }
                };
                _selectionViewController.didSelectRadio += () =>
                {
                    if (!mainScreenViewControllers.Any(x => x.GetPrivateField<bool>("_isInTransition")))
                    { PresentFlowCoordinator(PluginUI.instance.channelSelectionFlowCoordinator); }
                };
                _selectionViewController.didFinishEvent += () =>
                {
                    Resources.FindObjectsOfTypeAll<MainFlowCoordinator>().First().InvokeMethod("DismissFlowCoordinator", this, null, false);
                };
            }
            
            ProvideInitialViewControllers(_selectionViewController, null, null);
        }
    }
}
