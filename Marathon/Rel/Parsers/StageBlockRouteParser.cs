// File: Marathon/Rel/Parsers/StageBlockRouteParser.cs
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
                if (reader.BaseStream.Position + 4 > fileSize) break;

                int rawPtr = reader.ReadInt32();
                uint resolved = RelResolver.ResolveOffset(rawPtr, fileSize, reader.Offset);
                if (resolved > 0 && resolved < fileSize)
                {
                    data.Offsets.Add((int)resolved);
                }
            }

            return data;
        }
    }
}