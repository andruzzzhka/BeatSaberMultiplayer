using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class RoomManagementViewController : VRUIViewController
    {
        public event Action DestroyRoomPressed;

        Button _destroyRoomButton;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if(firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                _destroyRoomButton = BeatSaberUI.CreateUIButton(rectTransform, "SettingsButton");
                BeatSaberUI.SetButtonText(_destroyRoomButton, "DESTROY ROOM");
                _destroyRoomButton.onClick.AddListener(delegate() {
                    DestroyRoomPressed?.Invoke();
                });
                
            }

        }


        public void UpdateViewController(bool isHost)
        {
            _destroyRoomButton.gameObject.SetActive(isHost);
        }
    }
}
