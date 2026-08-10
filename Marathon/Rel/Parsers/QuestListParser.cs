using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class QuestListParser
    {
        public static List<QuestListingData> Parse(BinaryReaderEx reader, uint baseAddr)
        {
            List<QuestListingData> list = new List<QuestListingData>();
            uint listLoc = (uint)(reader.ReadInt32() - baseAddr);
            int listCount = reader.ReadInt32();

            for (int i = 0; i < listCount; i++)
            {
                reader.JumpTo(listLoc + i * 8);
                QuestListingData q = new QuestListingData();
                q.QuestNumber = reader.ReadInt32();
                uint strLoc = (uint)(reader.ReadInt32() - baseAddr);
                reader.JumpTo(strLoc);
                q.FileName = reader.ReadNullTerminatedString();
                list.Add(q);
            }
            return list;
        }
    }
}