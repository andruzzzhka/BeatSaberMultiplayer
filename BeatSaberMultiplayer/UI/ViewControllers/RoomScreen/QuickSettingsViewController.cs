using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI.UIElements;
using CustomUI.BeatSaber;
using HMUI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class QuickSettingsViewController : BSMLViewController
    {
        public override string Content => Utilities.GetResourceContent(Assembly.GetAssembly(this.GetType()), "BeatSaberMultiplayer.UI.ViewControllers.RoomScreen.QuickSettingsViewController");

        private Settings _settings;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                _settings = new GameObject("Multiplayer Quick Settings").AddComponent<Settings>();
                BSMLParser.instance.Parse(Content, gameObject, _settings);
            }

            didActivate?.Invoke(firstActivation, activationType);
        }


    }
}
