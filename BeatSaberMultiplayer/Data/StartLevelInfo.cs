using BeatSaberMultiplayer.Misc;
using Lidgren.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer.Data
{
    public class StartLevelInfo
    {
        public BeatmapDifficulty difficulty;
        public GameplayModifiers modifiers;
        public string characteristicName;

        public StartLevelInfo(BeatmapDifficulty difficulty, GameplayModifiers modifiers, string characteristicName)
        {
            this.difficulty = difficulty;
            this.modifiers = modifiers;
            this.characteristicName = characteristicName;
        }

        public StartLevelInfo(NetIncomingMessage msg)
        {
            byte[] modifiersBytes = msg.ReadBytes(2);

            BitArray modifiersBits = new BitArray(modifiersBytes);

            //Difficulty
            //Easy      = 000
            //Normal    = 001
            //Hard      = 010
            //Expert    = 011
            //Expert+   = 100
            difficulty = (modifiersBits[2] ? BeatmapDifficulty.ExpertPlus : (modifiersBits[1] ? (modifiersBits[0] ? BeatmapDifficulty.Expert : BeatmapDifficulty.Hard) : (modifiersBits[0] ? BeatmapDifficulty.Normal : BeatmapDifficulty.Easy)));

            modifiers = new GameplayModifiers();

            //Song speed modifier
            //Normal speed = 00
            //Faster speed = 01
            //Slower Speed = 10
            //Reserved =     11
            modifiers.songSpeed = (modifiersBits[4] ? GameplayModifiers.SongSpeed.Slower : (modifiersBits[3] ? GameplayModifiers.SongSpeed.Faster : GameplayModifiers.SongSpeed.Normal));

            //Other modifiers
            modifiers.noFail = modifiersBits[5];
            modifiers.noObstacles = modifiersBits[6];
            modifiers.noBombs = modifiersBits[7];
            modifiers.noArrows = modifiersBits[8];
            modifiers.instaFail = modifiersBits[9];
            modifiers.batteryEnergy = modifiersBits[10];
            modifiers.disappearingArrows = modifiersBits[11];
            modifiers.ghostNotes = modifiersBits[12];

            //Beatmap characteristic
            //Standard = 00
            //No arrows = 01
            //One saber = 10
            //Reserved = 11
            characteristicName = modifiersBits[14] ? "OneSaber" : (modifiersBits[13] ? "NoArrows" : "Standard");
        }

        public byte[] ToBytes()
        {
            BitArray modifiersBits = new BitArray(16);

            //Difficulty
            //Easy      = 000
            //Normal    = 001
            //Hard      = 010
            //Expert    = 011
            //Expert+   = 100
            modifiersBits[0] = difficulty == BeatmapDifficulty.Normal || difficulty == BeatmapDifficulty.Expert; //First bit
            modifiersBits[1] = difficulty == BeatmapDifficulty.Hard || difficulty == BeatmapDifficulty.Expert; //Second bit
            modifiersBits[2] = difficulty == BeatmapDifficulty.ExpertPlus; //Third bit

            //Song speed modifier
            //Normal speed = 00
            //Faster speed = 01
            //Slower Speed = 10
            //Reserved =     11
            modifiersBits[3] = modifiers.songSpeed == GameplayModifiers.SongSpeed.Faster; //First bit
            modifiersBits[4] = modifiers.songSpeed == GameplayModifiers.SongSpeed.Slower; //Second bit

            //Other modifiers
            modifiersBits[5] = modifiers.noFail;
            modifiersBits[6] = modifiers.noObstacles;
            modifiersBits[7] = modifiers.noBombs;
            modifiersBits[8] = modifiers.noArrows;
            modifiersBits[9] = modifiers.instaFail;
            modifiersBits[10] = modifiers.batteryEnergy;
            modifiersBits[11] = modifiers.disappearingArrows;
            modifiersBits[12] = modifiers.ghostNotes;


            //Beatmap characteristic
            //Standard = 00
            //No arrows = 01
            //One saber = 10
            //Reserved = 11
            modifiersBits[13] = characteristicName == "NoArrows"; //First bit
            modifiersBits[14] = characteristicName == "OneSaber"; //Second bit

            //Reserved
            modifiersBits[15] = false;

            return modifiersBits.ToBytes();
        }

        public void AddToMessage(NetOutgoingMessage outMsg)
        {
            outMsg.Write(ToBytes());
        }


    }
}
