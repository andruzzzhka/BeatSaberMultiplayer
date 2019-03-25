using CustomUI.BeatSaber;
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
    class MultiplayerNavigationController : VRUINavigationController
    {
        public event Action didFinishEvent;

        private GameObject _loadingIndicator;
        private Button _backButton;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation)
            {
                if (activationType == ActivationType.AddedToHierarchy)
                {
                    _backButton = BeatSaberUI.CreateBackButton(rectTransform);
                    _backButton.onClick.AddListener(delegate () { didFinishEvent?.Invoke(); });

                    _loadingIndicator = BeatSaberUI.CreateLoadingSpinner(rectTransform);
                    _loadingIndicator.SetActive(false);
                }
            }
        }

        public void SetLoadingState(bool loading)
        {
            _loadingIndicator.SetActive(loading);
        }


    }
}
