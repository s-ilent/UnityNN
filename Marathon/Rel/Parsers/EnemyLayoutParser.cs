using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class EnemyLayoutParser
    {
        public static EnemyLayoutData Parse(BinaryReaderEx reader, uint baseAddr)
        {
            EnemyLayoutData data = new EnemyLayoutData();
            uint listLoc = (uint)(reader.ReadInt32() - baseAddr);
            int listCount = reader.ReadInt32();

            reader.JumpTo(listLoc);
            for (int i = 0; i < listCount; i++)
            {
                uint spawnDataLoc = (uint)(reader.ReadInt32() - baseAddr);
                int spawnDataCount = reader.ReadInt32();
                uint arrangementLoc = (uint)(reader.ReadInt32() - baseAddr);
                int arrangementCount = reader.ReadInt32();
                uint monsterLoc = (uint)(reader.ReadInt32() - baseAddr);
                int monsterCount = reader.ReadInt32();

                long pos = reader.BaseStream.Position;
                List<EnemyMonsterEntryData> spawnMonsters = new List<EnemyMonsterEntryData>();

                for (int j = 0; j < monsterCount; j++)
                {
                    reader.JumpTo(monsterLoc + j * 8);
                    uint entryLoc = (uint)(reader.ReadInt32() - baseAddr);
                    int count = reader.ReadInt32();

                    reader.JumpTo(entryLoc);
                    for (int k = 0; k < count; k++)
                    {
                        EnemyMonsterEntryData m = new EnemyMonsterEntryData();
                        m.MonsterNum = reader.ReadInt16();
                        m.Element = reader.ReadInt16();
                        m.KingBuff = reader.ReadByte();
                        m.Buff1 = reader.ReadByte();
                        m.Buff2 = reader.ReadByte();
                        m.Buff3 = reader.ReadByte();
                        m.Buff4 = reader.ReadByte();
                        m.UnkByte1 = reader.ReadByte();
                        m.SpawnAnimation = reader.ReadInt16();
                        m.UnkShort2 = reader.ReadInt16();
                        m.SpawnDelay = reader.ReadInt16();
                        m.Count = reader.ReadInt16();
                        m.UnkShort3 = reader.ReadInt16();
                        m.UnkShort4 = reader.ReadInt16();
                        m.UnknownShort5 = reader.ReadInt16();
                        m.LevelModifier = reader.ReadInt16();
                        m.LevelCapUnused = reader.ReadInt16();
                        m.UnkShort7 = reader.ReadInt16();
                        m.UnkShort8 = reader.ReadInt16();
                        m.UnkInt1 = reader.ReadInt32();
                        spawnMonsters.Add(m);
                    }
                }
                data.Spawns.Add(spawnMonsters);
                reader.JumpTo(pos);
            }
            return data;
        }
    }
}