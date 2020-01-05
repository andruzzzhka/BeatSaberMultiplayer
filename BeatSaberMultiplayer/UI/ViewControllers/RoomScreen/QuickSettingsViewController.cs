using BeatSaberMarkupLanguage;
using HMUI;
using System.Reflection;
using UnityEngine;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class QuickSettingsViewController : ViewController
    {
        private Settings _settings;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                _settings = new GameObject("Multiplayer Quick Settings").AddComponent<Settings>();
                BSMLParser.instance.Parse(Utilities.GetResourceContent(Assembly.GetAssembly(this.GetType()), "BeatSaberMultiplayer.UI.ViewControllers.RoomScreen.QuickSettingsViewController"), gameObject, _settings);
            }
        }
    }
}
