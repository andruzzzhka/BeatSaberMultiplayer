using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BeatSaberMultiplayer
{
    [Serializable]
    class PlayerInfo
    {
        public string playerName;
        public ulong playerId;

        public int playerScore;
        public int playerCutBlocks;
        public int playerComboBlocks;
        public int playerEnergy;

        public string playerAvatar;

        [NonSerialized]
        public Vector3 rightHandPos;
        [NonSerialized]
        public Vector3 leftHandPos;
        [NonSerialized]
        public Vector3 headPos;
        [NonSerialized]
        public Quaternion rightHandRot;
        [NonSerialized]
        public Quaternion leftHandRot;
        [NonSerialized]
        public Quaternion headRot;

        public PlayerInfo(string _name, ulong _id, string _avatar = null)
        {
            playerName = _name;
            playerId = _id;
            playerAvatar = _avatar;
        }

        public override bool Equals(object obj)
        {
            if(obj is PlayerInfo)
            {
                return (playerId == (obj as PlayerInfo).playerId) && (playerName == (obj as PlayerInfo).playerName);
            }
            else
            {
                return false;
            }
        }
    }

    enum ServerState { Voting, Preparing, Playing };

    enum ServerCommandType { SetServerState, SetLobbyTimer, DownloadSongs, StartSelectedSongLevel, SetPlayerInfos, SetSelectedSong, UpdateRequired, Ping, Kicked };

    [Serializable]
    class ServerCommand
    {
        public string version = "0.4.5.0";
        public ServerCommandType commandType;
        public ServerState serverState;
        public int lobbyTimer;
        public string[] songsToDownload;
        public string selectedLevelID;
        public int selectedSongDifficlty;
        public string[] playerInfos;
        public double selectedSongDuration;
        public double selectedSongPlayTime;
        public string kickReason;
        public bool noFailMode;
        public string scoreboardScoreFormat;

        public ServerCommand(ServerCommandType _type, int _timer = 0, string[] _songs = null, string _selectedLevelID = null, int _difficulty = 0, string[] _playerInfos = null, double _selectedSongDuration = 0, double _selectedSongPlayTime = 0, string _kickReason = "")
        {
            commandType = _type;
            lobbyTimer = _timer;
            songsToDownload = _songs;
            selectedLevelID = _selectedLevelID;
            selectedSongDifficlty = _difficulty;
            playerInfos = _playerInfos;
            selectedSongDuration = _selectedSongDuration;
            selectedSongPlayTime = _selectedSongPlayTime;
            kickReason = _kickReason;
        }
    }

    enum ClientCommandType { GetServerState, SetPlayerInfo, GetAvailableSongs, VoteForSong };

    [Serializable]
    class ClientCommand
    {
        public string version = "0.4.5.0";
        public ClientCommandType commandType;
        public string playerInfo;
        public string voteForLevelId;

        public ClientCommand(ClientCommandType _type, string _playerInfo = null, string _voteForLevelId = null)
        {
            version = BSMultiplayerClient.version;
            commandType = _type;
            playerInfo = _playerInfo;
            voteForLevelId = _voteForLevelId;
        }

    }
}
