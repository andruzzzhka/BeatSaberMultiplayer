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
        public OnlineBeatmapObjectManager onlineObjectManager;

        public void Init(OnlinePlayerController newOwner, OnlineBeatmapCallbackController callbackController, OnlineBeatmapObjectManager objectManager, OnlineAudioTimeController syncController)
        {
            BeatmapObjectSpawnController original = FindObjectsOfType<BeatmapObjectSpawnController>().First(x => !(x is OnlineBeatmapSpawnController));

            transform.position = original.transform.position;

            _initData = original.GetPrivateField<InitData>("_initData");
            _beatmapObjectCallbackController = original.GetPrivateField<BeatmapObjectCallbackController>("_beatmapObjectCallbackController");

            owner = newOwner;

            onlineObjectManager = objectManager;
            _beatmapObjectCallbackController = callbackController;
            onlineSyncController = syncController;
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

                    var movementSpeed = diffBeatmap.noteJumpMovementSpeed;

                    if (movementSpeed <= 0f)
                    {
                        movementSpeed = diffBeatmap.difficulty.NoteJumpMovementSpeed();
                    }

                    //TODO: Implement Fast Notes modifier in level options
                    /*
                    if (levelInfo.modifiers.ToGameplayModifiers().fastNotes)
                    {
                        movementSpeed = 20f;
                    }
                    */
                    _initData = new InitData(diffBeatmap.level.beatsPerMinute, diffBeatmap.beatmapData.beatmapLinesData.Length, movementSpeed, diffBeatmap.noteJumpStartBeatOffset, _disappearingArrows, _ghostNotes, _initData.jumpOffsetY);
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

        public override void SpawnNote(NoteData noteData, float cutDirectionAngleOffset)
        {
            if (_disableSpawning || owner == null)
            {
                return;
            }
            _beatmapObjectSpawnMovementData.GetNoteSpawnMovementData(noteData, out var moveStartPos, out var moveEndPos, out var jumpEndPos, out var jumpGravity);
            float moveDuration = _beatmapObjectSpawnMovementData.moveDuration;
            float jumpDuration = _beatmapObjectSpawnMovementData.jumpDuration;
            float rotation = _spawnRotationProcesser.rotation;
            if (noteData.noteType == NoteType.Bomb)
            {
                onlineObjectManager.SpawnBombNote(noteData, moveStartPos, moveEndPos, jumpEndPos, moveDuration, jumpDuration, jumpGravity, rotation);
                return;
            }
            if (noteData.noteType.IsBasicNote())
            {
                if (_firstBasicNoteTime == null)
                {
                    _firstBasicNoteTime = new float?(noteData.time);
                }
                float? firstBasicNoteTime = _firstBasicNoteTime;
                float time = noteData.time;
                onlineObjectManager.SpawnBasicNote(noteData, moveStartPos, moveEndPos, jumpEndPos, moveDuration, jumpDuration, jumpGravity, rotation, _disappearingArrows, _ghostNotes && !(firstBasicNoteTime.GetValueOrDefault() == time & firstBasicNoteTime != null), cutDirectionAngleOffset);
            }
        }

        public override void SpawnObstacle(ObstacleData obstacleData)
        {
            if (_disableSpawning)
            {
                return;
            }
            _beatmapObjectSpawnMovementData.GetObstacleSpawnMovementData(obstacleData, out var moveStartPos, out var moveEndPos, out var jumpEndPos, out var obstacleHeight);
            float moveDuration = _beatmapObjectSpawnMovementData.moveDuration;
            float jumpDuration = _beatmapObjectSpawnMovementData.jumpDuration;
            float noteLinesDistance = _beatmapObjectSpawnMovementData.noteLinesDistance;
            float rotation = _spawnRotationProcesser.rotation;
            onlineObjectManager.SpawnObstacle(obstacleData, moveStartPos, moveEndPos, jumpEndPos, moveDuration, jumpDuration, rotation, noteLinesDistance, obstacleHeight);
        }
    }
}
