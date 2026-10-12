using System.Collections.Generic;

namespace Xv2CoreLib.ValuesDictionary
{
    public static class BCM
    {
        // BCM_Entry.I_18, low 4 bits.
        public static Dictionary<uint, string> OpponentSize { get; private set; } = new Dictionary<uint, string>()
        {
            { 0x0, "All sizes" },
            { 0x2, "Small characters" },
            { 0x4, "Default size" },
            { 0x5, "Medium" },
            { 0x6, "Medium Large" },
            { 0x7, "Large" },
            { 0x8, "Great Ape" }
        };

        // Named values need that BAC BcmCallback cancel bit to be open.
        public static Dictionary<uint, string> MoveCategory { get; private set; } = new Dictionary<uint, string>()
        {
            { 0, "0" },
            { 1, "1: Combos (BcmCallback 0x100)" },
            { 2, "2: Supers (BcmCallback 0x200)" },
            { 3, "3: Ultimates/Evasives (BcmCallback 0x400)" },
            { 4, "4: Ultimates/Evasives (BcmCallback 0x400)" },
            { 5, "5" },
            { 6, "6" },
            { 7, "7: Z-Vanish (BcmCallback 0x800)" },
            { 8, "8: Ki Blasts (BcmCallback 0x1000)" },
            { 9, "9: Back Hits (BcmCallback 0x80)" },
            { 10, "10: Combos (BcmCallback 0x100)" },
            { 11, "11: Ultimates/Evasives (BcmCallback 0x400)" },
            { 12, "12" }
        };

        public static Dictionary<uint, string> CharacterCondition { get; private set; } = new Dictionary<uint, string>()
        {
            { 0, "None / Default" },
            { 1, "Custom Character (CAC)" },
            { 2, "Human Male (HUM)" },
            { 3, "Human Female (HUF)" },
            { 4, "Saiyan Male (SYM)" },
            { 5, "Saiyan Female (SYF)" },
            { 6, "Namekian (NMC)" },
            { 7, "Frieza Race (FRI)" },
            { 8, "Majin Male (MAM)" },
            { 9, "Majin Female (MAF)" }
        };
    }
}
