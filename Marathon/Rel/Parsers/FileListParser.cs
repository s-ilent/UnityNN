using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class FileListParser
    {
        public static FileListData Parse(BinaryReaderEx reader, uint baseAddr, uint headerLoc)
        {
            FileListData data = new FileListData();
            uint fileSize = (uint)reader.BaseStream.Length;
            if (headerLoc + 64 > fileSize) return data;

            reader.JumpTo(headerLoc);
            uint[] topLevelPointers = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                if (reader.BaseStream.Position + 4 > fileSize) break;
                topLevelPointers[i] = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);
            }

            for (int i = 0; i < 16; i++)
            {
                if (topLevelPointers[i] > 0 && topLevelPointers[i] + 8 <= fileSize)
                {
                    reader.JumpTo(topLevelPointers[i]);
                    int currListSize = reader.ReadInt32();
                    uint currListAddr = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);

                    if (currListSize > 0 && currListSize < 5000 && currListAddr > 0 && currListAddr + currListSize * 4 <= fileSize)
                    {
                        FileListCategoryData category = new FileListCategoryData { CategoryIndex = i };
                        uint[] stringLocs = new uint[currListSize];

                        reader.JumpTo(currListAddr);
                        for (int j = 0; j < currListSize; j++)
                        {
                            stringLocs[j] = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);
                        }

                        for (int j = 0; j < currListSize; j++)
                        {
                            if (stringLocs[j] > 0 && stringLocs[j] < fileSize)
                            {
                                reader.JumpTo(stringLocs[j]);
                                string fn = reader.ReadNullTerminatedString();
                                if (!string.IsNullOrEmpty(fn))
                                {
                                    category.FileNames.Add(fn);
                                }
                            }
                        }

                        if (category.FileNames.Count > 0)
                        {
                            data.Categories.Add(category);
                        }
                    }
                }
            }

            return data;
        }
    }
}