using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using IPA.Utilities;
using Lidgren.Network;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class ReplayData
    {
        public PlayerInfo playerInfo;
        public List<PlayerUpdate> updates;
        public Dictionary<int, HitData> hits;

        public ReplayData()
        {
            playerInfo = null;
            updates = new List<PlayerUpdate>();
            hits = new Dictionary<int, HitData>();
        }
    }

    public class SpectatingController : MonoBehaviour
    {
        public static SpectatingController Instance;

        public static bool active = false;

        public Dictionary<ulong, ReplayData> playerUpdates = new Dictionary<ulong, ReplayData>();

        public OnlinePlayerController spectatedPlayer;
        public AudioTimeSyncController audioTimeSync;

        private ScoreController _scoreController;
        private GameEnergyCounter _energyCounter;

        private string _currentScene;

        private OnlineVRController _leftController;
        private OnlineVRController _rightController;

        private Saber _leftSaber;
        private Saber _rightSaber;

        private TextMeshPro _spectatingText;
        private TextMeshPro _bufferingText;

        private bool _paused = false;

#if DEBUG && VERBOSE
        private int _prevFrameIndex = 0;
        private float _prevFrameLerp = 0f;
#endif

        public static void OnLoad()
        {
            if (Instance != null)
                return;
            new GameObject("SpectatingController").AddComponent<SpectatingController>();

        }

        public void Awake()
        {
            if (Instance != this)
            {
                Instance = this;
                DontDestroyOnLoad(this);

                Client.Instance.PlayerInfoUpdateReceived -= PacketReceived;
                Client.Instance.PlayerInfoUpdateReceived += PacketReceived;
                _currentScene = SceneManager.GetActiveScene().name;
            }
        }

        public void MenuSceneLoaded()
        {
            _currentScene = "MenuCore";
            active = false;
            if (!Config.Instance.SpectatorMode)
                return;
            DestroyAvatar();
            if (_spectatingText != null)
            {
                Destroy(_spectatingText);
            }
            if (_bufferingText != null)
            {
                Destroy(_bufferingText);
            }
        }

        public void GameSceneLoaded()
        {
            Plugin.log.Info("Game scene loaded");
            _currentScene = "GameCore";

            if (!Config.Instance.SpectatorMode || !Client.Instance.connected)
            {
                active = false;
                return;
            }

            StartCoroutine(Delay(5, () => {
                active = true;
            }));
            _paused = false;

            DestroyAvatar();
            ReplaceControllers();
            
            if(_spectatingText != null)
            {
                Destroy(_spectatingText);
            }

            _spectatingText = CustomExtensions.CreateWorldText(transform, "Spectating PLAYER");
            _spectatingText.alignment = TextAlignmentOptions.Center;
            _spectatingText.fontSize = 6f;
            _spectatingText.transform.position = new Vector3(0f, 3.75f, 12f);
            _spectatingText.gameObject.SetActive(false);

            if (_bufferingText != null)
            {
                Destroy(_bufferingText);
            }

            _bufferingText = CustomExtensions.CreateWorldText(transform, "Buffering...");
            _bufferingText.alignment = TextAlignmentOptions.Center;
            _bufferingText.fontSize = 8f;
            _bufferingText.transform.position = new Vector3(0f, 2f, 8f);
            _bufferingText.gameObject.SetActive(false);

            if (playerUpdates != null)
                playerUpdates.Clear();
            else
                playerUpdates = new Dictionary<ulong, ReplayData>();


#if DEBUG  && LOCALREPLAY
            string replayPath = Path.GetFullPath("MPDumps\\BootyBounce.mpdmp");

            Stream stream;
            long length;
            long position;

            if (replayPath.EndsWith(".zip"))
            {
                ZipArchiveEntry entry = new ZipArchive(File.Open(replayPath, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read).Entries.First(x => x.Name.EndsWith(".mpdmp"));
                position = 0;
                length = entry.Length;
                stream = entry.Open();
            }
            else
            {
                var fileStream = File.Open(replayPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                position = 0;
                length = fileStream.Length;
                stream = fileStream;
            }

            byte[] startLevelInfo = new byte[2];
            stream.Read(startLevelInfo, 0, 2);
            position += 2;

            byte[] levelIdBytes = new byte[16];
            stream.Read(levelIdBytes, 0, 20);
            position += 20;

            while (length - position > 79)
            {

                byte[] packetSizeBytes = new byte[4];
                stream.Read(packetSizeBytes, 0, 4);
                position += 4;

                int packetSize = BitConverter.ToInt32(packetSizeBytes, 0);

                byte[] packetBytes = new byte[packetSize];
                stream.Read(packetBytes, 0, packetSize);
                position += packetSize;

                PlayerInfo player = new PlayerInfo(packetBytes);
                player.playerId = 76561198047255565;
                player.playerName = "andruzzzhka";
                player.avatarHash = "1f0152521ab8aa04ea53beed79c083e6".ToUpper();

                if (playerInfos.ContainsKey(player.playerId))
                {
                    playerInfos[player.playerId].Add(player);
                    playersHits[player.playerId].AddRange(player.hitsLastUpdate);
                }
                else
                {
                    playerInfos.Add(player.playerId, new List<PlayerInfo>() { player });
                    playersHits.Add(player.playerId, new List<HitData>(player.hitsLastUpdate));
                }
            }

            Plugin.log.Info("Loaded "+ playerInfos[76561198047255565].Count + " packets!");
#endif
        }

        IEnumerator Delay(int frames, Action callback)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
            callback.Invoke();
        }

        void ReplaceControllers()
        {
            if (!Config.Instance.SpectatorMode || Client.Instance.inRadioMode)
                return;
            
            audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();

            _leftSaber = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberB);
            _leftController = _leftSaber.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            _leftSaber.SetPrivateField("_vrController", _leftController);

            _rightSaber = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberA);
            _rightController = _rightSaber.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            _rightSaber.SetPrivateField("_vrController", _rightController);

            Plugin.log.Info("Controllers replaced!");

            _scoreController = FindObjectOfType<ScoreController>();

#if DEBUG
            _scoreController.noteWasMissedEvent += _scoreController_noteWasMissedEvent;
            _scoreController.noteWasCutEvent += _scoreController_noteWasCutEvent;
#endif

            Plugin.log.Info("Score controller found!");

            _energyCounter = FindObjectOfType<GameEnergyCounter>();

            Plugin.log.Info("Energy counter found!");
        }

