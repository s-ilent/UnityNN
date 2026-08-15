using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class ObjectParticleInfoParser
    {
        public static ObjectParticleInfoData Parse(BinaryReaderEx reader, uint fileSize, uint baseAddr, uint headerLoc)
        {
            ObjectParticleInfoData data = new ObjectParticleInfoData();
            if (headerLoc + 8 > fileSize) return data;

            reader.JumpTo(headerLoc);
            int listPtr = reader.ReadInt32();
            int entryCount = reader.ReadInt32();

            uint listLoc = RelResolver.ResolveOffset(listPtr, fileSize, baseAddr);

            if (entryCount <= 0 || entryCount > 5000 || listLoc >= fileSize)
                return data;

            for (int i = 0; i < entryCount; i++)
            {
                if (listLoc + i * 20 + 20 > fileSize) break;
                reader.JumpTo(listLoc + i * 20);

                int pIdx = reader.ReadInt32();
                int namePtr = reader.ReadInt32();
                int filePtr = reader.ReadInt32();
                float mysteryFloat = reader.ReadSingle();
                int mysteryInt = reader.ReadInt32();

                ObjectParticleFileEntry entry = new ObjectParticleFileEntry
                {
                    ParticleIndex = pIdx,
                    MysteryFloat = mysteryFloat,
                    MysteryInt = mysteryInt
                };

                uint nameLoc = RelResolver.ResolveOffset(namePtr, fileSize, baseAddr);
                if (nameLoc > 0 && nameLoc < fileSize)
                {
                    reader.JumpTo(nameLoc);
                    entry.ParticleName = reader.ReadNullTerminatedString();
                }

                uint fileLoc = RelResolver.ResolveOffset(filePtr, fileSize, baseAddr);
                if (fileLoc > 0 && fileLoc < fileSize)
                {
                    reader.JumpTo(fileLoc);
                    entry.ParticleFileName = reader.ReadNullTerminatedString();
                }

                data.Entries.Add(entry);
            }

            return data;
        }
    }
}