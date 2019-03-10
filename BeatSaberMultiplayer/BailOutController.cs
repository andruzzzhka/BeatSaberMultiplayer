using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TMPro;

namespace BeatSaberMultiplayer
{
    class BailOutController : MonoBehaviour
    {
        #region "Fields with get/setters"
        private static BailOutController _instance;
        private LevelFailedTextEffect _levelFailedEffect;
        private StandardLevelGameplayManager _gameManager;
        private GameEnergyCounter _energyCounter;
        private TextMeshProUGUI _failText;
        private PlayerSpecificSettings _playerSettings;
        private StandardLevelSceneSetup _standardLevelSceneSetup;
        #endregion
        #region "Fields"
        public bool isHiding = false;
        public static int numFails = 0;

        #region Settings
        
        public static bool BailOutEnabled = false;  // Is BailOutMode enabled
        public static int EnergyResetAmount = 50; // Amount of energy to reset to on failure
        public static bool ShowFailEffect = true; // Whether to show the giant Failed effect on failure
        public static int FailEffectDuration = 2; // How long the Failed effect remains on screen (seconds)
        public static bool ShowFailText = true; // Whether to show the Bailed out text
        public static string FailTextPosition = "0,.3,2.5"; // Position of the Bailed out text
        public static float FailTextFontSize = 20f; // Font size for the Bailed out text
        #endregion
        public TextMeshProUGUI FailText
        {
            get
            {
                if (_failText == null)
                    _failText = CreateFailText("");
                return _failText;
            }
            set { _failText = value; }
        }

        private LevelFailedTextEffect LevelFailedEffect
        {
            get
            {
                if (_levelFailedEffect == null)
                    _levelFailedEffect = GameObject.FindObjectsOfType<LevelFailedTextEffect>().FirstOrDefault();
                return _levelFailedEffect;
            }
            set { _levelFailedEffect = value; }
        }
        private StandardLevelGameplayManager GameManager
        {
            get
            {
                if (_gameManager == null)
                    _gameManager = GameObject.FindObjectsOfType<StandardLevelGameplayManager>().FirstOrDefault();
                return _gameManager;
            }
        }

        private StandardLevelSceneSetup standardLevelSceneSetup
        {
            get
            {
                if (_standardLevelSceneSetup == null)
                    _standardLevelSceneSetup = GameObject.FindObjectsOfType<StandardLevelSceneSetup>().FirstOrDefault();
                return _standardLevelSceneSetup;
            }
        }
        private GameEnergyCounter EnergyCounter
        {
            get
            {
                if (_energyCounter == null)
                    _energyCounter = GameObject.FindObjectsOfType<GameEnergyCounter>().FirstOrDefault();
                return _energyCounter;
            }
        }
        private PlayerSpecificSettings PlayerSettings
        {
            get
            {
                if (_playerSettings == null)
                    _playerSettings = standardLevelSceneSetup?.standardLevelSceneSetupData?.gameplayCoreSetupData?.playerSpecificSettings;
                return _playerSettings;
            }
        }
        private float PlayerHeight;
        public static BailOutController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("BailOutController").AddComponent<BailOutController>();
                }
                return _instance;
            }
        }
        public static bool InstanceExists
        {
            get { return _instance != null; }
        }

        #endregion

        public void Awake()
        {
            _instance = this;
            isHiding = false;
        }

        public void Start()
        {
            LevelFailedEffect = GameObject.FindObjectsOfType<LevelFailedTextEffect>().FirstOrDefault();
            if ((GameManager != null) && (EnergyCounter != null) && BailOutEnabled)
            {
                EnergyCounter.gameEnergyDidReach0Event -= GameManager.HandleGameEnergyDidReach0;
            }
            if (PlayerSettings != null)
                PlayerHeight = PlayerSettings.playerHeight;
            else
                PlayerHeight = 1.8f;
        }

        private void OnDestroy()
        {
        }


        public void ShowLevelFailed()
        {
            BS_Utils.Gameplay.ScoreSubmission.DisableSubmission(Plugin.instance.Name);
            if(ShowFailText)
                FailText.text = $"Bailed Out {numFails} time{(numFails != 1 ? "s" : "")}";
            if (!isHiding && ShowFailEffect)
            {
                try
                {
                    LevelFailedEffect.gameObject.SetActive(true);
                    LevelFailedEffect.ShowEffect();
                    if (FailEffectDuration > 0)
                        StartCoroutine(hideLevelFailed());
                    else
                        isHiding = true; // Fail text never hides, so don't try to keep showing it
                }
                catch (Exception ex)
                {
                    Misc.Logger.Exception(ex);
                }
            }
        }

        public IEnumerator<WaitForSeconds> hideLevelFailed()
        {
            if (!isHiding)
            {
                isHiding = true;
                yield return new WaitForSeconds(FailEffectDuration);
                LevelFailedEffect.gameObject.SetActive(false);
                isHiding = false;
            }
            yield break;
        }

        public static void FacePosition(Transform obj, Vector3 targetPos)
        {
            var rotAngle = Quaternion.LookRotation(obj.position - targetPos);
            obj.rotation = rotAngle;
        }

        public TextMeshProUGUI CreateFailText(string text, float yOffset = 0)
        {
            var textGO = new GameObject();
            textGO.transform.position = StringToVector3(FailTextPosition);
            textGO.transform.eulerAngles = new Vector3(0f, 0f, 0f);
            textGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            var textCanvas = textGO.AddComponent<Canvas>();
            textCanvas.renderMode = RenderMode.WorldSpace;
            (textCanvas.transform as RectTransform).sizeDelta = new Vector2(200f, 50f);

            FacePosition(textCanvas.transform, new Vector3(0, PlayerHeight, 0));
            TextMeshProUGUI textMeshProUGUI = new GameObject("BailOutFailText").AddComponent<TextMeshProUGUI>();

            RectTransform rectTransform = textMeshProUGUI.transform as RectTransform;
            rectTransform.anchoredPosition = new Vector2(0f, 0f);
            rectTransform.sizeDelta = new Vector2(400f, 20f);
            rectTransform.Translate(new Vector3(0, yOffset, 0));
            textMeshProUGUI.text = text;
            textMeshProUGUI.fontSize = FailTextFontSize;
            textMeshProUGUI.alignment = TextAlignmentOptions.Center;
            textMeshProUGUI.ForceMeshUpdate();
            textMeshProUGUI.rectTransform.SetParent(textCanvas.transform, false);
            return textMeshProUGUI;
        }

        public static Vector3 StringToVector3(string vStr)
        {
            string[] sAry = vStr.Split(',');
            try
            {
                Vector3 retVal = new Vector3(
                    float.Parse(sAry[0]),
                    float.Parse(sAry[1]),
                    float.Parse(sAry[2]));
                return retVal;
            }
            catch (Exception ex)
            {
                return new Vector3(0f, .3f, 2.5f);
            }
        }
    }
}
