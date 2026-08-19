// File: Marathon/Rel/Parsers/QuestListParser.cs
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class QuestListParser
    {
        public static List<QuestListingData> Parse(BinaryReaderEx reader, uint baseAddr)
        {
            List<QuestListingData> list = new List<QuestListingData>();
            uint fileSize = (uint)reader.BaseStream.Length;
            if (fileSize < 8) return list;

            int rawListLoc = reader.ReadInt32();
            uint listCountVal = reader.ReadUInt32();

            if (!RelResolver.TryResolveOffset(rawListLoc, fileSize, baseAddr, out uint listLoc))
                return list;

            int listCount = (int)listCountVal;
            if (baseAddr != 0 && listCountVal >= baseAddr)
            {
                uint rebasedEnd = listCountVal - baseAddr;
                if (rebasedEnd > listLoc && (rebasedEnd - listLoc) <= 0x5000)
                {
                    listCount = (int)((rebasedEnd - listLoc) / 8);
                }
            }

            if (listCount <= 0 || listCount > 1000 || listLoc + listCount * 8 > fileSize)
                return list;

            for (int i = 0; i < listCount; i++)
            {
                if (listLoc + i * 8 + 8 > fileSize) break;

                reader.JumpTo(listLoc + i * 8);
                int qNum = reader.ReadInt32();
                int strRawPtr = reader.ReadInt32();

                if (RelResolver.TryResolveOffset(strRawPtr, fileSize, baseAddr, out uint strLoc) && strLoc < fileSize)
                {
                    reader.JumpTo(strLoc);
                    string fileName = reader.ReadNullTerminatedString();
                    list.Add(new QuestListingData { QuestNumber = qNum, FileName = fileName });
                }
            }

            return list;
        }
    }
}