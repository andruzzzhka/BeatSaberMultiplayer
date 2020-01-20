using BeatSaberMarkupLanguage;
using BeatSaberMultiplayer.Misc;
using HMUI;
using System;

namespace BeatSaberMultiplayer.UI.FlowCoordinators
{
    class ModeSelectionFlowCoordinator : FlowCoordinator
    {
        public event Action didFinishEvent;

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
                    //PresentFlowCoordinator(PluginUI.instance.channelSelectionFlowCoordinator);
                };
            }

            showBackButton = true;

            ProvideInitialViewControllers(_selectionViewController, null, null);
        }

        public void JoinGameWithSecret(string secret)
        {
            string ip = secret.Substring(0, secret.IndexOf(':'));
            int port = int.Parse(secret.Substring(secret.IndexOf(':') + 1, secret.IndexOf('?') - secret.IndexOf(':') - 1));
            uint roomId = uint.Parse(secret.Substring(secret.IndexOf('?') + 1, secret.IndexOf('#') - secret.IndexOf('?') - 1));
            string password = secret.Substring(secret.IndexOf('#') + 1);

            if (ModelSaberAPI.isCalculatingHashes)
            {
                ModelSaberAPI.hashesCalculated += () =>
                {
                    PresentFlowCoordinator(PluginUI.instance.serverHubFlowCoordinator, null, true);
                    PluginUI.instance.serverHubFlowCoordinator.JoinRoom(ip, port, roomId, !string.IsNullOrEmpty(password), password);
                };
            }
            else
            {
                PresentFlowCoordinator(PluginUI.instance.serverHubFlowCoordinator, null, true);
                PluginUI.instance.serverHubFlowCoordinator.JoinRoom(ip, port, roomId, !string.IsNullOrEmpty(password), password);
            }

        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if(topViewController == _selectionViewController)
            {
                didFinishEvent?.Invoke();
            }
        }
    }
}
