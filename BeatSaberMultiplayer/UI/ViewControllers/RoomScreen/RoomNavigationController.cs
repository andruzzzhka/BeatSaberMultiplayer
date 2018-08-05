using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class RoomNavigationController : VRUINavigationController
    {
        public TextMeshProUGUI _errorText;

        private Button _backButton;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform);
                _backButton.onClick.AddListener(delegate () { DismissModalViewController(null, false); PluginUI.instance.roomFlowCoordinator.LeaveRoom(); });

                _errorText = BeatSaberUI.CreateText(rectTransform, "", new Vector2(0f, -25f));
                _errorText.fontSize = 8f;
                _errorText.alignment = TextAlignmentOptions.Center;
                _errorText.rectTransform.sizeDelta = new Vector2(120f, 6f);
            }
        }

        public void DisplayError(string error)
        {
            if(_errorText != null)
                _errorText.text = "Can't join room!\n"+error;
        }

    }
}
