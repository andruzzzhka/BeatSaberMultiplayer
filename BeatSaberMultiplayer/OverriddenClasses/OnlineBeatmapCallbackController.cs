using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class OnlineBeatmapCallbackController : BeatmapObjectCallbackController
    {
        public OnlinePlayerController owner;
        
        public void Init(OnlinePlayerController newOwner)
        {
            BeatmapObjectCallbackController original = FindObjectsOfType<BeatmapObjectCallbackController>().First(x => !(x is OnlineBeatmapCallbackController));

            foreach (FieldInfo info in original.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Default).Where(x => !x.Name.ToLower().Contains("event")))
            {
                info.SetValue(this, info.GetValue(original));
            }

            owner = newOwner;

            _beatmapObjectDataCallbackCacheList = new List<BeatmapObjectData>();
            _beatmapObjectCallbackData = new List<BeatmapObjectCallbackData>();

            _beatmapDataModel = new GameObject("CustomBeatmapDataModel").AddComponent<BeatmapDataModel>();
            if (BS_Utils.Plugin.LevelData.IsSet)
            {
                LevelOptionsInfo levelInfo = owner.PlayerInfo.playerLevelOptions;
                IDifficultyBeatmap diffBeatmap = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic.serializedName == owner.PlayerInfo.playerLevelOptions.characteristicName).difficultyBeatmaps.First(x => x.difficulty == owner.PlayerInfo.playerLevelOptions.difficulty);
                BeatmapData data = diffBeatmap.beatmapData;

                _beatmapDataModel.beatmapData = BeatDataTransformHelper.CreateTransformedBeatmapData(data, levelInfo.modifiers, PracticeSettings.defaultPracticeSettings, PlayerSpecificSettings.defaultSettings);
                HandleBeatmapDataModelDidChangeBeatmapData();

                Plugin.log.Info($"Set custom BeatmapDataModel for difficulty {levelInfo.difficulty}");
            }
        }

        public override void LateUpdate()
        {
            BeatmapData beatmapData = _beatmapDataModel.beatmapData;
            if (beatmapData == null || owner == null || owner.PlayerInfo == null)
            {
                return;
            }
            for (int i = 0; i < _beatmapObjectCallbackData.Count; i++)
            {
                _beatmapObjectDataCallbackCacheList.Clear();
                BeatmapObjectCallbackData beatmapObjectCallbackData = _beatmapObjectCallbackData[i];
                for (int j = 0; j < beatmapData.beatmapLinesData.Length; j++)
                {
                    while (beatmapObjectCallbackData.nextObjectIndexInLine[j] < beatmapData.beatmapLinesData[j].beatmapObjectsData.Length)
                    {
                        BeatmapObjectData beatmapObjectData = beatmapData.beatmapLinesData[j].beatmapObjectsData[beatmapObjectCallbackData.nextObjectIndexInLine[j]];
                        if (beatmapObjectData.time - beatmapObjectCallbackData.aheadTime >= owner.PlayerInfo.playerProgress)
                        {
                            break;
                        }
                        if (beatmapObjectData.time >= startSongTime)
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
            while (_nextEventIndex < beatmapData.beatmapEventData.Length)
            {
                BeatmapEventData beatmapEventData = beatmapData.beatmapEventData[_nextEventIndex];
                if (beatmapEventData.time >= owner.PlayerInfo.playerProgress)
                {
                    break;
                }
                SendBeatmapEventDidTriggerEvent(beatmapEventData);
                _nextEventIndex++;
            }
        }

    }
}