#if DEBUG
        private void _scoreController_noteWasCutEvent(NoteData arg1, NoteCutInfo arg2, int arg3)
        {
            if(spectatedPlayer != null && spectatedPlayer.playerInfo != null)
            {
                HitData hit = playerUpdates[spectatedPlayer.playerInfo.playerId].hits.FirstOrDefault(x => x.Key == arg1.id).Value;
                bool allIsOKExpected = hit.noteWasCut && hit.speedOK && hit.saberTypeOK && hit.directionOK && !hit.wasCutTooSoon;

                if(allIsOKExpected != arg2.allIsOK)
                {
                    Plugin.log.Error($"Replay error detected!\n|   Real hit: {arg2.allIsOK}\n|   Expected hit: {allIsOKExpected}\n|   Time: {audioTimeSync.songTime}");
                }
            }
        }

        private void _scoreController_noteWasMissedEvent(NoteData arg1, int arg2)
        {
            if (spectatedPlayer != null && spectatedPlayer.playerInfo != null)
            {
                HitData hit = playerUpdates[spectatedPlayer.playerInfo.playerId].hits.FirstOrDefault(x => x.Key == arg1.id).Value;

                if (hit.noteWasCut)
                {
                    Plugin.log.Error($"Replay error detected!\n|   Real hit: false\n|   Expected hit: {hit.noteWasCut}\n|   Time: {audioTimeSync.songTime}");
                }
            }
        }
