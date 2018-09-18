using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BeatSaberMultiplayer.UI.UIElements
{
    class CustomSettingsHelper
    {
        public static T AddListSetting<T>(RectTransform parent, string name) where T : MonoBehaviour
        {
            var volumeSettings = Resources.FindObjectsOfTypeAll<VolumeSettingsController>().FirstOrDefault();
            GameObject newSettingsObject = UnityEngine.Object.Instantiate(volumeSettings.gameObject, parent);
            newSettingsObject.name = name;

            VolumeSettingsController volume = newSettingsObject.GetComponent<VolumeSettingsController>();
            T newListSettingsController = volume.gameObject.AddComponent<T>();
            UnityEngine.Object.DestroyImmediate(volume);

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            return newListSettingsController;
        }

        public static T AddToggleSetting<T>(RectTransform parent, string name) where T : MonoBehaviour
        {
            var volumeSettings = Resources.FindObjectsOfTypeAll<WindowModeSettingsController>().FirstOrDefault();
            GameObject newSettingsObject = UnityEngine.Object.Instantiate(volumeSettings.gameObject, parent);
            newSettingsObject.name = name;

            WindowModeSettingsController volume = newSettingsObject.GetComponent<WindowModeSettingsController>();
            T newToggleSettingsController = volume.gameObject.AddComponent<T>();
            UnityEngine.Object.DestroyImmediate(volume);

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            return newToggleSettingsController;
        }
    }
}
