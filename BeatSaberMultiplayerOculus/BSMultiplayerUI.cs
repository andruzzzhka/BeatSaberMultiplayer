using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaberMultiplayer
{
    class BSMultiplayerUI : MonoBehaviour
    {
        public static BSMultiplayerUI _instance;

        public  RectTransform _mainMenuRectTransform;
        private MainMenuViewController _mainMenuViewController;
        private MultiplayerLobbyViewController _multiplayerLobbyViewController;

        private Button _buttonInstance;
        private Button _backButtonInstance;
        private GameObject _loadingIndicatorInstance;

        public static void OnLoad()
        {
            if (_instance != null)
            {
                _instance.Awake();
                return;
            }
            new GameObject("Multiplayer UI").AddComponent<BSMultiplayerUI>();

        }

        private void Awake()
        {
            _instance = this;

            DontDestroyOnLoad(this);

            Console.ForegroundColor = ConsoleColor.Gray;

            try
            {
                _buttonInstance = Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "QuitButton"));
                _backButtonInstance = Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "BackArrowButton"));
                _mainMenuViewController = Resources.FindObjectsOfTypeAll<MainMenuViewController>().First();
                _mainMenuRectTransform = _buttonInstance.transform.parent as RectTransform;
                _loadingIndicatorInstance = Resources.FindObjectsOfTypeAll<GameObject>().Where(x => x.name == "LoadingIndicator").First();

                Console.WriteLine("Buttons and main menu found!");

                
               

            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION ON AWAKE(TRY FIND BUTTONS): " + e);
            }

            try
            {
                CreateMultiplayerButton();

                Console.WriteLine("Online button created!");
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION ON AWAKE(TRY CREATE BUTTON): " + e);
            }
        }

        private void CreateMultiplayerButton()
        {

            Button _multiplayerButton = CreateUIButton(_mainMenuRectTransform, "PartyButton");
            Console.ForegroundColor = ConsoleColor.Gray;
            
            (Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "SoloButton").transform as RectTransform).anchoredPosition = new Vector2(-19f,8f);
            (Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "PartyButton").transform as RectTransform).anchoredPosition = new Vector2(-18f, 8f);


            try
            {
                (_multiplayerButton.transform as RectTransform).anchoredPosition = new Vector2(19f, 8f);
                (_multiplayerButton.transform as RectTransform).sizeDelta = new Vector2(36f, 36f);

                SetButtonText(ref _multiplayerButton, "Online");

                SetButtonIcon(ref _multiplayerButton, Base64ToSprite(Base64Sprites.onlineIcon));

                _multiplayerButton.onClick.AddListener(delegate () {

                    try
                    {

                        if (_multiplayerLobbyViewController == null)
                        {
                            _multiplayerLobbyViewController = CreateViewController<MultiplayerLobbyViewController>();


                        }
                        _mainMenuViewController.PresentModalViewController(_multiplayerLobbyViewController, null, false);

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("EXCETPION IN BUTTON: " + e.Message);
                    }

                });

            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION: " + e.Message);
            }
        }



        public Button CreateUIButton(RectTransform parent, string buttonTemplate)
        {
            if (_buttonInstance == null)
            {
                return null;
            }

            Button btn = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == buttonTemplate)), parent, false);
            DestroyImmediate(btn.GetComponent<GameEventOnUIButtonClick>());
            btn.onClick = new Button.ButtonClickedEvent();

            return btn;
        }

        public Button CreateBackButton(RectTransform parent)
        {
            if (_backButtonInstance == null)
            {
                return null;
            }

            Button _button = Instantiate(_backButtonInstance, parent, false);
            DestroyImmediate(_button.GetComponent<GameEventOnUIButtonClick>());
            _button.onClick = new Button.ButtonClickedEvent();

            return _button;
        }

        public T CreateViewController<T>() where T : VRUIViewController
        {
            T vc = new GameObject("CreatedViewController").AddComponent<T>();

            vc.rectTransform.anchorMin = new Vector2(0f, 0f);
            vc.rectTransform.anchorMax = new Vector2(1f, 1f);
            vc.rectTransform.sizeDelta = new Vector2(0f, 0f);
            vc.rectTransform.anchoredPosition = new Vector2(0f, 0f);

            return vc;
        }

        public GameObject CreateLoadingIndicator(Transform parent)
        {
            GameObject indicator = Instantiate(_loadingIndicatorInstance, parent, false);
            return indicator;
        }

        public TextMeshProUGUI CreateText(RectTransform parent, string text, Vector2 position)
        {
            TextMeshProUGUI textMesh = new GameObject("TextMeshProUGUI_GO").AddComponent<TextMeshProUGUI>();
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
            TextMeshPro textMesh = new GameObject("TextMeshPro_GO").AddComponent<TextMeshPro>();
            textMesh.transform.SetParent(parent, false);
            textMesh.text = text;
            textMesh.fontSize = 5;
            textMesh.color = Color.white;
            textMesh.font = Resources.Load<TMP_FontAsset>("Teko-Medium SDF No Glow");


            return textMesh;
        }

        public void SetButtonText(ref Button _button, string _text)
        {
            if (_button.GetComponentInChildren<TextMeshProUGUI>() != null)
            {

                _button.GetComponentInChildren<TextMeshProUGUI>().text = _text;
            }

        }

        public void SetButtonTextSize(ref Button _button, float _fontSize)
        {
            if (_button.GetComponentInChildren<TextMeshProUGUI>() != null)
            {
                _button.GetComponentInChildren<TextMeshProUGUI>().fontSize = _fontSize;
            }


        }

        public void SetButtonIcon(ref Button _button, Sprite _icon)
        {
            if (_button.GetComponentsInChildren<Image>().Count() > 1)
            {

                _button.GetComponentsInChildren<Image>()[1].sprite = _icon;
            }

        }

        public void SetButtonBackground(ref Button _button, Sprite _background)
        {
            if (_button.GetComponentsInChildren<Image>().Any())
            {

                _button.GetComponentsInChildren<Image>()[0].sprite = _background;
            }

        }

        public static Sprite Base64ToSprite(string base64)
        {
            Texture2D tex = Base64ToTexture2D(base64);
            return Sprite.Create(tex, new Rect(0,0,tex.width, tex.height), (Vector2.one / 2f) );
        }

        public static Texture2D Base64ToTexture2D(string encodedData)
        {
            byte[] imageData = Convert.FromBase64String(encodedData);

            int width, height;
            GetImageSize(imageData, out width, out height);

            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Trilinear;
            texture.LoadImage(imageData);
            return texture;
        }

        private static void GetImageSize(byte[] imageData, out int width, out int height)
        {
            width = ReadInt(imageData, 3 + 15);
            height = ReadInt(imageData, 3 + 15 + 2 + 2);
        }

        private static int ReadInt(byte[] imageData, int offset)
        {
            return (imageData[offset] << 8) | imageData[offset + 1];
        }
    }
}
