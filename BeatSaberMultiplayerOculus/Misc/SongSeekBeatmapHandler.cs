using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/*
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

                        _beatmapObjectCallbackController.GetBeatmapDataModelFromProvider();
                        _beatmapData = _beatmapObjectCallbackController
                            .GetPrivateField<BeatmapDataModel>("_beatmapDataModel").beatmapData;
                    }

                    if (_beatmapObjectSpawnController == null)
                    {
                        _beatmapObjectSpawnController = Resources.FindObjectsOfTypeAll<BeatmapObjectSpawnController>()
                            .FirstOrDefault();
                        if (_beatmapObjectSpawnController != null)
                        {
                            _gameNotePrefab =
                                _beatmapObjectSpawnController.GetPrivateField<NoteController>("_gameNotePrefab");
                            _bombNotePrefab =
                                _beatmapObjectSpawnController.GetPrivateField<BombNoteController>("_bombNotePrefab");
                            _obstacleFullHeightPrefab =
                                _beatmapObjectSpawnController.GetPrivateField<ObstacleController>(
                                    "_obstacleFullHeightPrefab");
                            _obstacleTopPrefab =
                                _beatmapObjectSpawnController.GetPrivateField<ObstacleController>("_obstacleTopPrefab");
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

        private static NoteController _gameNotePrefab;
        private static BombNoteController _bombNotePrefab;
        private static ObstacleController _obstacleFullHeightPrefab;
        private static ObstacleController _obstacleTopPrefab;

        private static BeatmapData _beatmapData;

        public static void OnSongTimeChanged(float newSongTime, float aheadTime)
        {
            foreach (var callbackData in CallbackList)
            {
                for (var i = 0; i < _beatmapData.beatmapLinesData.Length; i++)
                {
                    callbackData.nextObjectIndexInLine[i] = 0;
                    while (callbackData.nextObjectIndexInLine[i] <
                           _beatmapData.beatmapLinesData[i].beatmapObjectsData.Length)
                    {
                        var beatmapObjectData = _beatmapData.beatmapLinesData[i]
                            .beatmapObjectsData[callbackData.nextObjectIndexInLine[i]];
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

            _gameNotePrefab.gameObject.RecycleAll();
            _bombNotePrefab.gameObject.RecycleAll();
            _obstacleFullHeightPrefab.RecycleAll();
            _obstacleTopPrefab.RecycleAll();

            SpectatingController.Instance.audioTimeSync.SetPrivateField("_prevAudioSamplePos", -1);
            SpectatingController.Instance.audioTimeSync.GetPrivateField<FloatVariableSetter>("_songTime").SetValue(newSongTime);
            _noteCutSoundEffectManager.SetPrivateField("_prevNoteATime", -1);
            _noteCutSoundEffectManager.SetPrivateField("_prevNoteBTime", -1);
        }
    }
}
*/