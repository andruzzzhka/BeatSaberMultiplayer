using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using VRUI;
using VRUIControls;
using TMPro;
using IllusionPlugin;

namespace BeatSaberMultiplayer
{
    public class ListViewController : ListSettingsController
    {
        public delegate float GetFloat();
        public event GetFloat GetValue;

        public delegate void SetFloat(float value);
        public event SetFloat SetValue;

        public delegate string StringForValue(float value);
        public event StringForValue FormatValue;

        protected float[] values;

        public void SetValues(float[] values)
        {
            this.values = values;
        }

        protected override void GetInitValues(out int idx, out int numberOfElements)
        {
            numberOfElements = values.Length;
            float value = 0;
            if (GetValue != null)
            {
                value = GetValue();
            }

            idx = numberOfElements - 1;
            for (int j = 0; j < values.Length; j++)
            {
                if (value == values[j])
                {
                    idx = j;
                    return;
                }
            }
        }

        protected override void ApplyValue(int idx)
        {
            if (SetValue != null)
            {
                SetValue(values[idx]);
            }
        }

        protected override string TextForValue(int idx)
        {
            string text = "?";
            if (FormatValue != null)
            {
                text = FormatValue(values[idx]);
            }
            return text;
        }
    }

    public class BoolViewController : SwitchSettingsController
    {
        public delegate bool GetBool();
        public event GetBool GetValue;

        public delegate void SetBool(bool value);
        public event SetBool SetValue;

        protected override bool GetInitValue()
        {
            bool value = false;
            if (GetValue != null)
            {
                value = GetValue();
            }
            return value;
        }

        protected override void ApplyValue(bool value)
        {
            if (SetValue != null)
            {
                SetValue(value);
            }
        }

        protected override string TextForValue(bool value)
        {
            return (value) ? "ON" : "OFF";
        }
    }

    public class SubMenu
    {
        public Transform transform;

        public SubMenu(Transform transform)
        {
            this.transform = transform;
        }

        public BoolViewController AddBool(string name)
        {
            return AddToggleSetting<BoolViewController>(name);
        }

        public ListViewController AddList(string name, float[] values)
        {
            var view = AddListSetting<ListViewController>(name);
            view.SetValues(values);
            return view;
        }

        public T AddListSetting<T>(string name) where T : ListSettingsController
        {
            var volumeSettings = Resources.FindObjectsOfTypeAll<VolumeSettingsController>().FirstOrDefault();
            GameObject newSettingsObject = MonoBehaviour.Instantiate(volumeSettings.gameObject, transform);
            newSettingsObject.name = name;

            VolumeSettingsController volume = newSettingsObject.GetComponent<VolumeSettingsController>();
            T newListSettingsController = (T)ReflectionUtil.CopyComponent(volume, typeof(ListSettingsController), typeof(T), newSettingsObject);
            MonoBehaviour.DestroyImmediate(volume);

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            return newListSettingsController;
        }

        public T AddToggleSetting<T>(string name) where T : SwitchSettingsController
        {
            var volumeSettings = Resources.FindObjectsOfTypeAll<WindowModeSettingsController>().FirstOrDefault();
            GameObject newSettingsObject = MonoBehaviour.Instantiate(volumeSettings.gameObject, transform);
            newSettingsObject.name = name;

            WindowModeSettingsController volume = newSettingsObject.GetComponent<WindowModeSettingsController>();
            T newToggleSettingsController = (T)ReflectionUtil.CopyComponent(volume, typeof(SwitchSettingsController), typeof(T), newSettingsObject);
            MonoBehaviour.DestroyImmediate(volume);

            newSettingsObject.GetComponentInChildren<TMP_Text>().text = name;

            return newToggleSettingsController;
        }
    }

    public class SettingsUI : MonoBehaviour
    {
        public const int MainScene = 1;
        public const int GameScene = 5;

        public static SettingsUI Instance = null;
        MainMenuViewController _mainMenuViewController = null;
        SimpleDialogPromptViewController prompt = null;

        static MainSettingsTableCell tableCell = null;

        public static void OnLoad()
        {
            if (Instance != null) return;
            new GameObject("SettingsUI").AddComponent<SettingsUI>();
        }

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(this);
            }
        }

        public void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            if (scene.buildIndex == MainScene)
            {
                _mainMenuViewController = Resources.FindObjectsOfTypeAll<MainMenuViewController>().First();
                var _menuMasterViewController = Resources.FindObjectsOfTypeAll<StandardLevelSelectionFlowCoordinator>().First();
                prompt = ReflectionUtil.GetPrivateField<SimpleDialogPromptViewController>(_menuMasterViewController, "_simpleDialogPromptViewController");
            }
        }

        public static SubMenu CreateSubMenu(string name)
        {
            if (SceneManager.GetActiveScene().buildIndex != MainScene)
            {
                Console.WriteLine("Cannot create settings menu when no in the main scene.");
                return null;
            }

            if (tableCell == null)
            {
                tableCell = Resources.FindObjectsOfTypeAll<MainSettingsTableCell>().FirstOrDefault();
                // Get a refence to the Settings Table cell text in case we want to change fint size, etc
                var text = tableCell.GetPrivateField<TextMeshProUGUI>("_settingsSubMenuText");
            }

            var temp = Resources.FindObjectsOfTypeAll<SettingsViewController>().FirstOrDefault();
            var others = temp.transform.Find("SubSettingsViewControllers").Find("Others");
            var tweakSettingsObject = Instantiate(others.gameObject, others.transform.parent);
            Transform mainContainer = CleanScreen(tweakSettingsObject.transform);

            var tweaksSubMenu = new SettingsSubMenuInfo();
            tweaksSubMenu.SetPrivateField("_menuName", name);
            tweaksSubMenu.SetPrivateField("_controller", tweakSettingsObject.GetComponent<VRUIViewController>());

            var mainSettingsMenu = Resources.FindObjectsOfTypeAll<MainSettingsMenuViewController>().FirstOrDefault();
            var subMenus = mainSettingsMenu.GetPrivateField<SettingsSubMenuInfo[]>("_settingsSubMenuInfos").ToList();
            subMenus.Add(tweaksSubMenu);
            mainSettingsMenu.SetPrivateField("_settingsSubMenuInfos", subMenus.ToArray());

            // Find the table view and if page buttons don't exist add them

            SubMenu menu = new SubMenu(mainContainer);
            return menu;
        }

        static Transform CleanScreen(Transform screen)
        {
            var container = screen.Find("SettingsContainer");
            var tempList = container.Cast<Transform>().ToList();
            foreach (var child in tempList)
            {
                DestroyImmediate(child.gameObject);
            }
            return container;
        }
    }
}