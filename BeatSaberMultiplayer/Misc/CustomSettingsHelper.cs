using Polyglot;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BeatSaberMultiplayer.Misc
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
            UnityEngine.Object.DestroyImmediate(newSettingsObject.GetComponentInChildren<LocalizedTextMeshProUGUI>());

            TMP_Text nameText = newSettingsObject.GetComponentsInChildren<TMP_Text>().First(x => x.name == "NameText");
            UnityEngine.Object.Destroy(nameText.gameObject.GetComponent<LocalizedTextMeshProUGUI>());
            nameText.text = name;

            (newListSettingsController.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (newListSettingsController.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (newListSettingsController.transform as RectTransform).anchoredPosition = position;
            (newListSettingsController.transform as RectTransform).sizeDelta = new Vector2(105f, 0f);

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

            TMP_Text nameText = newSettingsObject.GetComponentsInChildren<TMP_Text>().First(x => x.name == "NameText");
            UnityEngine.Object.Destroy(nameText.gameObject.GetComponent<LocalizedTextMeshProUGUI>());
            nameText.text = name;

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
            UnityEngine.Object.DestroyImmediate(newSettingsObject.GetComponentInChildren<LocalizedTextMeshProUGUI>());

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            (newToggleSettingsController.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (newToggleSettingsController.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (newToggleSettingsController.transform as RectTransform).anchoredPosition = position;
            (newToggleSettingsController.transform as RectTransform).sizeDelta = new Vector2(105f, 0f);

            return newToggleSettingsController;
        }
    }
}
