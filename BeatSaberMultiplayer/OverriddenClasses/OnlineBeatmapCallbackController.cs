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
        
        public void Init(OnlinePlayerController newOwner, OnlineAudioTimeController onlineSyncController)
        {
            BeatmapObjectCallbackController original = FindObjectsOfType<BeatmapObjectCallbackController>().First(x => !(x is OnlineBeatmapCallbackController));

			transform.position = original.transform.position;

            owner = newOwner;

            _audioTimeSource = onlineSyncController;

            _initData = original.GetPrivateField<InitData>("_initData");
            _beatmapData = original.GetPrivateField<BeatmapData>("_beatmapData");
        }

        public override void Start()
        {
            _spawningStartTime = _initData.spawningStartTime;
            if (BS_Utils.Plugin.LevelData.IsSet)
            {
                Plugin.log.Debug($"Level data is set, trying to match BeatmapDatamodel for selected difficulty...");

                LevelOptionsInfo levelInfo = owner.playerInfo.updateInfo.playerLevelOptions;
                IDifficultyBeatmap diffBeatmap = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic.serializedName == owner.playerInfo.updateInfo.playerLevelOptions.characteristicName).difficultyBeatmaps.First(x => x.difficulty == owner.playerInfo.updateInfo.playerLevelOptions.difficulty);
                BeatmapData data = diffBeatmap.beatmapData;

                SetNewBeatmapData(BeatDataTransformHelper.CreateTransformedBeatmapData(data, levelInfo.modifiers.ToGameplayModifiers(), PracticeSettings.defaultPracticeSettings, PlayerSpecificSettings.defaultSettings));

                Plugin.log.Debug($"Set custom BeatmapDataModel for difficulty {levelInfo.difficulty}");
            }
            else
            {
                Plugin.log.Warn($"Level data is not set! Unable to set BeatmapDataModel for other players!");
                SetNewBeatmapData(_initData.beatmapData);
            }
        }
    }
}