#endif

        private void PacketReceived(NetIncomingMessage msg)
        {
            if (Config.Instance.SpectatorMode && !Client.Instance.inRadioMode && _currentScene == "GameCore")
            {
                msg.Position = 0;
                CommandType commandType = (CommandType)msg.ReadByte();
                if (commandType == CommandType.GetPlayerUpdates)
                {
                    int playersCount = msg.ReadInt32();

                    for (int j = 0; j < playersCount; j++)
                    {
                        try
                        {
                            ulong playerId = msg.ReadUInt64();
                            byte packetsCount = msg.ReadByte();

                            ReplayData replay;

                            if(playerUpdates.TryGetValue(playerId, out replay))
                            {
                                if(replay.playerInfo == null)
                                {
                                    if (InGameOnlineController.Instance.players.TryGetValue(playerId, out OnlinePlayerController playerController))
                                    {
                                        replay.playerInfo = playerController.playerInfo;
                                    }
                                }

                                for (int k = 0; k < packetsCount; k++)
                                {
                                    PlayerUpdate player = new PlayerUpdate(msg);

                                    replay.updates.Add(player);
                                }

                                byte hitCount = msg.ReadByte();

                                for (int i = 0; i < hitCount; i++)
                                {
                                    HitData hit = new HitData(msg);
                                    if (!replay.hits.ContainsKey(hit.objectId))
                                        replay.hits.Add(hit.objectId, hit);
                                }

                                if (replay.updates.Count > 450)
                                {
                                    replay.updates.RemoveRange(0, replay.updates.Count - 450);
                                }
                            }
                            else
                            {
                                replay = new ReplayData();

                                if(InGameOnlineController.Instance.players.TryGetValue(playerId, out OnlinePlayerController playerController))
                                {
                                    replay.playerInfo = playerController.playerInfo;
                                }

                                if(replay.playerInfo == null)
                                {
                                    InGameOnlineController.Instance.sendFullUpdate = true;
                                    continue;
                                }

                                for (int k = 0; k < packetsCount; k++)
                                {
                                    PlayerUpdate player = new PlayerUpdate(msg);

                                    replay.updates.Add(player);
                                }

                                byte hitCount = msg.ReadByte();

                                for (int i = 0; i < hitCount; i++)
                                {
                                    HitData hit = new HitData(msg);
                                    if(!replay.hits.ContainsKey(hit.objectId))
                                        replay.hits.Add(hit.objectId, hit);
                                }

                                playerUpdates.Add(playerId, replay);
                            }                            
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            Plugin.log.Critical($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                        }
                    }
                }
            }
        }

        public void DestroyAvatar()
        {
            if(spectatedPlayer != null)
            {
                Destroy(spectatedPlayer.gameObject);
            }
        }

        public void Update()
        {
            if (Config.Instance.SpectatorMode && _currentScene == "GameCore" && active)
            {
                if (spectatedPlayer == null && _leftSaber != null && _rightSaber != null)
                {
                    spectatedPlayer = new GameObject("SpectatedPlayerController").AddComponent<OnlinePlayerController>();
                    spectatedPlayer.SetAvatarState(Config.Instance.ShowAvatarsInGame);
                    spectatedPlayer.SetSabers(_leftSaber, _rightSaber);

                    ReplacePlayerController(spectatedPlayer);

                    spectatedPlayer.noInterpolation = true;

                    if (_leftController != null && _rightController != null)
                    {
                        _leftController.owner = spectatedPlayer;
                        _rightController.owner = spectatedPlayer;
                    }
                }

                if (Input.GetKeyDown(KeyCode.KeypadMultiply) && spectatedPlayer != null && spectatedPlayer.playerInfo != null)
                {
                    int index = playerUpdates.Keys.ToList().FindIndexInList(spectatedPlayer.playerInfo.playerId);
                    if (index >= playerUpdates.Count - 1)
                    {
                        index = 0;
                    }

                    spectatedPlayer.playerInfo = playerUpdates[playerUpdates.Keys.ElementAt(index)].playerInfo;
                    Plugin.log.Info("Spectating player: " + spectatedPlayer.playerInfo.playerName);
                    _spectatingText.gameObject.SetActive(true);
                    _spectatingText.text = "Spectating " + spectatedPlayer.playerInfo.playerName;
                }

                if (Input.GetKeyDown(KeyCode.KeypadDivide) && spectatedPlayer != null && spectatedPlayer.playerInfo != null)
                {
                    int index = playerUpdates.Keys.ToList().FindIndexInList(spectatedPlayer.playerInfo.playerId);
                    if (index <= 0)
                    {
                        index = playerUpdates.Count - 1;
                    }

                    spectatedPlayer.playerInfo = playerUpdates[playerUpdates.Keys.ElementAt(index)].playerInfo;
                    Plugin.log.Info("Spectating player: " + spectatedPlayer.playerInfo.playerName);
                    _spectatingText.gameObject.SetActive(true);
                    _spectatingText.text = "Spectating " + spectatedPlayer.playerInfo.playerName;
                }


                if (playerUpdates.Count > 0 && spectatedPlayer != null && spectatedPlayer.playerInfo == null)
                {
                    spectatedPlayer.playerInfo = playerUpdates.FirstOrDefault(x => x.Key != Client.Instance.playerInfo.playerId).Value?.playerInfo;
                    Plugin.log.Info("Set spectated player...");
                    if (spectatedPlayer.playerInfo != null)
                    {
                        Plugin.log.Info("Spectating player: " + spectatedPlayer.playerInfo.playerName);
                        _spectatingText.gameObject.SetActive(true);
                        _spectatingText.text = "Spectating " + spectatedPlayer.playerInfo.playerName;
                    }
                }

                if (spectatedPlayer != null && spectatedPlayer.playerInfo != null)
                {
                    float currentSongTime = Math.Max(0f, audioTimeSync.songTime);
                    int index = FindClosestIndex(playerUpdates[spectatedPlayer.playerInfo.playerId].updates, currentSongTime);
                    index = Math.Max(index, 0);
                    (float, float) playerProgressMinMax = MinMax(playerUpdates[spectatedPlayer.playerInfo.playerId].updates);
                    PlayerUpdate lerpFrom;
                    PlayerUpdate lerpTo;
                    float lerpProgress;


                    if (playerProgressMinMax.Item2 < currentSongTime + 2f && audioTimeSync.songLength > currentSongTime + 5f && !_paused)
                    {
                        Plugin.log.Info($"Pausing...");
                        if (playerProgressMinMax.Item2 > 2.5f)
                        {
                            Plugin.log.Info($"Buffering...");
                            _bufferingText.gameObject.SetActive(true);
                            _bufferingText.alignment = TextAlignmentOptions.Center;
                        }
                        InGameOnlineController.Instance.PauseSong();
                        _paused = true;
                        Plugin.log.Info($"Paused!");
                    }

                    if (playerProgressMinMax.Item2 - currentSongTime > 3f && _paused)
                    {
                        _bufferingText.gameObject.SetActive(false);
                        Plugin.log.Info("Resuming song...");
                        InGameOnlineController.Instance.ResumeSong();
                        _paused = false;
                    }

                    if (_paused)
                        return;

                    if (playerProgressMinMax.Item1 < currentSongTime && playerProgressMinMax.Item2 > currentSongTime)
                    {
                        lerpFrom = playerUpdates[spectatedPlayer.playerInfo.playerId].updates[index];
                        lerpTo = playerUpdates[spectatedPlayer.playerInfo.playerId].updates[index + 1];

                        lerpProgress = Remap(currentSongTime, lerpFrom.playerProgress, lerpTo.playerProgress, 0f, 1f);
                    }
                    else
                    {
                        if (audioTimeSync.songLength - currentSongTime > 5f && currentSongTime > 3f)
                        {
                            Plugin.log.Warn($"No data recorded for that point in time!\nStart time: {playerProgressMinMax.Item1}\nStop time: {playerProgressMinMax.Item2}\nCurrent time: {currentSongTime}");
                        }
                        return;
                    }

#if DEBUG && VERBOSE
                    if (index - _prevFrameIndex > 1)
                    {
                        Plugin.log.Warn($"Frame skip!\nPrev index: {_prevFrameIndex}\nNew index: {index}");
                    }
                    else if (index < _prevFrameIndex)
                    {
                        Plugin.log.Warn($"Going back in time!\nPrev index: {_prevFrameIndex}\nNew index: {index}");
                    }
                    else if (_prevFrameIndex == index)
                    {
                        if (lerpProgress < _prevFrameLerp)
                        {
                            Plugin.log.Warn($"Going back in time!\nPrev lerp progress: {_prevFrameIndex}\nNew lerp progress: {lerpProgress}");
                        }
                        else if (_prevFrameLerp == lerpProgress)
                        {
                            Plugin.log.Warn($"Staying in place! Prev lerp progress: {_prevFrameLerp}\nNew  lerp progress: {lerpProgress}");
                        }
                    }
                    _prevFrameIndex = index;
                    _prevFrameLerp = lerpProgress;
#endif

                    spectatedPlayer.playerInfo.updateInfo.leftHandPos = Vector3.Lerp(lerpFrom.leftHandPos, lerpTo.leftHandPos, lerpProgress);
                    spectatedPlayer.playerInfo.updateInfo.rightHandPos = Vector3.Lerp(lerpFrom.rightHandPos, lerpTo.rightHandPos, lerpProgress);
                    spectatedPlayer.playerInfo.updateInfo.headPos = Vector3.Lerp(lerpFrom.headPos, lerpTo.headPos, lerpProgress);
                    
                    spectatedPlayer.playerInfo.updateInfo.leftHandRot = Quaternion.Lerp(lerpFrom.leftHandRot, lerpTo.leftHandRot, lerpProgress);
                    spectatedPlayer.playerInfo.updateInfo.rightHandRot = Quaternion.Lerp(lerpFrom.rightHandRot, lerpTo.rightHandRot, lerpProgress);
                    spectatedPlayer.playerInfo.updateInfo.headRot = Quaternion.Lerp(lerpFrom.headRot, lerpTo.headRot, lerpProgress);

                    if (spectatedPlayer.playerInfo.updateInfo.fullBodyTracking)
                    {
                        spectatedPlayer.playerInfo.updateInfo.leftLegPos = Vector3.Lerp(lerpFrom.leftLegPos, lerpTo.leftLegPos, lerpProgress);
                        spectatedPlayer.playerInfo.updateInfo.rightLegPos = Vector3.Lerp(lerpFrom.rightLegPos, lerpTo.rightLegPos, lerpProgress);
                        spectatedPlayer.playerInfo.updateInfo.pelvisPos = Vector3.Lerp(lerpFrom.pelvisPos, lerpTo.pelvisPos, lerpProgress);

                        spectatedPlayer.playerInfo.updateInfo.leftLegRot = Quaternion.Lerp(lerpFrom.leftLegRot, lerpTo.leftLegRot, lerpProgress);
                        spectatedPlayer.playerInfo.updateInfo.rightLegRot = Quaternion.Lerp(lerpFrom.rightLegRot, lerpTo.rightLegRot, lerpProgress);
                        spectatedPlayer.playerInfo.updateInfo.pelvisRot = Quaternion.Lerp(lerpFrom.pelvisRot, lerpTo.pelvisRot, lerpProgress);
                    }

                    if (_scoreController != null)
                    {
                        _scoreController.SetPrivateField("_prevFrameRawScore", (int)lerpFrom.playerScore);
                        _scoreController.SetPrivateField("_baseRawScore", (int)lerpTo.playerScore);
                        _scoreController.SetPrivateField("_combo", (int)lerpTo.playerComboBlocks);
                    }

                    if(_energyCounter != null)
                    {
                        _energyCounter.SetPrivateProperty("energy", lerpTo.playerEnergy / 100f);
                    }

                }
            }
        }

        private void ReplacePlayerController(PlayerController newPlayerController)
        {
            Type[] typesToReplace = new Type[] { typeof(BombCutSoundEffectManager), typeof(NoteCutSoundEffectManager), typeof(MoveBackWall), typeof(NoteFloorMovement), typeof(NoteJump), typeof(NoteLineConnectionController), typeof(ObstacleController), typeof(PlayerHeadAndObstacleInteraction) };

            foreach(Type type in typesToReplace)
            {
                Resources.FindObjectsOfTypeAll(type).ToList().ForEach(x => x.SetPrivateField("_playerController", newPlayerController));
            }
        }

        public float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        private static int FindClosestIndex(List<PlayerUpdate> infos, float targetProgress)
        {
            for (int i = 0; i < infos.Count - 1; i++)
            {
                if ((infos[i].playerProgress < targetProgress && infos[i+1].playerProgress > targetProgress) || Mathf.Abs(infos[i].playerProgress - targetProgress) < float.Epsilon)
                {
                    return i;
                }
            }
            return -1;
        }

        private static (float, float) MinMax(List<PlayerUpdate> infos)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach(PlayerUpdate info in infos)
            {
                if (info.playerProgress > max)
                    max = info.playerProgress;
                if (info.playerProgress < min)
                    min = info.playerProgress;
            }

            return (min, max);
        }
    }
}
