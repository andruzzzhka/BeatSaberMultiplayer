using System.Collections.Generic;

namespace BeatSaberMultiplayer.Misc
{
    public struct ScrappedSong
    {
        public string Key { get; set; }
        public string Hash { get; set; }
        public string SongName { get; set; }
        public string SongSubName { get; set; }
        public string LevelAuthorName { get; set; }
        public string SongAuthorName { get; set; }
        public List<DifficultyStats> Diffs { get; set; }
        public float Bpm { get; set; }
        public int PlayedCount { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public float Heat { get; set; }
        public float Rating { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ScrappedSong song &&
                   Key == song.Key &&
                   Hash == song.Hash &&
                   SongName == song.SongName &&
                   SongSubName == song.SongSubName &&
                   LevelAuthorName == song.LevelAuthorName &&
                   SongAuthorName == song.SongAuthorName &&
                   EqualityComparer<List<DifficultyStats>>.Default.Equals(Diffs, song.Diffs) &&
                   Bpm == song.Bpm &&
                   PlayedCount == song.PlayedCount &&
                   Upvotes == song.Upvotes &&
                   Downvotes == song.Downvotes &&
                   Heat == song.Heat &&
                   Rating == song.Rating;
        }

        public override int GetHashCode()
        {
            var hashCode = 505786036;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Key);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Hash);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SongName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SongSubName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(LevelAuthorName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SongAuthorName);
            hashCode = hashCode * -1521134295 + EqualityComparer<List<DifficultyStats>>.Default.GetHashCode(Diffs);
            hashCode = hashCode * -1521134295 + Bpm.GetHashCode();
            hashCode = hashCode * -1521134295 + PlayedCount.GetHashCode();
            hashCode = hashCode * -1521134295 + Upvotes.GetHashCode();
            hashCode = hashCode * -1521134295 + Downvotes.GetHashCode();
            hashCode = hashCode * -1521134295 + Heat.GetHashCode();
            hashCode = hashCode * -1521134295 + Rating.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(ScrappedSong c1, ScrappedSong c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(ScrappedSong c1, ScrappedSong c2)
        {
            return !c1.Equals(c2);
        }
    }

    public struct DifficultyStats
    {
        public string Diff { get; set; }
        public int Scores { get; set; }
        public float Stars { get; set; }
        public byte Ranked { get; set; }

        public override bool Equals(object obj)
        {
            return obj is DifficultyStats stats &&
                   Diff == stats.Diff &&
                   Scores == stats.Scores &&
                   Stars == stats.Stars &&
                   Ranked == stats.Ranked;
        }

        public override int GetHashCode()
        {
            var hashCode = 342751480;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Diff);
            hashCode = hashCode * -1521134295 + Scores.GetHashCode();
            hashCode = hashCode * -1521134295 + Stars.GetHashCode();
            hashCode = hashCode * -1521134295 + Ranked.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(DifficultyStats c1, DifficultyStats c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(DifficultyStats c1, DifficultyStats c2)
        {
            return !c1.Equals(c2);
        }
    }
}
