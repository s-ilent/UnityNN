using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public class StageBlockRouteData
    {
        public List<int> Offsets { get; set; } = new List<int>();
        public int EntryCount { get; set; }
    }

    public static class StageBlockRouteParser
    {
        public static StageBlockRouteData Parse(BinaryReaderEx reader, uint fileSize, uint headerLoc)
        {
            StageBlockRouteData data = new StageBlockRouteData();
            if (headerLoc >= fileSize) return data;

            reader.JumpTo(headerLoc);
            int count = (int)((fileSize - headerLoc) / 4);
            data.EntryCount = count;

            for (int i = 0; i < count; i++)
            {
                int val = reader.ReadInt32();
                data.Offsets.Add((int)RelResolver.ResolveOffset(val, fileSize, reader.Offset));
            }

            return data;
        }
    }
}