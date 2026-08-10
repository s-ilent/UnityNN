using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class FogBankParser
    {
        public static List<LndFogData> Parse(BinaryReaderEx reader, uint baseAddr, uint headerLoc)
        {
            List<LndFogData> list = new List<LndFogData>();
            int count = (int)((headerLoc - 0x10) / 28);
            uint[] locs = new uint[count];
            for (int i = 0; i < count; i++) locs[i] = (uint)(reader.ReadInt32() - baseAddr);
            for (int i = 0; i < count; i++)
            {
                reader.JumpTo(locs[i]);
                list.Add(LndEffectParser.ReadFog(reader));
            }
            return list;
        }
    }
}