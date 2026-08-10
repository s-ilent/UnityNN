using System;
using Marathon.IO;

namespace SilentTools
{
    public static class SetFileParser
    {
        public static SetFileData Parse(BinaryReaderEx reader, uint fileSize)
        {
            SetFileData data = new SetFileData();
            data.AreaID = reader.ReadInt16();
            short mapCount = reader.ReadInt16();
            uint mainListPointer = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize);

            for (int i = 0; i < mapCount; i++)
            {
                if (mainListPointer + i * 12 >= fileSize) break;
                reader.JumpTo(mainListPointer + i * 12);
                SetMapListing map = new SetMapListing();
                map.MapNumber = reader.ReadInt16();
                short listCount = reader.ReadInt16();
                uint listPtr = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize);

                for (int j = 0; j < listCount; j++)
                {
                    if (listPtr + j * 0x28 >= fileSize) break;
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
                    uint objectListLoc = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize);

                    for (int k = 0; k < listEntryCount; k++)
                    {
                        if (objectListLoc + k * 0x34 >= fileSize) break;
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
                        uint metadataLoc = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize);

                        if (metadataLength > 0 && metadataLoc < reader.BaseStream.Length)
                        {
                            reader.JumpTo(metadataLoc);
                            obj.Metadata = reader.ReadBytes(Math.Min(metadataLength, (int)(reader.BaseStream.Length - metadataLoc)));
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