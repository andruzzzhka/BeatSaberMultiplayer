using BeatSaberMultiplayer.Data;
using BS_Utils.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class OnlineBeatmapCallbackController : BeatmapObjectCallbackController
    {
        public OnlinePlayerController owner;
        
        public void Init(OnlinePlayerController newOwner)
        {
            BeatmapObjectCallbackController original = FindObjectsOfType<BeatmapObjectCallbackController>().First(x => !(x is OnlineBeatmapCallbackController));

            foreach (FieldInfo info in original.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).Where(x => !x.Name.ToLower().Contains("event")))
            {
                info.SetValue(this, info.GetValue(original));
            }

            owner = newOwner;

            _beatmapObjectDataCallbackCacheList = new List<BeatmapObjectData>();
            _beatmapObjectCallbackData = new List<BeatmapObjectCallbackData>();

            if (BS_Utils.Plugin.LevelData.IsSet)
            {
                Plugin.log.Debug($"Level data is set, trying to match BeatmapDatamodel for selected difficulty...");

                LevelOptionsInfo levelInfo = owner.playerInfo.updateInfo.playerLevelOptions;
                IDifficultyBeatmap diffBeatmap = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic.serializedName == owner.playerInfo.updateInfo.playerLevelOptions.characteristicName).difficultyBeatmaps.First(x => x.difficulty == owner.playerInfo.updateInfo.playerLevelOptions.difficulty);
                BeatmapData data = diffBeatmap.beatmapData;

                SetNewBeatmapData(BeatDataTransformHelper.CreateTransformedBeatmapData(data, levelInfo.modifiers.ToGameplayModifiers(), PracticeSettings.defaultPracticeSettings, PlayerSpecificSettings.defaultSettings));

                Plugin.log.Debug($"Set custom BeatmapDataModel for difficulty {levelInfo.difficulty}");
            }
        }

        public override void LateUpdate()
        {
            if (_beatmapData == null || owner == null || owner.playerInfo == null)
            {
                return;
            }
			if (_firstLateUpdate)
			{
				_firstLateUpdate = false;
				return;
			}
			if (_beatmapData == null)
			{
				return;
			}
			for (int i = 0; i < _beatmapObjectCallbackData.Count; i++)
			{
				_beatmapObjectDataCallbackCacheList.Clear();
				BeatmapObjectCallbackData beatmapObjectCallbackData = _beatmapObjectCallbackData[i];
				for (int j = 0; j < _beatmapData.beatmapLinesData.Length; j++)
				{
					while (beatmapObjectCallbackData.nextObjectIndexInLine[j] < _beatmapData.beatmapLinesData[j].beatmapObjectsData.Length)
					{
						BeatmapObjectData beatmapObjectData = _beatmapData.beatmapLinesData[j].beatmapObjectsData[beatmapObjectCallbackData.nextObjectIndexInLine[j]];
						if (beatmapObjectData.time - beatmapObjectCallbackData.aheadTime >= owner.playerInfo.updateInfo.playerProgress)
						{
							break;
						}
						if (beatmapObjectData.time >= _spawningStartTime)
						{
							for (int k = _beatmapObjectDataCallbackCacheList.Count; k >= 0; k--)
							{
								if (k == 0 || _beatmapObjectDataCallbackCacheList[k - 1].time <= beatmapObjectData.time)
								{
									_beatmapObjectDataCallbackCacheList.Insert(k, beatmapObjectData);
									break;
								}
							}
						}
						beatmapObjectCallbackData.nextObjectIndexInLine[j]++;
					}
				}
				foreach (BeatmapObjectData noteData in _beatmapObjectDataCallbackCacheList)
				{
					beatmapObjectCallbackData.callback(noteData);
				}
			}
			for (int l = 0; l < _beatmapEventCallbackData.Count; l++)
			{
				BeatmapEventCallbackData beatmapEventCallbackData = _beatmapEventCallbackData[l];
				while (beatmapEventCallbackData.nextEventIndex < _beatmapData.beatmapEventData.Length)
				{
					BeatmapEventData beatmapEventData = _beatmapData.beatmapEventData[beatmapEventCallbackData.nextEventIndex];
					if (beatmapEventData.time - beatmapEventCallbackData.aheadTime >= owner.playerInfo.updateInfo.playerProgress)
					{
						break;
					}
					beatmapEventCallbackData.callback(beatmapEventData);
					beatmapEventCallbackData.nextEventIndex++;
				}
			}
			while (_nextEventIndex < _beatmapData.beatmapEventData.Length)
			{
				BeatmapEventData beatmapEventData2 = _beatmapData.beatmapEventData[_nextEventIndex];
				if (beatmapEventData2.time >= owner.playerInfo.updateInfo.playerProgress)
				{
					break;
				}
				SendBeatmapEventDidTriggerEvent(beatmapEventData2);
				_nextEventIndex++;
			}
			this.GetPrivateField<Action>("callbacksForThisFrameWereProcessedEvent")?.Invoke();
		}
    }
}
