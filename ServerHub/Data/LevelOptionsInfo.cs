using Lidgren.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerHub.Data
{
    public struct LevelOptionsInfo : IEquatable<LevelOptionsInfo>
    {
        public BeatmapDifficulty difficulty;
        public GameplayModifiersStruct modifiers;
        public string characteristicName;

        public LevelOptionsInfo(BeatmapDifficulty difficulty, GameplayModifiers modifiers, string characteristicName)
        {
            this.difficulty = difficulty;
            this.modifiers = new GameplayModifiersStruct(modifiers);
            this.characteristicName = characteristicName;
        }

        public LevelOptionsInfo(LevelOptionsInfo original)
        {
            difficulty = original.difficulty;
            modifiers = original.modifiers;
            characteristicName = original.characteristicName;
        }

        public LevelOptionsInfo(NetIncomingMessage msg)
        {
            bool bit0 = msg.ReadBoolean();
            bool bit1 = msg.ReadBoolean();
            bool bit2 = msg.ReadBoolean();
            bool bit3 = msg.ReadBoolean();
            bool bit4 = msg.ReadBoolean();

            difficulty = (bit2 ? BeatmapDifficulty.ExpertPlus : (bit1 ? (bit0 ? BeatmapDifficulty.Expert : BeatmapDifficulty.Hard) : (bit0 ? BeatmapDifficulty.Normal : BeatmapDifficulty.Easy)));

            modifiers = new GameplayModifiersStruct();

            modifiers.songSpeed = (bit4 ? GameplayModifiers.SongSpeed.Slower : (bit3 ? GameplayModifiers.SongSpeed.Faster : GameplayModifiers.SongSpeed.Normal));

            modifiers.noFail = msg.ReadBoolean();
            modifiers.noObstacles = msg.ReadBoolean();
            modifiers.noBombs = msg.ReadBoolean();
            modifiers.noArrows = msg.ReadBoolean();
            modifiers.instaFail = msg.ReadBoolean();
            modifiers.batteryEnergy = msg.ReadBoolean();
            modifiers.disappearingArrows = msg.ReadBoolean();
            modifiers.ghostNotes = msg.ReadBoolean();

            bool bit13 = msg.ReadBoolean();
            bool bit14 = msg.ReadBoolean();
            msg.ReadBoolean();

            characteristicName = bit14 ? (!bit13 ? "OneSaber" : string.Empty) : (bit13 ? "NoArrows" : "Standard");

            if (characteristicName == string.Empty)
                characteristicName = msg.ReadString();
        }

        public void AddToMessage(NetOutgoingMessage outMsg)
        {
            outMsg.Write(difficulty == BeatmapDifficulty.Normal || difficulty == BeatmapDifficulty.Expert);
            outMsg.Write(difficulty == BeatmapDifficulty.Hard || difficulty == BeatmapDifficulty.Expert);
            outMsg.Write(difficulty == BeatmapDifficulty.ExpertPlus);

            outMsg.Write(modifiers.songSpeed == GameplayModifiers.SongSpeed.Faster);
            outMsg.Write(modifiers.songSpeed == GameplayModifiers.SongSpeed.Slower);

            outMsg.Write(modifiers.noFail);
            outMsg.Write(modifiers.noObstacles);
            outMsg.Write(modifiers.noBombs);
            outMsg.Write(modifiers.noArrows);
            outMsg.Write(modifiers.instaFail);
            outMsg.Write(modifiers.batteryEnergy);
            outMsg.Write(modifiers.disappearingArrows);
            outMsg.Write(modifiers.ghostNotes);

            bool writeCharName = false;

            switch (characteristicName)
            {
                case "Standard":
                    outMsg.Write(false);
                    outMsg.Write(false);
                    break;
                case "NoArrows":
                    outMsg.Write(false);
                    outMsg.Write(true);
                    break;
                case "OneSaber":
                    outMsg.Write(true);
                    outMsg.Write(false);
                    break;
                default:
                    outMsg.Write(true);
                    outMsg.Write(true);
                    writeCharName = true;
                    break;
            }

            outMsg.Write(false);

            if (writeCharName)
            {
                outMsg.Write(characteristicName);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is LevelOptionsInfo info &&
                   difficulty == info.difficulty &&
                   EqualityComparer<GameplayModifiersStruct>.Default.Equals(modifiers, info.modifiers) &&
                   characteristicName == info.characteristicName;
        }

        public override int GetHashCode()
        {
            var hashCode = -1809390680;
            hashCode = hashCode * -1521134295 + ((int)difficulty).GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<GameplayModifiersStruct>.Default.GetHashCode(modifiers);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(characteristicName);
            return hashCode;
        }

        public bool Equals(LevelOptionsInfo other)
        {
            return difficulty == other.difficulty &&
                      EqualityComparer<GameplayModifiersStruct>.Default.Equals(modifiers, other.modifiers) &&
                      characteristicName == other.characteristicName;
        }

        public static bool operator ==(LevelOptionsInfo c1, LevelOptionsInfo c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(LevelOptionsInfo c1, LevelOptionsInfo c2)
        {
            return !c1.Equals(c2);
        }
    }

    public struct GameplayModifiersStruct : IEquatable<GameplayModifiersStruct>
    {
        internal GameplayModifiers.SongSpeed songSpeed;
        internal bool noFail;
        internal bool noObstacles;
        internal bool noBombs;
        internal bool noArrows;
        internal bool instaFail;
        internal bool batteryEnergy;
        internal bool disappearingArrows;
        internal bool ghostNotes;

        public GameplayModifiersStruct(GameplayModifiers modifiers)
        {
            songSpeed = modifiers.songSpeed;
            noFail = modifiers.noFail;
            noObstacles = modifiers.noObstacles;
            noBombs = modifiers.noBombs;
            noArrows = modifiers.noArrows;
            instaFail = modifiers.instaFail;
            batteryEnergy = modifiers.batteryEnergy;
            disappearingArrows = modifiers.disappearingArrows;
            ghostNotes = modifiers.ghostNotes;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayModifiersStruct @struct &&
                   songSpeed == @struct.songSpeed &&
                   noFail == @struct.noFail &&
                   noObstacles == @struct.noObstacles &&
                   noBombs == @struct.noBombs &&
                   noArrows == @struct.noArrows &&
                   instaFail == @struct.instaFail &&
                   batteryEnergy == @struct.batteryEnergy &&
                   disappearingArrows == @struct.disappearingArrows &&
                   ghostNotes == @struct.ghostNotes;
        }

        public bool Equals(GameplayModifiersStruct other)
        {
            return songSpeed == other.songSpeed &&
                   noFail == other.noFail &&
                   noObstacles == other.noObstacles &&
                   noBombs == other.noBombs &&
                   noArrows == other.noArrows &&
                   instaFail == other.instaFail &&
                   batteryEnergy == other.batteryEnergy &&
                   disappearingArrows == other.disappearingArrows &&
                   ghostNotes == other.ghostNotes;
        }

        public override int GetHashCode()
        {
            var hashCode = 11229795;
            hashCode = hashCode * -1521134295 + ((int)songSpeed).GetHashCode();
            hashCode = hashCode * -1521134295 + noFail.GetHashCode();
            hashCode = hashCode * -1521134295 + noObstacles.GetHashCode();
            hashCode = hashCode * -1521134295 + noBombs.GetHashCode();
            hashCode = hashCode * -1521134295 + noArrows.GetHashCode();
            hashCode = hashCode * -1521134295 + instaFail.GetHashCode();
            hashCode = hashCode * -1521134295 + batteryEnergy.GetHashCode();
            hashCode = hashCode * -1521134295 + disappearingArrows.GetHashCode();
            hashCode = hashCode * -1521134295 + ghostNotes.GetHashCode();
            return hashCode;
        }

        public GameplayModifiers ToGameplayModifiers()
        {
            var modifiers = new GameplayModifiers();

            modifiers.songSpeed = songSpeed;
            modifiers.noFail = noFail;
            modifiers.noObstacles = noObstacles;
            modifiers.noBombs = noBombs;
            modifiers.noArrows = noArrows;
            modifiers.instaFail = instaFail;
            modifiers.batteryEnergy = batteryEnergy;
            modifiers.disappearingArrows = disappearingArrows;
            modifiers.ghostNotes = ghostNotes;

            return modifiers;
        }
    }


}
