using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers
{
    class ServerHubNavigationController : VRUINavigationController
    {
        private GameObject _loadingIndicator;
        private Button _backButton;

        public RoomListViewController roomListViewController;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {

            if(firstActivation)
            {
                if (activationType == ActivationType.AddedToHierarchy)
                {
                    _backButton = BeatSaberUI.CreateBackButton(rectTransform);
                    _backButton.onClick.AddListener(delegate () { DismissModalViewController(null, false); });

                    _loadingIndicator = BeatSaberUI.CreateLoadingIndicator(rectTransform);

                    roomListViewController = BeatSaberUI.CreateViewController<RoomListViewController>();
                }
            }
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {

        }

        public void SetLoadingState(bool loading)
        {
            _loadingIndicator.SetActive(loading);
        }


    }
}
