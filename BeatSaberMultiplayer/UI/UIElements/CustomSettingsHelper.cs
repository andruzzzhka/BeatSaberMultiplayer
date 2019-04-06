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
        public static T AddListSetting<T>(RectTransform parent, string name, Vector2 position) where T : MonoBehaviour
        {
            var listSettings = Resources.FindObjectsOfTypeAll<FormattedFloatListSettingsController>().FirstOrDefault();
            GameObject newSettingsObject = UnityEngine.Object.Instantiate(listSettings.gameObject, parent);
            newSettingsObject.name = name;
            
            var incBg = newSettingsObject.transform.Find("Value").Find("IncButton").Find("BG").gameObject.GetComponent<UnityEngine.UI.Image>();
            (incBg.transform as RectTransform).localScale *= new Vector2(0.8f, 0.8f);
            var decBg = newSettingsObject.transform.Find("Value").Find("DecButton").Find("BG").gameObject.GetComponent<UnityEngine.UI.Image>();
            (decBg.transform as RectTransform).localScale *= new Vector2(0.8f, 0.8f);

            ListSettingsController volume = newSettingsObject.GetComponent<ListSettingsController>();
            T newListSettingsController = volume.gameObject.AddComponent<T>();
            UnityEngine.Object.DestroyImmediate(volume);

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            (newListSettingsController.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (newListSettingsController.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (newListSettingsController.transform as RectTransform).anchoredPosition = position;

            return newListSettingsController;
        }

        public static T AddListSetting<T>(RectTransform parent, string name) where T : MonoBehaviour
        {
            var listSettings = Resources.FindObjectsOfTypeAll<FormattedFloatListSettingsController>().FirstOrDefault();
            GameObject newSettingsObject = UnityEngine.Object.Instantiate(listSettings.gameObject, parent);
            newSettingsObject.name = name;

            var incBg = newSettingsObject.transform.Find("Value").Find("IncButton").Find("BG").gameObject.GetComponent<UnityEngine.UI.Image>();
            (incBg.transform as RectTransform).localScale *= new Vector2(0.8f, 0.8f);
            var decBg = newSettingsObject.transform.Find("Value").Find("DecButton").Find("BG").gameObject.GetComponent<UnityEngine.UI.Image>();
            (decBg.transform as RectTransform).localScale *= new Vector2(0.8f, 0.8f);

            ListSettingsController volume = newSettingsObject.GetComponent<ListSettingsController>();
            T newListSettingsController = volume.gameObject.AddComponent<T>();
            UnityEngine.Object.DestroyImmediate(volume);

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            return newListSettingsController;
        }

        public static T AddToggleSetting<T>(RectTransform parent, string name, Vector2 position) where T : MonoBehaviour
        {
            var switchSettings = Resources.FindObjectsOfTypeAll<SwitchSettingsController>().FirstOrDefault();
            GameObject newSettingsObject = UnityEngine.Object.Instantiate(switchSettings.gameObject, parent);
            newSettingsObject.name = name;

            SwitchSettingsController volume = newSettingsObject.GetComponent<SwitchSettingsController>();
            T newToggleSettingsController = volume.gameObject.AddComponent<T>();
            UnityEngine.Object.DestroyImmediate(volume);

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            (newToggleSettingsController.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (newToggleSettingsController.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (newToggleSettingsController.transform as RectTransform).anchoredPosition = position;

            return newToggleSettingsController;
        }
    }
}
