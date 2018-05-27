using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using SongLoaderPlugin;

namespace BeatSaberMultiplayer
{

    [Serializable]
    public class DifficultyLevel
    {
        public string difficulty;
        public int difficultyRank;
        public string audioPath;
        public string jsonPath;
        public int? offset;

        public DifficultyLevel(CustomSongInfo.DifficultyLevel difficultyLevel)
        {
            difficulty = difficultyLevel.difficulty;
            difficultyRank = difficultyLevel.difficultyRank;
            audioPath = difficultyLevel.audioPath;
            jsonPath = difficultyLevel.jsonPath;
        }

        public DifficultyLevel(LevelStaticData.DifficultyLevel difficultyLevel)
        {
            difficulty = LevelStaticData.GetDifficultyName(difficultyLevel.difficulty);
            difficultyRank = difficultyLevel.difficultyRank;
        }

        public DifficultyLevel(string Difficulty, int DifficultyRank, string AudioPath, string JsonPath, int Offset = 0)
        {
            difficulty = Difficulty;
            difficultyRank = DifficultyRank;
            audioPath = AudioPath;
            jsonPath = JsonPath;
            offset = Offset;

        }

    }
    [Serializable]
    public class Song
    {
        public string id;
        public string beatname;
        public string ownerid;
        public string downloads;
        public string upvotes;
        public string plays;
        public string beattext;
        public string uploadtime;
        public string songName;
        public string songSubName;
        public string authorName;
        public string beatsPerMinute;
        public DifficultyLevel[] difficultyLevels;
        public string img;

        public string path;

        public Song(JSONNode jsonNode)
        {

            id = jsonNode["id"];
            beatname = jsonNode["beatname"];
            ownerid = jsonNode["ownerid"];
            downloads = jsonNode["downloads"];
            plays = jsonNode["plays"];
            upvotes = jsonNode["upvotes"];
            beattext = jsonNode["beattext"];
            uploadtime = jsonNode["uploadtime"];
            songName = jsonNode["songName"];
            songSubName = jsonNode["songSubName"];
            authorName = jsonNode["authorName"];
            beatsPerMinute = jsonNode["beatsPerMinute"];
            img = jsonNode["img"];

            difficultyLevels = new DifficultyLevel[jsonNode["difficultyLevels"].Count];

            for (int i = 0; i < jsonNode["difficultyLevels"].Count; i++)
            {
                difficultyLevels[i] = new DifficultyLevel(jsonNode["difficultyLevels"][i]["difficulty"], jsonNode["difficultyLevels"][i]["difficultyRank"], jsonNode["difficultyLevels"][i]["audioPath"], jsonNode["difficultyLevels"][i]["jsonPath"]);
            }

        }

        public Song(JSONNode jsonNode, JSONNode difficultyNode)
        {

            id = jsonNode["id"].Value;
            beatname = jsonNode["beatname"].Value;
            ownerid = jsonNode["ownerid"].Value;
            downloads = jsonNode["downloads"].Value;
            upvotes = jsonNode["upvotes"].Value;
            plays = jsonNode["plays"].Value;
            beattext = jsonNode["beattext"].Value;
            uploadtime = jsonNode["uploadtime"].Value;
            songName = jsonNode["songName"].Value;
            songSubName = jsonNode["songSubName"].Value;
            authorName = jsonNode["authorName"].Value;
            beatsPerMinute = jsonNode["beatsPerMinute"].Value;
            img = jsonNode["img"].Value;

            difficultyLevels = new DifficultyLevel[difficultyNode.Count];

            for (int i = 0; i < difficultyNode.Count; i++)
            {
                difficultyLevels[i] = new DifficultyLevel(difficultyNode[i]["difficulty"], difficultyNode[i]["difficultyRank"], difficultyNode[i]["audioPath"], difficultyNode[i]["jsonPath"]);
            }

        }

        public bool Compare(Song compareTo)
        {
            if (compareTo != null)
            {
                /*
                Console.WriteLine("songName = " + HTML5Decode.HtmlDecode(compareTo.songName) + " vs " + HTML5Decode.HtmlDecode(songName)+" is "+ (HTML5Decode.HtmlDecode(songName) == HTML5Decode.HtmlDecode(compareTo.songName)));
                Console.WriteLine("songSubName = " + HTML5Decode.HtmlDecode(compareTo.songSubName) + " vs " + HTML5Decode.HtmlDecode(songSubName) + " is " + (HTML5Decode.HtmlDecode(songSubName) == HTML5Decode.HtmlDecode(compareTo.songSubName)));
                Console.WriteLine("authorName = " + HTML5Decode.HtmlDecode(compareTo.authorName) + " vs " + HTML5Decode.HtmlDecode(authorName) + " is " + (HTML5Decode.HtmlDecode(authorName) == HTML5Decode.HtmlDecode(compareTo.authorName)));
                Console.WriteLine("diffs = " + compareTo.difficultyLevels.Length + " vs " + difficultyLevels.Length + " is " + (difficultyLevels.Length == compareTo.difficultyLevels.Length));
                */

                if (HTML5Decode.HtmlDecode(songName) == HTML5Decode.HtmlDecode(compareTo.songName))
                {
                    if (difficultyLevels != null && compareTo.difficultyLevels != null)
                    {
                        return (HTML5Decode.HtmlDecode(songSubName) == HTML5Decode.HtmlDecode(compareTo.songSubName) && HTML5Decode.HtmlDecode(authorName) == HTML5Decode.HtmlDecode(compareTo.authorName) && difficultyLevels.Length == compareTo.difficultyLevels.Length);
                    }
                    else
                    {
                        return (HTML5Decode.HtmlDecode(songSubName) == HTML5Decode.HtmlDecode(compareTo.songSubName) && HTML5Decode.HtmlDecode(authorName) == HTML5Decode.HtmlDecode(compareTo.authorName));
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }



        public Song(CustomLevelStaticData _data)
        {
            songName = _data.songName;
            songSubName = _data.songSubName;
            authorName = _data.authorName;
            difficultyLevels = ConvertDifficultyLevels(_data.difficultyLevels);
        }

        public Song(CustomSongInfo _song)
        {

            songName = _song.songName;
            songSubName = _song.songSubName;
            authorName = _song.authorName;
            difficultyLevels = ConvertDifficultyLevels(_song.difficultyLevels);
            path = _song.path;
        }

        public DifficultyLevel[] ConvertDifficultyLevels(CustomSongInfo.DifficultyLevel[] _difficultyLevels)
        {
            if (_difficultyLevels != null && _difficultyLevels.Length > 0)
            {
                DifficultyLevel[] buffer = new DifficultyLevel[_difficultyLevels.Length];

                for (int i = 0; i < _difficultyLevels.Length; i++)
                {
                    buffer[i] = new DifficultyLevel(_difficultyLevels[i]);
                }


                return buffer;
            }
            else
            {
                return null;
            }
        }

        public DifficultyLevel[] ConvertDifficultyLevels(LevelStaticData.DifficultyLevel[] _difficultyLevels)
        {
            if (_difficultyLevels != null && _difficultyLevels.Length > 0)
            {
                DifficultyLevel[] buffer = new DifficultyLevel[_difficultyLevels.Length];

                for (int i = 0; i < _difficultyLevels.Length; i++)
                {
                    buffer[i] = new DifficultyLevel(_difficultyLevels[i]);
                }


                return buffer;
            }
            else
            {
                return null;
            }
        }

    }
    [Serializable]
    public class RootObject
    {
        public Song[] songs;
    }
}
