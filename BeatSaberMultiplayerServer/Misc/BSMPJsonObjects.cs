using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BeatSaberMultiplayerServer
{
    class PlayerInfo
    {
        public string playerName;
        public string playerId;
        public int playerScore;
        public int playerCombo;
        public int playerMaxCombo;


        public PlayerInfo(string _name, string _id)
        {
            playerName = _name;
            playerId = _id;
        }
    }

    enum ServerState { Lobby, Playing };

    enum ServerCommandType {SetServerState ,SetLobbyTimer, DownloadSongs, StartSelectedSongLevel, SetPlayerInfos, SetSelectedSong, UpdateRequired, Ping };

    class ServerCommand
    {
        public string version = "0.1";
        public ServerCommandType commandType;
        public ServerState serverState;
        public int lobbyTimer;
        public string[] songsToDownload;
        public string selectedLevelID;
        public int selectedSongDifficlty;
        public string[] playerInfos;
        public double selectedSongDuration;
        public double selectedSongPlayTime;

        public ServerCommand(ServerCommandType _type, int _timer = 0, string[] _songs = null, string _selectedLevelID = null, int _difficulty = 0, string[] _playerInfos = null, double _selectedSongDuration = 0, double _selectedSongPlayTime = 0)
        {
            version = ServerMain.version;
            commandType = _type;
            lobbyTimer = _timer;
            songsToDownload = _songs;
            serverState = ServerMain.serverState;
            selectedLevelID = _selectedLevelID;
            selectedSongDifficlty = _difficulty;
            playerInfos = _playerInfos;
            selectedSongDuration = _selectedSongDuration;
            selectedSongPlayTime = _selectedSongPlayTime;
        }
    }

    enum ClientCommandType { GetServerState, SetPlayerInfo, GetAvailableSongs };

    [Serializable]
    class ClientCommand
    {
        public string version = "0.1";
        public ClientCommandType commandType;
        public string playerInfo;

        public ClientCommand(ClientCommandType _type, string _playerInfo = null)
        {
            
            commandType = _type;
            playerInfo = _playerInfo;
        }

    }

    public enum ClientState { Disconnected, Connected, Playing, UpdateRequired, MasterServer};

    enum Difficulty { Easy, Normal, Hard, Expert, ExpertPlus };

    [Serializable]
    public class CustomSongInfo
    {
        public string songName;
        public string songSubName;
        public string authorName;
        public float beatsPerMinute;
        public float previewStartTime;
        public float previewDuration;
        public string environmentName;
        public string coverImagePath;
        public string videoPath;
        public DifficultyLevel[] difficultyLevels;
        public string path;
        public string levelId;

        public TimeSpan duration;

        [Serializable]
        public class DifficultyLevel
        {
            public string difficulty;
            public int difficultyRank;
            public string audioPath;
            public string jsonPath;
            public string json;
        }

        public string GetIdentifier()
        {
            var combinedJson = "";
            foreach (var diffLevel in difficultyLevels)
            {
                if (!File.Exists(path + "/" + diffLevel.jsonPath))
                {
                    continue;
                }

                diffLevel.json = File.ReadAllText(path + "/" + diffLevel.jsonPath);
                combinedJson += diffLevel.json;
            }

            var hash = MD5Hash(combinedJson);
            levelId = hash + "∎" + string.Join("∎", new[] { songName, songSubName, authorName, beatsPerMinute.ToString() }) + "∎";
            return levelId;
        }

        public static string MD5Hash(string input)
        {
            StringBuilder hash = new StringBuilder();
            MD5CryptoServiceProvider md5provider = new MD5CryptoServiceProvider();
            byte[] bytes = md5provider.ComputeHash(new UTF8Encoding().GetBytes(input));

            for (int i = 0; i < bytes.Length; i++)
            {
                hash.Append(bytes[i].ToString("x2"));
            }
            return hash.ToString().ToUpper();
        }
    }
}
