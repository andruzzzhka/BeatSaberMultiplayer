using BS_Utils.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.OverriddenClasses
{
    public class OnlineBeatmapObjectManager : BeatmapObjectManager
    {
        public OnlinePlayerController owner;
        public OnlineAudioTimeController onlineSyncController;

        private PlayerController _localPlayer;
        private AudioTimeSyncController _localSyncController;

        private List<NoteController> _activeNotes = new List<NoteController>();
        private List<ObstacleController> _activeObstacles = new List<ObstacleController>();

        public void Init(OnlinePlayerController newOwner, OnlineAudioTimeController syncController)
        {
            BeatmapObjectManager original = FindObjectsOfType<BeatmapObjectManager>().First(x => !(x is OnlineBeatmapObjectManager));

            transform.position = original.transform.position;

            foreach (FieldInfo info in original.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).Where(x => !x.Name.ToLower().Contains("event")))
            {
                info.SetValue(this, info.GetValue(original));
            }

            owner = newOwner;
            onlineSyncController = syncController;

            _localPlayer = FindObjectsOfType<PlayerController>().First(x => !(x is OnlinePlayerController));
            _localSyncController = FindObjectsOfType<AudioTimeSyncController>().First(x => !(x is OnlineAudioTimeController));
        }

        public new void SpawnBasicNote(NoteData noteData, Vector3 moveStartPos, Vector3 moveEndPos, Vector3 jumpEndPos, float moveDuration, float jumpDuration, float jumpGravity, float rotation, bool disappearingArrow, bool ghostNote, float cutDirectionAngleOffset)
        {
            NoteController noteController = ((noteData.noteType == NoteType.NoteA) ? _noteAPool : _noteBPool).Spawn();
            SetNoteControllerEventCallbacks(noteController);
            noteController.transform.SetPositionAndRotation(moveStartPos, Quaternion.identity);
            GameNoteController gameNoteController = noteController as GameNoteController;
            if (gameNoteController != null)
            {
                gameNoteController.Init(noteData, rotation, moveStartPos, moveEndPos, jumpEndPos, moveDuration, jumpDuration, jumpGravity, disappearingArrow, ghostNote, cutDirectionAngleOffset);
            }
            else
            {
                noteController.Init(noteData, rotation, moveStartPos, moveEndPos, jumpEndPos, moveDuration, jumpDuration, jumpGravity, cutDirectionAngleOffset);
            }
            this.GetPrivateField<Action<NoteController>>("noteWasSpawnedEvent")?.Invoke(noteController);
        }

        public new void SpawnBombNote(NoteData noteData, Vector3 moveStartPos, Vector3 moveEndPos, Vector3 jumpEndPos, float moveDuration, float jumpDuration, float jumpGravity, float rotation)
        {
            NoteController noteController = _bombNotePool.Spawn();
            SetNoteControllerEventCallbacks(noteController);
            noteController.transform.SetPositionAndRotation(moveStartPos, Quaternion.identity);
            noteController.Init(noteData, rotation, moveStartPos, moveEndPos, jumpEndPos, moveDuration, jumpDuration, jumpGravity, 0f);
            this.GetPrivateField<Action<NoteController>>("noteWasSpawnedEvent")?.Invoke(noteController);
        }

        public new void SpawnObstacle(ObstacleData obstacleData, Vector3 moveStartPos, Vector3 moveEndPos, Vector3 jumpEndPos, float moveDuration, float jumpDuration, float rotation, float noteLinesDistance, float obstacleHeight)
        {
            ObstacleController obstacleController = _obstaclePool.Spawn();
            SetObstacleEventCallbacks(obstacleController);
            obstacleController.transform.SetPositionAndRotation(moveStartPos, Quaternion.identity);
            obstacleController.Init(obstacleData, rotation, moveStartPos, moveEndPos, jumpEndPos, moveDuration, jumpDuration, noteLinesDistance, obstacleHeight);
        }

        public override void SetNoteControllerEventCallbacks(NoteController noteController)
        {
            noteController.noteDidStartJumpEvent += HandleNoteDidStartJump;
            noteController.noteDidFinishJumpEvent += HandleNoteDidFinishJump;
            noteController.noteWasCutEvent += HandleNoteWasCut;
            noteController.noteWasMissedEvent += HandleNoteWasMissed;
            noteController.noteDidDissolveEvent += HandleNoteDidDissolve;

            var noteJump = noteController.GetComponent<NoteJump>();
            noteJump.SetPrivateField("_playerController", owner);
            noteJump.SetPrivateField("_audioTimeSyncController", onlineSyncController);
            noteController.GetComponent<NoteFloorMovement>().SetPrivateField("_audioTimeSyncController", onlineSyncController);
            _activeNotes.Add(noteController);
        }

        public override void RemoveNoteControllerEventCallbacks(NoteController noteController)
        {
            noteController.noteDidStartJumpEvent -= HandleNoteDidStartJump;
            noteController.noteDidFinishJumpEvent -= HandleNoteDidFinishJump;
            noteController.noteWasCutEvent -= HandleNoteWasCut;
            noteController.noteWasMissedEvent -= HandleNoteWasMissed;
            noteController.noteDidDissolveEvent -= HandleNoteDidDissolve;

            var noteJump = noteController.GetComponent<NoteJump>();
            noteJump.SetPrivateField("_playerController", _localPlayer);
            noteJump.SetPrivateField("_audioTimeSyncController", _localSyncController);
            noteController.GetComponent<NoteFloorMovement>().SetPrivateField("_audioTimeSyncController", _localSyncController);

            if (_activeNotes != null)
                _activeNotes.Remove(noteController);
        }

        public override void SetObstacleEventCallbacks(ObstacleController obstacleController)
        {
            obstacleController.finishedMovementEvent += HandleObstacleFinishedMovement;
            obstacleController.passedThreeQuartersOfMove2Event += HandleObstaclePassedThreeQuartersOfMove2;
            obstacleController.passedAvoidedMarkEvent += HandleObstaclePassedAvoidedMark;
            obstacleController.didDissolveEvent += HandleObstacleDidDissolve;

            obstacleController.SetPrivateField("_playerController", owner);
            obstacleController.SetPrivateField("_audioTimeSyncController", onlineSyncController);
            _activeObstacles.Add(obstacleController);
        }

        public override void RemoveObstacleEventCallbacks(ObstacleController obstacleController)
        {
            obstacleController.finishedMovementEvent -= HandleObstacleFinishedMovement;
            obstacleController.passedThreeQuartersOfMove2Event -= HandleObstaclePassedThreeQuartersOfMove2;
            obstacleController.passedAvoidedMarkEvent -= HandleObstaclePassedAvoidedMark;
            obstacleController.didDissolveEvent -= HandleObstacleDidDissolve;

            obstacleController.SetPrivateField("_playerController", _localPlayer);
            obstacleController.SetPrivateField("_audioTimeSyncController", _localSyncController);
            if (_activeObstacles != null)
                _activeObstacles.Remove(obstacleController);
        }

        public override void Despawn(NoteController noteController)
        {
            if (noteController.noteData.noteType == NoteType.NoteA)
            {
                RemoveNoteControllerEventCallbacks(noteController);
                _noteAPool.Despawn(noteController);
                return;
            }
            if (noteController.noteData.noteType == NoteType.NoteB)
            {
                RemoveNoteControllerEventCallbacks(noteController);
                _noteBPool.Despawn(noteController);
                return;
            }
            if (noteController.noteData.noteType == NoteType.Bomb)
            {
                RemoveNoteControllerEventCallbacks(noteController);
                _bombNotePool.Despawn(noteController);
            }
        }

        public override void Despawn(ObstacleController obstacleController)
        {
            RemoveObstacleEventCallbacks(obstacleController);
            _obstaclePool.Despawn(obstacleController);
        }

        public void PrepareForDestroy()
        {
            Plugin.log.Info("Spawn controller is destroyed! Dissolving notes and obstacles...");

            for (int i = 0; i < _activeNotes.Count; i++)
            {
                _activeNotes[i].Dissolve(1.4f);
            }

            for (int i = 0; i < _activeObstacles.Count; i++)
            {
                _activeObstacles[i].Dissolve(1.4f);
            }
        }

        public override void HandleNoteDidFinishJump(NoteController noteController)
        {
            Despawn(noteController);
        }

        public override void HandleNoteDidDissolve(NoteController noteController)
        {
            Despawn(noteController);
        }

        public override void HandleNoteWasCut(NoteController noteController, NoteCutInfo noteCutInfo)
        {
            this.GetPrivateField<Action<INoteController, NoteCutInfo>>("noteWasCutEvent")?.Invoke(noteController, noteCutInfo);
            Despawn(noteController);
        }

        public override void HandleObstacleFinishedMovement(ObstacleController obstacleController)
        {
            Despawn(obstacleController);
        }

        public override void HandleObstacleDidDissolve(ObstacleController obstacleController)
        {
            Despawn(obstacleController);
        }
    }
}
