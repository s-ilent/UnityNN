// File: Marathon/Rel/Parsers/FileListParser.cs
using System;
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
            if (headerLoc >= fileSize) return data;

            int maxCategories = Math.Min(16, (int)((fileSize - headerLoc) / 4));
            if (maxCategories <= 0) return data;

            reader.JumpTo(headerLoc);
            uint[] topLevelPointers = new uint[maxCategories];
            for (int i = 0; i < maxCategories; i++)
            {
                if (reader.BaseStream.Position + 4 > fileSize) break;
                topLevelPointers[i] = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, baseAddr);
            }

            for (int i = 0; i < maxCategories; i++)
            {
                if (topLevelPointers[i] > 0 && topLevelPointers[i] + 8 <= fileSize)
                {
                    reader.JumpTo(topLevelPointers[i]);
                    int currListSize = reader.ReadInt32();
                    uint currListAddr = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, baseAddr);

                    if (currListSize > 0 && currListSize < 5000 && currListAddr > 0 && currListAddr + currListSize * 4 <= fileSize)
                    {
                        FileListCategoryData category = new FileListCategoryData { CategoryIndex = i };
                        uint[] stringLocs = new uint[currListSize];

                        reader.JumpTo(currListAddr);
                        for (int j = 0; j < currListSize; j++)
                        {
                            stringLocs[j] = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, baseAddr);
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