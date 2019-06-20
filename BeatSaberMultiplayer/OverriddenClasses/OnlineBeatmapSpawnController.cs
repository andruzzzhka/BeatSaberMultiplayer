using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

        private List<NoteController> _activeNotes = new List<NoteController>();
        private List<ObstacleController> _activeObstacles = new List<ObstacleController>();

        public void Init(OnlinePlayerController newOwner, OnlineBeatmapCallbackController callbackController, OnlineAudioTimeController syncController)
        {
            BeatmapObjectSpawnController original = FindObjectsOfType<BeatmapObjectSpawnController>().First(x => !(x is OnlineBeatmapSpawnController));

            foreach (FieldInfo info in original.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).Where(x => !x.Name.ToLower().Contains("event")))
            {
                info.SetValue(this, info.GetValue(original));
            }

            owner = newOwner;


            try
            {
                if (BS_Utils.Plugin.LevelData.IsSet)
                {
                    LevelOptionsInfo levelInfo = owner.PlayerInfo.playerLevelOptions;
                    IDifficultyBeatmap diffBeatmap = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level.beatmapLevelData.difficultyBeatmapSets.First(x => x.beatmapCharacteristic.serializedName == owner.PlayerInfo.playerLevelOptions.characteristicName).difficultyBeatmaps.First(x => x.difficulty == owner.PlayerInfo.playerLevelOptions.difficulty);
                    
                    _beatsPerMinute = diffBeatmap.level.beatsPerMinute;
                    _noteLinesCount = (float)diffBeatmap.beatmapData.beatmapLinesData.Length;
                    _noteJumpMovementSpeed = diffBeatmap.noteJumpMovementSpeed;
                    _disappearingArrows = levelInfo.modifiers.disappearingArrows;
                    _ghostNotes = levelInfo.modifiers.ghostNotes;
                    float num = 60f / _beatsPerMinute;
                    _moveDistance = _moveSpeed * num * _moveDurationInBeats;
                    while (_noteJumpMovementSpeed * num * _halfJumpDurationInBeats > _maxHalfJumpDistance)
                    {
                        _halfJumpDurationInBeats /= 2f;
                    }
                    _halfJumpDurationInBeats += diffBeatmap.noteJumpStartBeatOffset;
                    if (_halfJumpDurationInBeats < 1f)
                    {
                        _halfJumpDurationInBeats = 1f;
                    }
                    _jumpDistance = _noteJumpMovementSpeed * num * _halfJumpDurationInBeats * 2f;
                    _spawnAheadTime = _moveDistance / _moveSpeed + _jumpDistance * 0.5f / _noteJumpMovementSpeed;
                }
            }catch(Exception e)
            {
                Plugin.log.Warn("Unable to update beatmap data! Exception: "+e);
            }

            _beatmapObjectCallbackController = callbackController;
            onlineSyncController = syncController;

            if (_beatmapObjectCallbackId != -1)
            {
                _beatmapObjectCallbackController.RemoveBeatmapObjectCallback(_beatmapObjectCallbackId);
            }

            if (_beatmapObjectCallbackController != null)
            {
                _beatmapObjectCallbackId = _beatmapObjectCallbackController.AddBeatmapObjectCallback(new BeatmapObjectCallbackController.BeatmapObjectCallback(BeatmapObjectSpawnCallback), _spawnAheadTime);
            }

            _localPlayer = FindObjectsOfType<PlayerController>().First(x => !(x is OnlinePlayerController));
            _localSyncController = FindObjectsOfType<AudioTimeSyncController>().First(x => !(x is OnlineAudioTimeController));

            NoteCutEffectSpawner cutEffectSpawner = FindObjectOfType<NoteCutEffectSpawner>();

            (this as BeatmapObjectSpawnController).noteWasCutEvent += (sender, controller, cutInfo) => { if(cutInfo.allIsOK) cutEffectSpawner.HandleNoteWasCutEvent(sender, controller, cutInfo); };

            _activeNotes = new List<NoteController>();
            _activeObstacles = new List<ObstacleController>();
        }

        public override void BeatmapObjectSpawnCallback(BeatmapObjectData beatmapObjectData)
        {
            if (_disableSpawning)
            {
                return;
            }
            float num = _moveDistance / _moveSpeed;
            float num2 = _jumpDistance / _noteJumpMovementSpeed;
            if (beatmapObjectData.beatmapObjectType == BeatmapObjectType.Obstacle)
            {
                ObstacleData obstacleData = (ObstacleData)beatmapObjectData;
                Vector3 forward = transform.forward;
                Vector3 a = transform.position;
                a += forward * (_moveDistance + _jumpDistance * 0.5f);
                Vector3 a2 = a - forward * _moveDistance;
                Vector3 a3 = a - forward * (_moveDistance + _jumpDistance);
                Vector3 noteOffset = GetNoteOffset(beatmapObjectData.lineIndex, NoteLineLayer.Base);
                noteOffset.y = ((obstacleData.obstacleType == ObstacleType.Top) ? (_topObstaclePosY + _globalYJumpOffset) : _verticalObstaclePosY);
                ObstacleController obstacleController = ((obstacleData.obstacleType == ObstacleType.Top) ? _topObstaclePool : _fullHeightObstaclePool).Spawn();
                SetObstacleEventCallbacks(obstacleController);
                obstacleController.transform.SetPositionAndRotation(a + noteOffset, Quaternion.identity);
                obstacleController.Init(obstacleData, a + noteOffset, a2 + noteOffset, a3 + noteOffset, num, num2, beatmapObjectData.time - _spawnAheadTime, _noteLinesDistance);

                obstacleController.SetPrivateField("_playerController", owner);
                obstacleController.SetPrivateField("_audioTimeSyncController", onlineSyncController);
                obstacleController.finishedMovementEvent += ResetControllers;
                obstacleController.didDissolveEvent += ResetControllers;
                _activeObstacles.Add(obstacleController);

                this.GetPrivateField<Action<BeatmapObjectSpawnController, ObstacleController>>("obstacleDiStartMovementEvent")?.Invoke(this, obstacleController);
            }
            else
            {
                NoteData noteData = (NoteData)beatmapObjectData;
                Vector3 forward2 = transform.forward;
                Vector3 a4 = transform.position;
                a4 += forward2 * (_moveDistance + _jumpDistance * 0.5f);
                Vector3 a5 = a4 - forward2 * _moveDistance;
                Vector3 a6 = a4 - forward2 * (_moveDistance + _jumpDistance);
                if (noteData.noteLineLayer == NoteLineLayer.Top)
                {
                    a6 += forward2 * _topLinesZPosOffset * 2f;
                }
                Vector3 noteOffset2 = GetNoteOffset(noteData.lineIndex, noteData.startNoteLineLayer);
                float jumpGravity = JumpGravityForLineLayer(noteData.noteLineLayer, noteData.startNoteLineLayer);
                if (noteData.noteType == NoteType.Bomb)
                {
                    NoteController noteController = _bombNotePool.Spawn();
                    SetNoteControllerEventCallbacks(noteController);
                    noteController.transform.SetPositionAndRotation(a4 + noteOffset2, Quaternion.identity);
                    noteController.Init(noteData, a4 + noteOffset2, a5 + noteOffset2, a6 + noteOffset2, num, num2, noteData.time - _spawnAheadTime, jumpGravity);

                    var noteJump = noteController.GetComponent<NoteJump>();
                    noteJump.SetPrivateField("_playerController", owner);
                    noteJump.SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    noteController.GetComponent<NoteFloorMovement>().SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    noteController.noteDidFinishJumpEvent += ResetControllers;
                    noteController.noteWasCutEvent += ResetControllersNoteWasCut;
                    noteController.noteDidDissolveEvent += ResetControllers;
                    _activeNotes.Add(noteController);
                }
                else if (noteData.noteType.IsBasicNote())
                {
                    MemoryPool<NoteController> memoryPool = (noteData.noteType == NoteType.NoteA) ? _noteAPool : _noteBPool;
                    if (_numberOfSpawnedBasicNotes == 0)
                    {
                        _firstBasicNoteTime = noteData.time;
                    }
                    bool flag = _firstBasicNoteTime == noteData.time;
                    NoteController noteController2 = memoryPool.Spawn();
                    SetNoteControllerEventCallbacks(noteController2);
                    Vector3 noteOffset3 = GetNoteOffset(noteData.flipLineIndex, noteData.startNoteLineLayer);
                    noteController2.transform.SetPositionAndRotation(a4 + noteOffset3, Quaternion.identity);
                    GameNoteController gameNoteController = noteController2 as GameNoteController;
                    if (gameNoteController != null)
                    {
                        gameNoteController.Init(noteData, a4 + noteOffset3, a5 + noteOffset3, a6 + noteOffset2, num, num2, noteData.time - _spawnAheadTime, jumpGravity, _disappearingArrows, _ghostNotes && !flag);
                    }
                    else
                    {
                        noteController2.Init(noteData, a4 + noteOffset3, a5 + noteOffset3, a6 + noteOffset2, num, num2, noteData.time - _spawnAheadTime, jumpGravity);
                    }

                    var noteJump = noteController2.GetComponent<NoteJump>();
                    noteJump.SetPrivateField("_playerController", owner);
                    noteJump.SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    noteController2.GetComponent<NoteFloorMovement>().SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    noteController2.noteDidFinishJumpEvent += ResetControllers;
                    noteController2.noteWasCutEvent += ResetControllersNoteWasCut;
                    noteController2.noteDidDissolveEvent += ResetControllers;

                    _activeNotes.Add(noteController2);
                    _numberOfSpawnedBasicNotes++;
                    if (_prevSpawnedNormalNoteController != null)
                    {
                        float time = _prevSpawnedNormalNoteController.noteData.time;
                        float time2 = noteController2.noteData.time;
                    }
                    _prevSpawnedNormalNoteController = noteController2;
                }
            }
            this.GetPrivateField<Action<BeatmapObjectSpawnController, BeatmapObjectData, float, float>>("beatmapObjectWasSpawnedEvent")?.Invoke(this, beatmapObjectData, num, num2);
        }

        public override Vector3 GetNoteOffset(int noteLineIndex, NoteLineLayer noteLineLayer)
        {
            float num = -(_noteLinesCount - 1f) * 0.5f;
            num = (num + noteLineIndex) * _noteLinesDistance;
            if (owner != null)
                num += owner.avatarOffset;
            return transform.right * num + new Vector3(0f, LineYPosForLineLayer(noteLineLayer), 0f);
        }

        private void ResetControllersNoteWasCut(NoteController controller, NoteCutInfo info)
        {
            ResetControllers(controller);
        }

        public void ResetControllers(NoteController noteController)
        {
            var noteJump = noteController.GetComponent<NoteJump>();
            noteJump.SetPrivateField("_playerController", _localPlayer);
            noteJump.SetPrivateField("_audioTimeSyncController", _localSyncController);
            noteController.GetComponent<NoteFloorMovement>().SetPrivateField("_audioTimeSyncController", _localSyncController);

            noteController.noteDidFinishJumpEvent -= ResetControllers;
            noteController.noteWasCutEvent -= ResetControllersNoteWasCut;
            noteController.noteDidDissolveEvent -= ResetControllers;

            if (_activeNotes != null)
                _activeNotes.Remove(noteController);
        }

        public void ResetControllers(ObstacleController controller)
        {
            controller.SetPrivateField("_playerController", _localPlayer);
            controller.SetPrivateField("_audioTimeSyncController", _localSyncController);
            controller.finishedMovementEvent -= ResetControllers;
            controller.didDissolveEvent -= ResetControllers;
            if (_activeObstacles != null)
                _activeObstacles.Remove(controller);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            Plugin.log.Info("Spawn controller is destroyed! Despawning notes...");

            _disableSpawning = true;

            for (int i = 0; i<  _noteAPool.activeItems.Count; i++)
            {
                if(_noteAPool.activeItems.Count > i)
                    Despawn(_noteAPool.activeItems.ElementAt(i));
            }
            for (int i = 0; i < _noteBPool.activeItems.Count; i++)
            {
                if (_noteBPool.activeItems.Count > i)
                    Despawn(_noteBPool.activeItems.ElementAt(i));
            }
            for (int i = 0; i < _bombNotePool.activeItems.Count; i++)
            {
                if (_bombNotePool.activeItems.Count > i)
                    Despawn(((BombNoteController)_bombNotePool.activeItems.ElementAt(i)));
            }
            for (int i = 0; i < _fullHeightObstaclePool.activeItems.Count; i++)
            {
                if (_fullHeightObstaclePool.activeItems.Count > i)
                    Despawn(_fullHeightObstaclePool.activeItems.ElementAt(i));
            }
            for (int i = 0; i < _topObstaclePool.activeItems.Count; i++)
            {
                if (_topObstaclePool.activeItems.Count > i)
                    Despawn(_topObstaclePool.activeItems.ElementAt(i));
            }
        }
    }
}
