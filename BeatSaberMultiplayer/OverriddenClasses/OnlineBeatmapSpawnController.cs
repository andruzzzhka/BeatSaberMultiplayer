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
    public class OnlineBeatmapSpawnController : BeatmapObjectSpawnController
    {
        public OnlinePlayerController owner;
        public OnlineBeatmapCallbackController onlineCallbackController;
        public OnlineAudioTimeController onlineSyncController;

        private PlayerController _localPlayer;
        private AudioTimeSyncController _localSyncController;

        private List<NoteController> _activeNotes = new List<NoteController>();
        private List<ObstacleController> _activeObstacles = new List<ObstacleController>();

        public void Init(OnlinePlayerController newOwner, OnlineBeatmapCallbackController callbackController, OnlineAudioTimeController syncController)
        {
            BeatmapObjectSpawnController original = FindObjectsOfType<BeatmapObjectSpawnController>().First(x => !x.name.StartsWith("Online"));

            foreach (FieldInfo info in original.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Default).Where(x => !x.Name.ToLower().Contains("event")))
            {
                info.SetValue(this, info.GetValue(original));
            }

            owner = newOwner;

            onlineCallbackController = callbackController;
            _beatmapObjectCallbackController = onlineCallbackController;
            onlineSyncController = syncController;

            if (onlineCallbackController != null)
            {
                _noteSpawnCallbackId = onlineCallbackController.AddBeatmapObjectCallback(new BeatmapObjectCallbackController.BeatmapObjectCallback(BeatmapObjectSpawnCallback), _spawnAheadTime);
            }

            _localPlayer = FindObjectsOfType<PlayerController>().First(x => !x.name.Contains("Online"));
            _localSyncController = FindObjectsOfType<AudioTimeSyncController>().First(x => !x.name.Contains("Online"));

            //(this as BeatmapObjectSpawnController).noteWasMissedEvent += FindObjectOfType<MissedNoteEffectSpawner>().HandleNoteWasMissed;
            (this as BeatmapObjectSpawnController).noteWasCutEvent += FindObjectOfType<NoteCutSoundEffectManager>().HandleNoteWasCut;
            (this as BeatmapObjectSpawnController).noteWasCutEvent += FindObjectOfType<BombCutSoundEffectManager>().HandleNoteWasCut;
            (this as BeatmapObjectSpawnController).noteWasCutEvent += FindObjectOfType<NoteCutEffectSpawner>().HandleNoteWasCutEvent;

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
                noteOffset.y = ((obstacleData.obstacleType != ObstacleType.Top) ? _verticalObstaclePosY : (_topObstaclePosY + _globalYJumpOffset));
                ObstacleController.Pool pool = (obstacleData.obstacleType != ObstacleType.Top) ? _fullHeightObstaclePool : _topObstaclePool;
                ObstacleController obstacleController = pool.Spawn();
                SetObstacleEventCallbacks(obstacleController);
                obstacleController.transform.SetPositionAndRotation(a + noteOffset, Quaternion.identity);
                obstacleController.Init(obstacleData, a + noteOffset, a2 + noteOffset, a3 + noteOffset, num, num2, beatmapObjectData.time - _spawnAheadTime, _noteLinesDistance);
                obstacleController.SetPrivateField("_playerController", owner);
                obstacleController.SetPrivateField("_audioTimeSyncController", onlineSyncController);
                obstacleController.finishedMovementEvent += ResetControllers;
                obstacleController.didDissolveEvent += ResetControllers;
                _activeObstacles.Add(obstacleController);
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
                    NoteController bombNoteController = _bombNotePool.Spawn();
                    SetNoteControllerEventCallbacks(bombNoteController);
                    bombNoteController.transform.SetPositionAndRotation(a4 + noteOffset2, Quaternion.identity);
                    bombNoteController.Init(noteData, a4 + noteOffset2, a5 + noteOffset2, a6 + noteOffset2, num, num2, noteData.time - _spawnAheadTime, jumpGravity);
                    var noteJump = bombNoteController.GetComponent<NoteJump>();
                    noteJump.SetPrivateField("_playerController", owner);
                    noteJump.SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    bombNoteController.GetComponent<NoteFloorMovement>().SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    bombNoteController.noteDidFinishJumpEvent += ResetControllers;
                    bombNoteController.noteWasCutEvent += ResetControllersNoteWasCut;
                    bombNoteController.noteDidDissolveEvent += ResetControllers;
                    _activeNotes.Add(bombNoteController);
                }
                else if (noteData.noteType.IsBasicNote())
                {
                    NoteController.Pool pool2 = (noteData.noteType != NoteType.NoteA) ? _noteBPool : _noteAPool;
                    NoteController basicNoteController = pool2.Spawn();
                    SetNoteControllerEventCallbacks(basicNoteController);
                    Vector3 noteOffset3 = GetNoteOffset(noteData.flipLineIndex, noteData.startNoteLineLayer);
                    basicNoteController.transform.SetPositionAndRotation(a4 + noteOffset3, Quaternion.identity);
                    GameNoteController gameNoteController = basicNoteController as GameNoteController;
                    if (gameNoteController != null)
                    {
                        gameNoteController.Init(noteData, a4 + noteOffset3, a5 + noteOffset3, a6 + noteOffset2, num, num2, noteData.time - _spawnAheadTime, jumpGravity, _disappearingArrows, _ghostNotes);
                    }
                    else
                    {
                        basicNoteController.Init(noteData, a4 + noteOffset3, a5 + noteOffset3, a6 + noteOffset2, num, num2, noteData.time - _spawnAheadTime, jumpGravity);
                    }
                    var noteJump = basicNoteController.GetComponent<NoteJump>();
                    noteJump.SetPrivateField("_playerController", owner);
                    noteJump.SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    basicNoteController.GetComponent<NoteFloorMovement>().SetPrivateField("_audioTimeSyncController", onlineSyncController);
                    basicNoteController.noteDidFinishJumpEvent += ResetControllers;
                    basicNoteController.noteWasCutEvent += ResetControllersNoteWasCut;
                    basicNoteController.noteDidDissolveEvent += ResetControllers;
                    _prevSpawnedNormalNoteController = basicNoteController;
                    _activeNotes.Add(basicNoteController);
                }
            }
        }

        public override Vector3 GetNoteOffset(int noteLineIndex, NoteLineLayer noteLineLayer)
        {
            float num = -(_noteLinesCount - 1f) * 0.5f;
            num = (num + noteLineIndex) * _noteLinesDistance;
            if (owner != null)
                num += owner.avatarOffset;
            return transform.right * num + new Vector3(0f, LineYPosForLineLayer(noteLineLayer), 0f);
        }

        public void Update()
        {
            if(owner != null && owner.PlayerInfo != null && owner.PlayerInfo.hitsLastUpdate != null)
            {
                foreach(HitData hit in owner.PlayerInfo.hitsLastUpdate)
                {
                    NoteController controller = _activeNotes.FirstOrDefault(x => Mathf.Approximately(x.noteData.time, hit.objectTime));

                    if(controller != null)
                    {
                        if (hit.noteWasCut)
                        {
                            controller.InvokePrivateMethod("SendNoteWasCutEvent", new object[] { hit.GetCutInfo() });
                        }
                        else
                        {
                            controller.InvokePrivateMethod("HandleNoteDidPassMissedMarkerEvent", new object[0]);
                        }
                    }
                }
            }
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

            Plugin.log.Info("Spawn controller is destroyed! Dissolving notes...");

            foreach (NoteController controller in _activeNotes)
            {
                controller.Dissolve(1f);
            }

            foreach (ObstacleController controller in _activeObstacles)
            {
                controller.Dissolve(1f);
            }

            Plugin.log.Info("Notes dissolved!");
        }
    }
}
