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
            uint listCountVal = reader.ReadUInt32();

            int listCount = (int)listCountVal;
            uint rebasedListEnd = (uint)(listCountVal - baseAddr);

            // If the value acts as a rebased list-end address, calculate actual count
            if (rebasedListEnd > listLoc && (rebasedListEnd - listLoc) <= 0x5000)
            {
                listCount = (int)((rebasedListEnd - listLoc) / 8);
            }

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