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
    class ModeSelectionViewController : VRUIViewController
    {
        public event Action didSelectRooms;
        public event Action didSelectRadio;

        Button _roomsButton;
        Button _radioButton;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                _roomsButton = BeatSaberUI.CreateUIButton(rectTransform, "SoloFreePlayButton", new Vector2(-5f, 10f), () => { didSelectRooms?.Invoke(); }, "ROOMS");
                _roomsButton.SetButtonIcon(Sprites.roomsIcon);

                _radioButton = BeatSaberUI.CreateUIButton(rectTransform, "SoloFreePlayButton", new Vector2(45f, 10f), () => { didSelectRadio?.Invoke(); }, "RADIO");
                _radioButton.SetButtonIcon(Sprites.radioIcon);
            }
        }
    }
}
