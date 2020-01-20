using BS_Utils.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatSaberMultiplayer.Misc
{
    //https://github.com/xyonico/PracticePlugin/blob/master/PracticePlugin/SongSeekBeatmapHandler.cs
    public static class SongSeekBeatmapHandler
    {
        private static List<BeatmapObjectCallbackController.BeatmapObjectCallbackData> CallbackList
        {
            get
            {
                if (_beatmapObjectCallbackController == null || _callbackList == null)
                {
                    _beatmapObjectCallbackController = Resources.FindObjectsOfTypeAll<BeatmapObjectCallbackController>()
                        .FirstOrDefault();

                    if (_beatmapObjectCallbackController != null)
                    {
                        _callbackList =
                            _beatmapObjectCallbackController
                                .GetPrivateField<List<BeatmapObjectCallbackController.BeatmapObjectCallbackData>>(
                                    "_beatmapObjectCallbackData");

                        _beatmapData = _beatmapObjectCallbackController
                            .GetPrivateField<BeatmapData>("_beatmapData");
                    }

                    if (_beatmapObjectSpawnController == null)
                    {
                        _beatmapObjectSpawnController = Resources.FindObjectsOfTypeAll<BeatmapObjectSpawnController>()
                            .FirstOrDefault();
                        if (_beatmapObjectSpawnController != null)
                        {
                            _noteAPool = _beatmapObjectSpawnController.GetPrivateField<NoteController.Pool>("_noteAPool");
                            _noteBPool = _beatmapObjectSpawnController.GetPrivateField<NoteController.Pool>("_noteBPool");
                            _bombNotePool = _beatmapObjectSpawnController.GetPrivateField<NoteController.Pool>("_bombNotePool");
                            _fullHeightObstaclePool =
                                _beatmapObjectSpawnController.GetPrivateField<ObstacleController.Pool>("_fullHeightObstaclePool");
                            _topObstaclePool = _beatmapObjectSpawnController.GetPrivateField<ObstacleController.Pool>("_topObstaclePool");
                        }
                    }

                    if (_noteCutSoundEffectManager == null)
                    {
                        _noteCutSoundEffectManager = Resources.FindObjectsOfTypeAll<NoteCutSoundEffectManager>()
                            .FirstOrDefault();
                    }
                }

                return _callbackList;
            }
        }

        private static List<BeatmapObjectCallbackController.BeatmapObjectCallbackData> _callbackList;
        private static BeatmapObjectCallbackController _beatmapObjectCallbackController;
        private static BeatmapObjectSpawnController _beatmapObjectSpawnController;
        private static NoteCutSoundEffectManager _noteCutSoundEffectManager;

        private static NoteController.Pool _noteAPool;
        private static NoteController.Pool _noteBPool;
        private static NoteController.Pool _bombNotePool;
        private static ObstacleController.Pool _fullHeightObstaclePool;
        private static ObstacleController.Pool _topObstaclePool;

        private static BeatmapData _beatmapData;

        public static void OnSongTimeChanged(float newSongTime, float aheadTime)
        {
            foreach (var callbackData in CallbackList)
            {
                for (var i = 0; i < _beatmapData.beatmapLinesData.Length; i++)
                {
                    callbackData.nextObjectIndexInLine[i] = 0;
                    while (callbackData.nextObjectIndexInLine[i] < _beatmapData.beatmapLinesData[i].beatmapObjectsData.Length)
                    {
                        var beatmapObjectData = _beatmapData.beatmapLinesData[i].beatmapObjectsData[callbackData.nextObjectIndexInLine[i]];
                        if (beatmapObjectData.time - aheadTime >= newSongTime)
                        {
                            break;
                        }

                        callbackData.nextObjectIndexInLine[i]++;
                    }
                }
            }

            var newNextEventIndex = 0;

            while (newNextEventIndex < _beatmapData.beatmapEventData.Length)
            {
                var beatmapEventData = _beatmapData.beatmapEventData[newNextEventIndex];
                if (beatmapEventData.time >= newSongTime)
                {
                    break;
                }

                newNextEventIndex++;
            }

            _beatmapObjectCallbackController.SetPrivateField("_nextEventIndex", newNextEventIndex);

            var notesA = _noteAPool.activeItems.ToList();
            foreach (var noteA in notesA)
            {
                _beatmapObjectSpawnController.Despawn(noteA);
            }

            var notesB = _noteBPool.activeItems.ToList();
            foreach (var noteB in notesB)
            {
                _beatmapObjectSpawnController.Despawn(noteB);
            }

            var bombs = _bombNotePool.activeItems.ToList();
            foreach (var bomb in bombs)
            {
                _beatmapObjectSpawnController.Despawn(bomb);
            }

            var fullHeights = _fullHeightObstaclePool.activeItems.ToList();
            foreach (var fullHeight in fullHeights)
            {
                _beatmapObjectSpawnController.Despawn(fullHeight);
            }

            var tops = _topObstaclePool.activeItems.ToList();
            foreach (var top in tops)
            {
                _beatmapObjectSpawnController.Despawn(top);
            }

            InGameOnlineController.Instance.audioTimeSync.SetPrivateField("_prevAudioSamplePos", -1);
            InGameOnlineController.Instance.audioTimeSync.GetPrivateField<FloatSO>("_songTime").value = newSongTime;
            _noteCutSoundEffectManager.SetPrivateField("_prevNoteATime", -1);
            _noteCutSoundEffectManager.SetPrivateField("_prevNoteBTime", -1);
        }
    }
}