using BeatSaberMultiplayer.Data;
using BS_Utils.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zenject;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class OnlineBeatmapSpawnController : BeatmapObjectSpawnController
    {
        public OnlinePlayerController owner;
        public OnlineAudioTimeController onlineSyncController;

        private PlayerController _localPlayer;
        private AudioTimeSyncController _localSyncController;

        public void Init(OnlinePlayerController newOwner, OnlineBeatmapCallbackController callbackController, OnlineBeatmapObjectManager objectManager, OnlineAudioTimeController syncController)
        {
            BeatmapObjectSpawnController original = FindObjectsOfType<BeatmapObjectSpawnController>().First(x => !(x is OnlineBeatmapSpawnController));

            transform.position = original.transform.position;

            foreach (FieldInfo info in original.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).Where(x => !x.Name.ToLower().Contains("event")))
            {
                info.SetValue(this, info.GetValue(original));
            }

            owner = newOwner;

            _beatmapObjectSpawner = objectManager;
            _beatmapObjectCallbackController = callbackController;
            onlineSyncController = syncController;

            _localPlayer = FindObjectsOfType<PlayerController>().First(x => !(x is OnlinePlayerController));
            _localSyncController = FindObjectsOfType<AudioTimeSyncController>().First(x => !(x is OnlineAudioTimeController));
        }

        public override void Start()
        {
            try
            {
                if (BS_Utils.Plugin.LevelData.IsSet)
                {
                    LevelOptionsInfo levelInfo = owner.playerInfo.updateInfo.playerLevelOptions;
                    IDifficultyBeatmap diffBeatmap = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic.serializedName == owner.playerInfo.updateInfo.playerLevelOptions.characteristicName).difficultyBeatmaps.First(x => x.difficulty == owner.playerInfo.updateInfo.playerLevelOptions.difficulty);

                    _disappearingArrows = levelInfo.modifiers.disappearingArrows;
                    _ghostNotes = levelInfo.modifiers.ghostNotes;

                    _initData = new InitData(diffBeatmap.level.beatsPerMinute, diffBeatmap.beatmapData.beatmapLinesData.Length, diffBeatmap.noteJumpMovementSpeed, diffBeatmap.noteJumpStartBeatOffset, _disappearingArrows, _ghostNotes, _initData.jumpOffsetY);
                }
            }
            catch (Exception e)
            {
                Plugin.log.Warn("Unable to update beatmap data! Exception: " + e);
            }

            _variableBPMProcessor.SetBPM(_initData.beatsPerMinute);
            _beatmapObjectSpawnMovementData.Init(_initData.noteLinesCount, _initData.noteJumpMovementSpeed, _initData.beatsPerMinute, _initData.noteJumpStartBeatOffset, _initData.jumpOffsetY, transform.position + transform.right * owner.avatarOffset, transform.right, transform.forward);
            _beatmapCallbackItemDataList = new BeatmapCallbackItemDataList(new BeatmapCallbackItemDataList.SpawnNoteCallback(SpawnNote), new BeatmapCallbackItemDataList.SpawnObstacleCallback(SpawnObstacle), new BeatmapCallbackItemDataList.ProcessBeatmapEventCallback(ProcessEarlyBeatmapEventData), new BeatmapCallbackItemDataList.ProcessBeatmapEventCallback(ProcessLateBeatmapEventData), new Action(EarlyEventsWereProcessed), new BeatmapCallbackItemDataList.GetRelativeNoteOffsetCallback(_beatmapObjectSpawnMovementData.Get2DNoteOffset));
            _jumpOffsetY = _initData.jumpOffsetY;
            _disappearingArrows = _initData.disappearingArrows;
            _ghostNotes = _initData.ghostNotes;
            if (_beatmapObjectCallbackData != null)
            {
                _beatmapObjectCallbackController.RemoveBeatmapObjectCallback(_beatmapObjectCallbackData);
            }
            _beatmapObjectCallbackData = _beatmapObjectCallbackController.AddBeatmapObjectCallback(new BeatmapObjectCallbackController.BeatmapObjectCallback(HandleBeatmapObjectCallback), _beatmapObjectSpawnMovementData.spawnAheadTime);
            if (_beatmapEventCallbackData != null)
            {
                _beatmapObjectCallbackController.RemoveBeatmapEventCallback(_beatmapEventCallbackData);
            }
            _beatmapEventCallbackData = _beatmapObjectCallbackController.AddBeatmapEventCallback(new BeatmapObjectCallbackController.BeatmapEventCallback(HandleBeatmapEventCallback), _beatmapObjectSpawnMovementData.spawnAheadTime);
            _beatmapObjectCallbackController.callbacksForThisFrameWereProcessedEvent += HandleCallbacksForThisFrameWereProcessed;
        }
        
    }
}
