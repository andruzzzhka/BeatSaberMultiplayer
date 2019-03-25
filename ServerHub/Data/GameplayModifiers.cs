namespace ServerHub.Data
{
    public class GameplayModifiers
    {
        public bool noFail;
        public bool instaFail;
        public bool noObstacles;
        public bool noArrows;
        public bool noBombs;
        public bool batteryEnergy;
        public bool disappearingArrows;
        public bool ghostNotes;
        public GameplayModifiers.SongSpeed songSpeed;
        
        public enum SongSpeed
        {
            Normal,
            Faster,
            Slower
        }
    }
}