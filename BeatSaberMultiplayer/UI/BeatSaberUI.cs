using BeatSaberMultiplayer.Misc;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRUI;
using Image = UnityEngine.UI.Image;

namespace BeatSaberMultiplayer.UI
{
    class BeatSaberUI : MonoBehaviour
    {

        private Button _buttonInstance;
        private Button _backButtonInstance;
        private GameObject _loadingIndicatorInstance;

        public static BeatSaberUI _instance;

        public static List<Sprite> icons = new List<Sprite>();

        internal static void OnLoad()
        {
            if (_instance != null)
            {
                return;
            }
            new GameObject("BeatSaber Custom UI").AddComponent<BeatSaberUI>();

        }

        private void Awake()
        {
            _instance = this;

            icons.AddRange(Resources.FindObjectsOfTypeAll<Sprite>());

            try
            {
                _buttonInstance = Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton"));
                _backButtonInstance = Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "BackArrowButton"));
                _loadingIndicatorInstance = Resources.FindObjectsOfTypeAll<GameObject>().Where(x => x.name == "LoadingIndicator").First();
            }
            catch(Exception e)
            {
                Console.WriteLine($"EXCEPTION ON AWAKE(TRY FIND BUTTONS): {e}");
            }            
        }       

        public static Button CreateUIButton(RectTransform parent, string buttonTemplate)
        {
            Button btn = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == buttonTemplate)), parent, false);
            DestroyImmediate(btn.GetComponent<GameEventOnUIButtonClick>());
            btn.onClick = new Button.ButtonClickedEvent();
            btn.name = "CustomUIButton";

            return btn;
        }

        public static Button CreateBackButton(RectTransform parent)
        {
            if (_instance._backButtonInstance == null)
            {
                return null;
            }

            Button btn = Instantiate(_instance._backButtonInstance, parent, false);
            DestroyImmediate(btn.GetComponent<GameEventOnUIButtonClick>());
            btn.onClick = new Button.ButtonClickedEvent();
            btn.name = "CustomUIButton";

            return btn;
        }

        public static T CreateViewController<T>() where T : VRUIViewController
        {
            T vc = new GameObject("CutomViewController").AddComponent<T>();

            vc.rectTransform.anchorMin = new Vector2(0f, 0f);
            vc.rectTransform.anchorMax = new Vector2(1f, 1f);
            vc.rectTransform.sizeDelta = new Vector2(0f, 0f);
            vc.rectTransform.anchoredPosition = new Vector2(0f, 0f);

            return vc;
        }

        public static GameObject CreateLoadingIndicator(Transform parent)
        {
            GameObject indicator = Instantiate(_instance._loadingIndicatorInstance, parent, false);
            (indicator.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (indicator.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (indicator.transform as RectTransform).anchoredPosition = new Vector2(0f, 0f);
            indicator.name = "CustomUILoading";
            indicator.SetActive(true);

            return indicator;
        }

        public static TextMeshProUGUI CreateText(RectTransform parent, string text, Vector2 position)
        {
            TextMeshProUGUI textMesh = new GameObject("CustomUIText").AddComponent<TextMeshProUGUI>();
            textMesh.rectTransform.SetParent(parent, false);
            textMesh.text = text;
            textMesh.fontSize = 4;
            textMesh.color = Color.white;
            textMesh.font = Resources.Load<TMP_FontAsset>("Teko-Medium SDF No Glow");
            textMesh.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            textMesh.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            textMesh.rectTransform.sizeDelta = new Vector2(60f, 10f);
            textMesh.rectTransform.anchoredPosition = position;

            return textMesh;
        }

        public TextMeshPro CreateWorldText(Transform parent, string text)
        {
            TextMeshPro textMesh = new GameObject("CustomUIText").AddComponent<TextMeshPro>();
            textMesh.transform.SetParent(parent, false);
            textMesh.text = text;
            textMesh.fontSize = 5;
            textMesh.color = Color.white;
            textMesh.font = Resources.Load<TMP_FontAsset>("Teko-Medium SDF No Glow");
            
            return textMesh;
        }

        public static void SetButtonText(Button _button, string _text)
        {
            if (_button.GetComponentInChildren<TextMeshProUGUI>() != null)
            {

                _button.GetComponentInChildren<TextMeshProUGUI>().text = _text;
            }

        }

        public static void SetButtonTextSize(Button _button, float _fontSize)
        {
            if (_button.GetComponentInChildren<TextMeshProUGUI>() != null)
            {
                _button.GetComponentInChildren<TextMeshProUGUI>().fontSize = _fontSize;
            }
            

        }

        public static void SetButtonIcon(Button _button, Sprite _icon)
        {
            if (_button.GetComponentsInChildren<Image>().Count() > 1)
            {

                _button.GetComponentsInChildren<Image>().First(x => x.name == "Icon").sprite = _icon;
            }

        }

        public static void SetButtonBackground(ref Button _button, Sprite _background)
        {
            if (_button.GetComponentsInChildren<Image>().Count() > 0)
            {

                _button.GetComponentsInChildren<Image>()[0].sprite = _background;
            }

        }


    }
}
