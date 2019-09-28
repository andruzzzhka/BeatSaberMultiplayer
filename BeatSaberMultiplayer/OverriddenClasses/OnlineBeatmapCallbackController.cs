using BeatSaberMultiplayer.Data;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

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

            _beatmapDataModel = new GameObject("CustomBeatmapDataModel").AddComponent<BeatmapDataModel>();
            if (BS_Utils.Plugin.LevelData.IsSet)
            {
                Plugin.log.Debug($"Level data is set, trying to match BeatmapDatamodel for selected difficulty...");

                LevelOptionsInfo levelInfo = owner.playerInfo.updateInfo.playerLevelOptions;
                IDifficultyBeatmap diffBeatmap = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic.serializedName == owner.playerInfo.updateInfo.playerLevelOptions.characteristicName).difficultyBeatmaps.First(x => x.difficulty == owner.playerInfo.updateInfo.playerLevelOptions.difficulty);
                BeatmapData data = diffBeatmap.beatmapData;

                _beatmapDataModel.beatmapData = BeatDataTransformHelper.CreateTransformedBeatmapData(data, levelInfo.modifiers.ToGameplayModifiers(), PracticeSettings.defaultPracticeSettings, PlayerSpecificSettings.defaultSettings);
                HandleBeatmapDataModelDidChangeBeatmapData();

                Plugin.log.Debug($"Set custom BeatmapDataModel for difficulty {levelInfo.difficulty}");
            }
        }

        public override void LateUpdate()
        {
            BeatmapData beatmapData = _beatmapDataModel.beatmapData;
            if (beatmapData == null || owner == null || owner.playerInfo == null)
            {
                return;
            }
            for (int i = 0; i < _beatmapEarlyEventCallbackData.Count; i++)
            {
                BeatmapEventCallbackData beatmapEventCallbackData = _beatmapEarlyEventCallbackData[i];
                while (beatmapEventCallbackData.nextEventIndex < beatmapData.beatmapEventData.Length)
                {
                    BeatmapEventData beatmapEventData = beatmapData.beatmapEventData[beatmapEventCallbackData.nextEventIndex];
                    if (beatmapEventData.time - beatmapEventCallbackData.aheadTime >= owner.playerInfo.updateInfo.playerProgress)
                    {
                        break;
                    }
                    beatmapEventCallbackData.callback(beatmapEventData);
                    beatmapEventCallbackData.nextEventIndex++;
                }
            }
            for (int j = 0; j < _beatmapObjectCallbackData.Count; j++)
            {
                _beatmapObjectDataCallbackCacheList.Clear();
                BeatmapObjectCallbackData beatmapObjectCallbackData = _beatmapObjectCallbackData[j];
                for (int k = 0; k < beatmapData.beatmapLinesData.Length; k++)
                {
                    while (beatmapObjectCallbackData.nextObjectIndexInLine[k] < beatmapData.beatmapLinesData[k].beatmapObjectsData.Length)
                    {
                        BeatmapObjectData beatmapObjectData = beatmapData.beatmapLinesData[k].beatmapObjectsData[beatmapObjectCallbackData.nextObjectIndexInLine[k]];
                        if (beatmapObjectData.time - beatmapObjectCallbackData.aheadTime >= owner.playerInfo.updateInfo.playerProgress)
                        {
                            break;
                        }
                        if (beatmapObjectData.time >= _spawningStartTime)
                        {
                            for (int l = _beatmapObjectDataCallbackCacheList.Count; l >= 0; l--)
                            {
                                if (l == 0 || _beatmapObjectDataCallbackCacheList[l - 1].time <= beatmapObjectData.time)
                                {
                                    _beatmapObjectDataCallbackCacheList.Insert(l, beatmapObjectData);
                                    break;
                                }
                            }
                        }
                        beatmapObjectCallbackData.nextObjectIndexInLine[k]++;
                    }
                }
                foreach (BeatmapObjectData noteData in _beatmapObjectDataCallbackCacheList)
                {
                    beatmapObjectCallbackData.callback(noteData);
                }
            }
            for (int m = 0; m < _beatmapLateEventCallbackData.Count; m++)
            {
                BeatmapEventCallbackData beatmapEventCallbackData2 = _beatmapLateEventCallbackData[m];
                while (beatmapEventCallbackData2.nextEventIndex < beatmapData.beatmapEventData.Length)
                {
                    BeatmapEventData beatmapEventData2 = beatmapData.beatmapEventData[beatmapEventCallbackData2.nextEventIndex];
                    if (beatmapEventData2.time - beatmapEventCallbackData2.aheadTime >= owner.playerInfo.updateInfo.playerProgress)
                    {
                        break;
                    }
                    beatmapEventCallbackData2.callback(beatmapEventData2);
                    beatmapEventCallbackData2.nextEventIndex++;
                }
            }
            while (_nextEventIndex < beatmapData.beatmapEventData.Length)
            {
                BeatmapEventData beatmapEventData3 = beatmapData.beatmapEventData[_nextEventIndex];
                if (beatmapEventData3.time >= owner.playerInfo.updateInfo.playerProgress)
                {
                    break;
                }
                SendBeatmapEventDidTriggerEvent(beatmapEventData3);
                _nextEventIndex++;
            }
        }
    }
}
