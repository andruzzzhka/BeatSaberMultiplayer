using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMarkupLanguage.Components;
using System;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using UnityEngine;

namespace BeatSaberMultiplayer.UI.ViewControllers.RoomScreen
{
    class LevelPacksUIViewController : BSMLResourceViewController
    {
        public override string ResourceName => string.Join(".", GetType().Namespace, GetType().Name);

        public event Action<IAnnotatedBeatmapLevelCollection> packSelected; 

        [UIComponent("packs-list-table")]
        CustomListTableData levelPacksTableData;

        //[UIComponent("packs-collections-control")]
        //TextSegmentedControl packsCollectionsControl;

		private bool _initialized;
		private BeatmapLevelsModel _beatmapLevelsModel;
		private IAnnotatedBeatmapLevelCollection[] _visiblePacks;

		protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            base.DidActivate(firstActivation, type);

            //packsCollectionsControl.SetTexts(new string[] { "OST & EXTRAS", "MUSIC PACKS", "PLAYLISTS", "CUSTOM LEVELS" });

			Initialize();

		}

        public void Initialize()
        {
			if (_initialized)
			{
				return;
			}

			if (_beatmapLevelsModel == null)
				_beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().First();

			_visiblePacks = _beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks;

			SetPacks(_visiblePacks);

			if(_visiblePacks.Length > 0)
				packSelected?.Invoke(_visiblePacks[0]);

			this._initialized = true;
		}

		public void SetPacks(IAnnotatedBeatmapLevelCollection[] packs)
		{
			levelPacksTableData.data.Clear();

			foreach (var pack in packs)
			{
				levelPacksTableData.data.Add(new CustomListTableData.CustomCellInfo(pack.collectionName, $"{pack.beatmapLevelCollection.beatmapLevels.Length} levels", pack.coverImage.texture));
			}

			levelPacksTableData.tableView.ReloadData();
		}

		[UIAction("pack-selected")]
		public void PackSelected(TableView sender, int index)
		{
			packSelected?.Invoke(_visiblePacks[index]);
		}
	}
}
