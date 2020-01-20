using HMUI;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using BeatSaberMultiplayer.Data;
using UnityEngine;
using TMPro;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;

namespace BeatSaberMultiplayer.UI.ViewControllers.CreateRoomScreen
{
    class PresetsListViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action<RoomPreset> didFinishEvent;

        [UIComponent("presets-list")]
        public CustomCellListTableData presetsList;

        [UIValue("presets")]
        public List<object> presetsListContents = new List<object>();

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);

            presetsList.tableView.ClearSelection();
        }

        public void SetPresets(List<RoomPreset> presets)
        {
            presetsListContents.Clear();

            if (presets != null)
            {
                foreach (RoomPreset preset in presets)
                {
                    presetsListContents.Add(new PresetListObject(preset));
                }
            }

            if(presetsList != null)
                presetsList.tableView.ReloadData();
        }

        [UIAction("preset-selected")]
        private void RoomSelected(TableView sender, PresetListObject obj)
        {
            didFinishEvent?.Invoke(obj.preset);
        }

        [UIAction("back-btn-pressed")]
        private void BackBtnPressed()
        {
            didFinishEvent?.Invoke(null);
        }
    }

    public class PresetListObject
    {
        public RoomPreset preset;

        [UIValue("preset-name")]
        private string presetName;

        [UIValue("preset-room-name")]
        private string presetRoomName;

        [UIComponent("bg")]
        private RawImage background;

        [UIComponent("preset-room-name-text")]
        private TextMeshProUGUI presetRoomNameText;

        public PresetListObject(RoomPreset preset)
        {
            this.preset = preset;
            presetName = preset.GetName();
            presetRoomName = preset.settings.Name;
        }

        [UIAction("refresh-visuals")]
        public void Refresh(bool selected, bool highlighted)
        {
            background.texture = Sprites.whitePixel.texture;
            background.color = new Color(1f, 1f, 1f, 0.125f);
            presetRoomNameText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }
}
