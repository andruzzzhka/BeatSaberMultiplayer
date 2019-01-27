using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRUI;
using UnityEngine.UI;
using BeatSaberMultiplayer.Data;
using UnityEngine;
using TMPro;
using CustomUI.BeatSaber;
using System.Collections;

namespace BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen
{
    class PresetsListViewController : VRUIViewController, TableView.IDataSource
    {
        public event Action<RoomPreset> didFinishEvent;

        private Button _backButton;
        private Button _pageUpButton;
        private Button _pageDownButton;

        TableView _presetsTableView;
        LevelListTableCell _presetsTableCellInstance;

        TextMeshProUGUI _selectPresetText;

        List<RoomPreset> availablePresets = new List<RoomPreset>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform, () => { didFinishEvent?.Invoke(null); });

                _presetsTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _presetsTableView.PageScrollUp();

                });
                _pageUpButton.interactable = false;

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _presetsTableView.PageScrollDown();

                });
                _pageDownButton.interactable = false;

                RectTransform container = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
                container.SetParent(rectTransform, false);
                container.anchorMin = new Vector2(0.3f, 0.5f);
                container.anchorMax = new Vector2(0.7f, 0.5f);
                container.sizeDelta = new Vector2(0f, 60f);
                container.anchoredPosition = new Vector2(0f, -3f);

                _presetsTableView = new GameObject("CustomTableView").AddComponent<TableView>();
                _presetsTableView.gameObject.AddComponent<RectMask2D>();
                _presetsTableView.transform.SetParent(container, false);

                _presetsTableView.SetPrivateField("_isInitialized", false);
                _presetsTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _presetsTableView.Init();
                
                (_presetsTableView.transform as RectTransform).anchorMin = new Vector2(0f, 0f);
                (_presetsTableView.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
                (_presetsTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 0f);
                (_presetsTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, 0f);

                ReflectionUtil.SetPrivateField(_presetsTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_presetsTableView, "_pageDownButton", _pageDownButton);

                _presetsTableView.didSelectRowEvent += SongsTableView_DidSelectRow;
                _presetsTableView.dataSource = this;

                _selectPresetText = BeatSaberUI.CreateText(rectTransform, "Select preset", new Vector2(0f, 36f));
                _selectPresetText.fontSize = 8f;
                _selectPresetText.alignment = TextAlignmentOptions.Center;
                _selectPresetText.rectTransform.sizeDelta = new Vector2(120f, 6f);

            }
            else
            {
                _presetsTableView.ReloadData();
            }
        }

        private void SongsTableView_DidSelectRow(TableView sender, int row)
        {
            didFinishEvent?.Invoke(availablePresets[row]);
        }

        public void SetPresets(List<RoomPreset> presets)
        {
            availablePresets = presets;

            if (_presetsTableView != null)
            {
                if (_presetsTableView.dataSource != this)
                {
                    _presetsTableView.dataSource = this;
                }
                else
                {
                    _presetsTableView.ReloadData();
                }

                StartCoroutine(ScrollWithDelay());
            }
        }

        IEnumerator ScrollWithDelay()
        {
            yield return null;
            yield return null;

            _presetsTableView.ScrollToRow(0, false);
        }

        public TableCell CellForRow(int row)
        {
            LevelListTableCell cell = Instantiate(_presetsTableCellInstance);

            RoomPreset preset = availablePresets[row];

            cell.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(x => x.name == "CoverImage").enabled = false;

            cell.songName = $"{preset.GetName()}";
            cell.author = $"{preset.settings.Name}";

            cell.reuseIdentifier = "PresetCell";

            return cell;
        }

        public int NumberOfRows()
        {
            return availablePresets.Count;
        }

        public float RowHeight()
        {
            return 10f;
        }
    }
}
