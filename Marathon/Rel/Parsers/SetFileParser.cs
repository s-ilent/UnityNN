using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class SetFileParser
    {
        public static SetFileData Parse(BinaryReaderEx reader, uint baseAddr)
        {
            SetFileData data = new SetFileData();
            data.AreaID = reader.ReadInt16();
            short mapCount = reader.ReadInt16();
            uint mainListPointer = (uint)(reader.ReadInt32() - baseAddr);

            for (int i = 0; i < mapCount; i++)
            {
                reader.JumpTo(mainListPointer + i * 12);
                SetMapListing map = new SetMapListing();
                map.MapNumber = reader.ReadInt16();
                short listCount = reader.ReadInt16();
                uint listPtr = (uint)(reader.ReadInt32() - baseAddr);

                for (int j = 0; j < listCount; j++)
                {
                    reader.JumpTo(listPtr + j * 0x28);
                    SetListHeader header = new SetListHeader();
                    header.UnusedInt1 = reader.ReadInt32();
                    header.BoundSphere = reader.ReadVector4();
                    header.UnusedShort1 = reader.ReadInt16();
                    header.UnknownShort1 = reader.ReadInt16();
                    header.UnusedInt2 = reader.ReadInt32();
                    header.ListIndex = reader.ReadInt16();
                    header.UnknownPairedShort1 = reader.ReadInt16();
                    header.UnknownPairedShort2 = reader.ReadInt16();
                    short listEntryCount = reader.ReadInt16();
                    uint objectListLoc = (uint)(reader.ReadInt32() - baseAddr);

                    for (int k = 0; k < listEntryCount; k++)
                    {
                        reader.JumpTo(objectListLoc + k * 0x34);
                        SetObjectEntry obj = new SetObjectEntry();
                        obj.HeaderInt1 = reader.ReadInt32();
                        obj.HeaderInt2 = reader.ReadInt32();
                        obj.HeaderInt3 = reader.ReadInt32();
                        obj.HeaderShort1 = reader.ReadInt16();
                        obj.ObjID = reader.ReadInt16();
                        obj.UnkInt1 = reader.ReadInt32();
                        obj.Position = reader.ReadVector3();
                        obj.Rotation = reader.ReadVector3();
                        int metadataLength = reader.ReadInt32();
                        uint metadataLoc = (uint)(reader.ReadInt32() - baseAddr);

                        if (metadataLength > 0 && metadataLoc < reader.BaseStream.Length)
                        {
                            reader.JumpTo(metadataLoc);
                            obj.Metadata = reader.ReadBytes(metadataLength);
                        }

                        header.Objects.Add(obj);
                    }
                    map.Headers.Add(header);
                }
                data.MapData.Add(map);
            }
            return data;
        }
    }
}