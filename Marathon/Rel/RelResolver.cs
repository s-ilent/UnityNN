// File: Marathon/Rel/RelResolver.cs
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    /// <summary>
    /// Core resolver and dispatcher for Phantasy Star Universe REL/XNR stage layout, environment,
    /// collision, and parameter binary files.
    /// </summary>
    public static class RelResolver
    {
        #region Offset Resolution & Address Rebasing
        /// <summary>
        /// Attempts to resolve a raw memory or file pointer to a valid file-relative offset.
        /// Handles base address rebasing when files contain dumped runtime memory pointers.
        /// </summary>
        public static bool TryResolveOffset(int ptr, uint fileSize, uint baseAddr, out uint resolvedOffset)
        {
            resolvedOffset = 0;
            if (ptr <= 0) return false;
            uint uPtr = (uint)ptr;

            // Rebase runtime memory address against computed base address
            if (baseAddr != 0 && uPtr >= baseAddr)
            {
                uint rebased = uPtr - baseAddr;
                if (rebased < fileSize)
                {
                    resolvedOffset = rebased;
                    return true;
                }
            }

            // Direct file-relative offset within bounds
            if (uPtr < fileSize)
            {
                resolvedOffset = uPtr;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a pointer to a file-relative offset, returning 0 if invalid or out of bounds.
        /// </summary>
        public static uint ResolveOffset(int ptr, uint fileSize, uint baseAddr = 0)
        {
            if (TryResolveOffset(ptr, fileSize, baseAddr, out uint resolved))
            {
                return resolved;
            }
            return 0;
        }
        #endregion

        #region Format Identification & Type Detection
        /// <summary>
        /// Identifies the specific REL/XNR file type from its filename or byte heuristics.
        /// </summary>
        public static RelFileType IdentifyRelType(string filename, byte[] rawData)
        {
            string name = string.IsNullOrEmpty(filename) ? "" : filename.ToLowerInvariant();

            // 1. Specific filename matches
            if (name == "filelist.rel" || name == "filelist.xnr")
                return RelFileType.QuestList;

            if (name.Contains("scene_filelist") || name.Contains("obj_unit_filelist") || name.Contains("filelist"))
                return RelFileType.FileList;

            if (name.Contains("collision") || name.Contains("colli") || 
                name.EndsWith("col.rel") || name.EndsWith("col.xnr") || name.EndsWith(".nxr"))
                return RelFileType.Collision;

            if (name.Contains("set") || name.Contains("layout"))
                return RelFileType.SetLayout;

            if (name.Contains("effect") || name.Contains("env"))
                return RelFileType.LndEffect;

            if (name.Contains("enemylight") || name.Contains("enemy_light"))
                return RelFileType.LndEnemyLight;

            if (name.Contains("fogbank") || name.Contains("fog_bank"))
                return RelFileType.FogBank;

            if (name.Contains("common"))
                return RelFileType.LndCommon;

            if (name.Contains("block") || name.Contains("route"))
                return RelFileType.StageRouteBlock;

            if (name.Contains("questlist"))
                return RelFileType.QuestList;

            if (name.StartsWith("obj_particle_info") || name.Contains("particle_info"))
                return RelFileType.ObjectParticleInfo;

            if (name.Contains("obj_param") || name.Contains("npc_param") || name.Contains("object_param"))
                return RelFileType.ObjectParam;

            if ((name.StartsWith("enemy") || name.Contains("param") || name.Contains("data") || 
                 name.Contains("drop") || name.Contains("atk") || name.Contains("spawn")) &&
                (name.EndsWith(".rel") || name.EndsWith(".xnr") || name.EndsWith(".gnr") || name.EndsWith(".znr")))
            {
                return RelFileType.EnemyLayout;
            }

            return DetectRelTypeFromData(rawData);
        }

        /// <summary>
        /// Probes raw binary bytes to infer the REL/XNR format type when the filename is ambiguous.
        /// </summary>
        public static RelFileType DetectRelTypeFromData(byte[] rawData)
        {
            if (rawData == null || rawData.Length < 16) return RelFileType.Unknown;

            // 1. Direct NXR Collision header checks
            if ((rawData[0] == 'N' && rawData[1] == 'X' && rawData[2] == 'R' && rawData[3] == 0) ||
                (rawData.Length >= 0x64 && rawData[0x60] == 'N' && rawData[0x61] == 'X' && rawData[0x62] == 'R' && rawData[0x63] == 0))
            {
                return RelFileType.Collision;
            }

            // 2. Collision 'qua\0' anchor marker search
            for (int i = 0; i < rawData.Length - 4; i++)
            {
                if (rawData[i] == 0x71 && rawData[i + 1] == 0x75 && rawData[i + 2] == 0x61 && rawData[i + 3] == 0)
                {
                    return RelFileType.Collision;
                }
            }

            byte[] payload = ExtractPayload(rawData);
            if (payload == null || payload.Length < 16) return RelFileType.Unknown;

            using (MemoryStream stream = new MemoryStream(payload))
            {
                BinaryReaderEx reader = new BinaryReaderEx(stream);
                uint fileSize = (uint)payload.Length;

                reader.JumpTo(4);
                uint rawSize = reader.ReadUInt32();
                uint headerLoc = reader.ReadUInt32();

                // Detect Big-Endian files
                if ((rawSize & 0xFF000000) != 0 || headerLoc > payload.Length)
                {
                    reader.IsBigEndian = true;
                    reader.JumpTo(8);
                    headerLoc = reader.ReadUInt32();
                }

                uint baseAddr = ComputeBaseAddress(reader, headerLoc, fileSize);
                if (headerLoc + 4 > fileSize) return RelFileType.Unknown;

                reader.JumpTo(headerLoc);

                // Check 1: 16-Category FileList (*filelist.rel)
                int maxCategories = Math.Min(16, (int)((fileSize - headerLoc) / 4));
                for (int i = 0; i < maxCategories; i++)
                {
                    if (reader.BaseStream.Position + 4 > fileSize) break;
                    int catPtr = reader.ReadInt32();

                    if (catPtr > 0 && TryResolveOffset(catPtr, fileSize, baseAddr, out uint resCatPtr) && resCatPtr + 8 <= fileSize)
                    {
                        long savedPos = reader.BaseStream.Position;
                        reader.JumpTo(resCatPtr);
                        int listSize = reader.ReadInt32();
                        int listAddr = reader.ReadInt32();
                        reader.JumpTo(savedPos);

                        if (listSize > 0 && listSize < 5000 && TryResolveOffset(listAddr, fileSize, baseAddr, out _))
                        {
                            return RelFileType.FileList;
                        }
                    }
                }

                // Check 2: ObjectParticleInfo
                reader.JumpTo(headerLoc);
                int partListPtr = reader.ReadInt32();
                int partCount = reader.ReadInt32();
                if (partCount > 0 && partCount < 1000 && TryResolveOffset(partListPtr, fileSize, baseAddr, out uint resPartPtr) && resPartPtr + 20 <= fileSize)
                {
                    reader.JumpTo(resPartPtr);
                    int testIdx = reader.ReadInt32();
                    int testNamePtr = reader.ReadInt32();
                    int testFilePtr = reader.ReadInt32();
                    if (testIdx >= 0 && testIdx < 5000 && 
                        TryResolveOffset(testNamePtr, fileSize, baseAddr, out _) && 
                        TryResolveOffset(testFilePtr, fileSize, baseAddr, out _))
                    {
                        return RelFileType.ObjectParticleInfo;
                    }
                }

                // Check 3: ObjectParam
                reader.JumpTo(headerLoc);
                int objCount = reader.ReadInt32();
                int tocPtr = reader.ReadInt32();
                if (objCount > 0 && objCount < 1000 && TryResolveOffset(tocPtr, fileSize, baseAddr, out uint resTocPtr) && resTocPtr + 8 <= fileSize)
                {
                    reader.JumpTo(resTocPtr);
                    int testObjId = reader.ReadInt32();
                    int testObjPtr = reader.ReadInt32();
                    if (testObjId >= 0 && testObjId < 10000 && TryResolveOffset(testObjPtr, fileSize, baseAddr, out _))
                    {
                        return RelFileType.ObjectParam;
                    }
                }

                // Check 4: SetLayout (Area ID + Map Count)
                reader.JumpTo(headerLoc);
                short areaId = reader.ReadInt16();
                short mapCount = reader.ReadInt16();
                int mainListPtr = reader.ReadInt32();
                if (mapCount > 0 && mapCount < 100 && TryResolveOffset(mainListPtr, fileSize, baseAddr, out uint resMainPtr) && resMainPtr + 8 <= fileSize)
                {
                    reader.JumpTo(resMainPtr);
                    reader.ReadInt16(); // mapNumber
                    short listCount = reader.ReadInt16();
                    int listPtr = reader.ReadInt32();
                    if (listCount >= 0 && listCount < 200 && TryResolveOffset(listPtr, fileSize, baseAddr, out _))
                    {
                        return RelFileType.SetLayout;
                    }
                }

                // Check 5: EnemyLayout
                reader.JumpTo(headerLoc);
                int eListPtr = reader.ReadInt32();
                int eListCount = reader.ReadInt32();
                if (eListCount > 0 && eListCount < 500 && TryResolveOffset(eListPtr, fileSize, baseAddr, out uint resEPtr) && resEPtr + 24 <= fileSize)
                {
                    return RelFileType.EnemyLayout;
                }

                // Check 6: LndEffect vs LndEnemyLight
                reader.JumpTo(headerLoc);
                int p1 = reader.ReadInt32();
                int p2 = reader.ReadInt32();
                int p3 = reader.ReadInt32();
                int p4 = reader.ReadInt32();
                if (TryResolveOffset(p1, fileSize, baseAddr, out _) && 
                    TryResolveOffset(p2, fileSize, baseAddr, out _) && 
                    TryResolveOffset(p3, fileSize, baseAddr, out _))
                {
                    return (p4 != 0 && TryResolveOffset(p4, fileSize, baseAddr, out _)) 
                        ? RelFileType.LndEffect 
                        : RelFileType.LndEnemyLight;
                }

                // Check 7: FogBank
                if (headerLoc >= 0x2C && (headerLoc - 0x10) % 28 == 0)
                {
                    return RelFileType.FogBank;
                }

                // Check 8: QuestList
                reader.JumpTo(headerLoc);
                int qListPtr = reader.ReadInt32();
                int qCount = reader.ReadInt32();
                if (qCount > 0 && qCount < 1000 && TryResolveOffset(qListPtr, fileSize, baseAddr, out uint resQPtr) && resQPtr + 8 <= fileSize)
                {
                    reader.JumpTo(resQPtr);
                    reader.ReadInt32(); // QuestNumber
                    int strPtr = reader.ReadInt32();
                    if (TryResolveOffset(strPtr, fileSize, baseAddr, out _))
                    {
                        return RelFileType.QuestList;
                    }
                }
            }

            return RelFileType.Unknown;
        }

        /// <summary>
        /// Strips outer NXIF container headers if present, returning the pure REL/NXR payload bytes.
        /// </summary>
        private static byte[] ExtractPayload(byte[] rawData)
        {
            if (rawData == null || rawData.Length < 16) return rawData;

            using (MemoryStream stream = new MemoryStream(rawData))
            {
                BinaryReaderEx reader = new BinaryReaderEx(stream);
                string signature = new string(reader.ReadChars(4));
                long containerStart = -1;

                if (signature == "NXIF" || signature.EndsWith("IF"))
                {
                    containerStart = 0;
                }
                else if (stream.Length >= 0x60)
                {
                    reader.JumpTo(0x40);
                    string innerSig = new string(reader.ReadChars(4));
                    if (innerSig == "NXIF" || innerSig.EndsWith("IF"))
                    {
                        containerStart = 0x40;
                    }
                }

                if (containerStart >= 0)
                {
                    reader.JumpTo(containerStart + 0x0C);
                    uint dataOffset = reader.ReadUInt32();
                    long chunkStart = containerStart + dataOffset;

                    if (chunkStart + 8 <= stream.Length)
                    {
                        reader.JumpTo(chunkStart + 4);
                        uint chunkSize = reader.ReadUInt32();
                        uint totalChunkSize = chunkSize + 8;

                        if (chunkStart + totalChunkSize <= stream.Length)
                        {
                            byte[] payload = new byte[totalChunkSize];
                            stream.Position = chunkStart;
                            stream.Read(payload, 0, (int)totalChunkSize);
                            return payload;
                        }
                    }
                }
            }
            return rawData;
        }

        /// <summary>
        /// Calculates the base memory address used for absolute pointer rebasing.
        /// </summary>
        public static uint ComputeBaseAddress(BinaryReaderEx reader, uint headerLoc, uint fileSize)
        {
            uint baseAddr = 0;
            if (headerLoc > fileSize) return headerLoc - 0x10;

            if (headerLoc + 4 <= fileSize)
            {
                reader.JumpTo(headerLoc);
                int p1 = reader.ReadInt32();
                int p2 = reader.BaseStream.Position + 4 <= fileSize ? reader.ReadInt32() : 0;

                // FileList probing
                int maxCat = Math.Min(16, (int)((fileSize - headerLoc) / 4));
                reader.JumpTo(headerLoc);
                int firstTopPtr = 0;
                for (int i = 0; i < maxCat; i++)
                {
                    int p = reader.ReadInt32();
                    if (firstTopPtr == 0 && p > (int)fileSize && p < 0x0FFFFFFF) firstTopPtr = p;
                }

                if (firstTopPtr != 0)
                {
                    for (int backStep = 8; backStep <= 0x200; backStep += 8)
                    {
                        int catOff = (int)headerLoc - backStep;
                        if (catOff >= 0x10)
                        {
                            uint candidate = (uint)firstTopPtr - (uint)catOff;
                            if (candidate % 16 == 0 && ((uint)firstTopPtr - candidate) < fileSize)
                            {
                                reader.JumpTo(catOff);
                                int listSize = reader.ReadInt32();
                                int listAddr = reader.ReadInt32();
                                if (listSize > 0 && listSize < 5000)
                                {
                                    uint cLoc = (uint)listAddr >= candidate ? (uint)listAddr - candidate : (uint)listAddr;
                                    if (cLoc > 0 && cLoc < fileSize)
                                    {
                                        reader.Offset = candidate;
                                        return candidate;
                                    }
                                }
                            }
                        }
                    }
                }

                // ObjectParam: count at headerLoc, TOC pointer follows
                if (p1 > 0 && p1 <= 5000 && p2 > (int)fileSize && p2 < 0x0FFFFFFF)
                {
                    int tocOff = (int)headerLoc - p1 * 8;
                    if (tocOff >= 0x10 && (uint)p2 >= (uint)tocOff)
                    {
                        uint candidate = (uint)p2 - (uint)tocOff;
                        if (candidate % 16 == 0 && ((uint)p2 - candidate) < fileSize)
                        {
                            reader.Offset = candidate;
                            return candidate;
                        }
                    }
                }

                // ObjectParticleInfo: pointer at headerLoc, count follows
                if (p2 > 0 && p2 <= 5000 && p1 > (int)fileSize && p1 < 0x0FFFFFFF)
                {
                    int listOff = (int)headerLoc - p2 * 20;
                    if (listOff >= 0x10 && (uint)p1 >= (uint)listOff)
                    {
                        uint candidate = (uint)p1 - (uint)listOff;
                        if (candidate % 16 == 0 && ((uint)p1 - candidate) < fileSize)
                        {
                            reader.Offset = candidate;
                            return candidate;
                        }
                    }
                }

                // Direct base deduction
                if (p1 > (int)fileSize && p1 < 0x0FFFFFFF)
                {
                    // Check if pointer points directly to payload start at 0x10, 0x20, 0x30, etc.
                    for (uint probeOff = 0x10; probeOff <= 0x80; probeOff += 0x10)
                    {
                        if ((uint)p1 >= probeOff)
                        {
                            uint candidate = (uint)p1 - probeOff;
                            if (candidate % 16 == 0 && ((uint)p1 - candidate) < fileSize)
                            {
                                baseAddr = candidate;
                                break;
                            }
                        }
                    }
                }
                else if (p2 > (int)fileSize && p2 < 0x0FFFFFFF)
                {
                    for (uint probeOff = 0x10; probeOff <= 0x80; probeOff += 0x10)
                    {
                        if ((uint)p2 >= probeOff)
                        {
                            uint candidate = (uint)p2 - probeOff;
                            if (candidate % 16 == 0 && ((uint)p2 - candidate) < fileSize)
                            {
                                baseAddr = candidate;
                                break;
                            }
                        }
                    }
                }
            }

            reader.Offset = baseAddr;
            return baseAddr;
        }
        #endregion

        #region Parsing Pipeline
        /// <summary>
        /// Parses raw REL/XNR bytes into strongly typed data models.
        /// </summary>
        public static object ParseRelBytes(byte[] rawData, string filename, out RelFileType relType)
        {
            relType = IdentifyRelType(filename, rawData);
            byte[] payload = ExtractPayload(rawData);

            using (MemoryStream stream = new MemoryStream(payload))
            {
                BinaryReaderEx reader = new BinaryReaderEx(stream);
                reader.JumpTo(4);
                uint fileSize = (uint)stream.Length;
                uint rawSize = reader.ReadUInt32();
                uint headerLoc = reader.ReadUInt32();

                if ((rawSize & 0xFF000000) != 0 || headerLoc > stream.Length)
                {
                    reader.IsBigEndian = true;
                    reader.JumpTo(8);
                    headerLoc = reader.ReadUInt32();
                }

                uint baseAddr = ComputeBaseAddress(reader, headerLoc, fileSize);
                if (headerLoc > fileSize) headerLoc = 0x10;

                // Priority test: try identified type first, then remaining candidates
                List<RelFileType> typesToTry = new List<RelFileType>();
                if (relType != RelFileType.Unknown)
                {
                    typesToTry.Add(relType);
                }

                foreach (RelFileType t in Enum.GetValues(typeof(RelFileType)))
                {
                    if (t != RelFileType.Unknown && !typesToTry.Contains(t))
                    {
                        typesToTry.Add(t);
                    }
                }

                foreach (RelFileType candidate in typesToTry)
                {
                    try
                    {
                        reader.JumpTo(headerLoc);
                        object parsed = ExecuteParser(candidate, reader, fileSize, baseAddr, headerLoc);
                        if (parsed != null && IsNonEmptyRelData(parsed))
                        {
                            relType = candidate;
                            return parsed;
                        }
                    }
                    catch
                    {
                        // Proceed to next candidate on failure
                    }
                }

                throw new InvalidDataException($"Unable to parse REL file '{filename}': Unrecognized structure.");
            }
        }

        private static object ExecuteParser(RelFileType type, BinaryReaderEx reader, uint fileSize, uint baseAddr, uint headerLoc)
        {
            switch (type)
            {
                case RelFileType.FileList:
                    return FileListParser.Parse(reader, baseAddr, headerLoc);
                case RelFileType.Collision:
                    return CollisionParser.Parse(reader, fileSize, headerLoc);
                case RelFileType.SetLayout:
                    return SetFileParser.Parse(reader, fileSize);
                case RelFileType.ObjectParam:
                    return ObjectParamParser.Parse(reader, fileSize, baseAddr, headerLoc);
                case RelFileType.ObjectParticleInfo:
                    return ObjectParticleInfoParser.Parse(reader, fileSize, baseAddr, headerLoc);
                case RelFileType.LndEffect:
                    return LndEffectParser.Parse(reader, fileSize);
                case RelFileType.LndEnemyLight:
                    return LndEnemyLightParser.Parse(reader, fileSize);
                case RelFileType.FogBank:
                    return FogBankParser.Parse(reader, baseAddr, headerLoc);
                case RelFileType.LndCommon:
                    return LndCommonParser.Parse(reader, baseAddr);
                case RelFileType.StageRouteBlock:
                    return StageBlockRouteParser.Parse(reader, fileSize, headerLoc);
                case RelFileType.EnemyLayout:
                    return EnemyLayoutParser.Parse(reader, baseAddr);
                case RelFileType.QuestList:
                    return QuestListParser.Parse(reader, baseAddr);
                default:
                    return null;
            }
        }

        public static bool IsNonEmptyRelData(object parsedData)
        {
            if (parsedData == null) return false;
            if (parsedData is FileListData fileListData) return fileListData.Categories != null && fileListData.Categories.Count > 0;
            if (parsedData is CollisionMeshData colData) return colData.Vertices != null && colData.Vertices.Count > 0 && colData.Triangles != null && colData.Triangles.Count > 0;
            if (parsedData is SetFileData setData) return setData.MapData != null && setData.MapData.Count > 0;
            if (parsedData is EnemyLayoutData enemyData) return enemyData.Spawns != null && enemyData.Spawns.Count > 0;
            if (parsedData is ObjectParamData paramData) return paramData.ObjectDefinitions != null && paramData.ObjectDefinitions.Count > 0;
            if (parsedData is ObjectParticleInfoData partData) return partData.Entries != null && partData.Entries.Count > 0;
            if (parsedData is LndEffectData effect)
            {
                return (effect.Fog != null && effect.Fog.FarPlane > 0f) ||
                       (effect.PlayerLight1 != null && effect.PlayerLight1.LightColor != Color.white) ||
                       effect.SunPosition != Vector3.zero;
            }
            if (parsedData is List<LndFogData> fogs) return fogs != null && fogs.Count > 0;
            if (parsedData is LndCommonData common) return !string.IsNullOrEmpty(common.NblFilenameFragment) || common.UnknownFloat != 0f;
            if (parsedData is LndEnemyLightData el) return el != null && el.Light1 != null && el.Light1.LightColor != Color.white;
            if (parsedData is List<QuestListingData> ql) return ql != null && ql.Count > 0;
            if (parsedData is StageBlockRouteData rd) return rd != null && rd.Offsets != null && rd.Offsets.Count > 0;
            return false;
        }
        #endregion

        #region Unity Asset Resolution & Hierarchy Construction
        /// <summary>
        /// Builds a representative Unity GameObject hierarchy from parsed REL data.
        /// </summary>
        public static GameObject ResolveRelAsset(
            object parsedData,
            RelFileType relType,
            string assetName,
            float scale = 0.05f,
            UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            GameObject rootGO = new GameObject(assetName);

            switch (relType)
            {
                case RelFileType.SetLayout:
                    if (parsedData is SetFileData setData)
                        BuildSetLayoutHierarchy(setData, rootGO, scale, ctx?.assetPath, ctx);
                    break;
                case RelFileType.LndEffect:
                    if (parsedData is LndEffectData effect)
                        BuildLndEffectHierarchy(effect, rootGO);
                    break;
                case RelFileType.LndEnemyLight:
                    if (parsedData is LndEnemyLightData enemyLight)
                        BuildLndEnemyLightHierarchy(enemyLight, rootGO);
                    break;
                case RelFileType.FogBank:
                    if (parsedData is List<LndFogData> fogs)
                        BuildFogBankHierarchy(fogs, rootGO);
                    break;
                case RelFileType.LndCommon:
                    if (parsedData is LndCommonData common)
                        BuildLndCommonHierarchy(common, rootGO);
                    break;
                case RelFileType.EnemyLayout:
                    if (parsedData is EnemyLayoutData enemy)
                        BuildEnemyLayoutHierarchy(enemy, rootGO);
                    break;
                case RelFileType.QuestList:
                    if (parsedData is List<QuestListingData> qList)
                        BuildQuestListHierarchy(qList, rootGO);
                    break;
                case RelFileType.FileList:
                    if (parsedData is FileListData fList)
                        BuildFileListHierarchy(fList, rootGO);
                    break;
                case RelFileType.ObjectParam:
                    if (parsedData is ObjectParamData paramData)
                        BuildObjectParamHierarchy(paramData, rootGO, scale);
                    break;
                case RelFileType.ObjectParticleInfo:
                    if (parsedData is ObjectParticleInfoData partData)
                        BuildObjectParticleInfoHierarchy(partData, rootGO);
                    break;
                case RelFileType.Collision:
                    if (parsedData is CollisionMeshData colData)
                        CollisionParser.CreateUnityMeshAndColliders(colData, scale, $"{assetName}_CollisionMesh", rootGO, ctx);
                    break;
            }

            return rootGO;
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3? localPos = null, Vector3? localEuler = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (localPos.HasValue) go.transform.localPosition = localPos.Value;
            if (localEuler.HasValue) go.transform.localEulerAngles = localEuler.Value;
            return go;
        }

        private static void BuildSetLayoutHierarchy(SetFileData data, GameObject rootGO, float scale, string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            if (data?.MapData == null) return;

            ResolvedStageContext stageCtx = !string.IsNullOrEmpty(assetPath)
                ? RelFolderResolver.ResolveAdjacentStageFiles(assetPath, ctx)
                : new ResolvedStageContext();

            foreach (var map in data.MapData)
            {
                GameObject mapGO = CreateChild(rootGO.transform, $"Map_{map.MapNumber:00}");

                foreach (var header in map.Headers)
                {
                    GameObject groupGO = CreateChild(mapGO.transform, $"Group_{header.ListIndex:00}");

                    foreach (var obj in header.Objects)
                    {
                        string defName = SetObjectDefinitions.GetDefinitionName(obj.ObjID);

                        Vector3 pos = new Vector3(-obj.Position.x * scale, obj.Position.y * scale, obj.Position.z * scale);
                        Vector3 rot = new Vector3(-obj.Rotation.x, -obj.Rotation.y, -obj.Rotation.z);

                        GameObject objGO = CreateChild(groupGO.transform, $"[Obj_{obj.ObjID:000}] {defName}", pos, rot);

                        RelObjectMetadataComponent meta = objGO.AddComponent<RelObjectMetadataComponent>();
                        meta.objID = obj.ObjID;
                        meta.objectName = defName;
                        meta.originalPosition = obj.Position;
                        meta.originalRotation = obj.Rotation;
                        meta.headerInt1 = obj.HeaderInt1;
                        meta.headerInt2 = obj.HeaderInt2;
                        meta.headerInt3 = obj.HeaderInt3;
                        meta.metadata = obj.Metadata;

                        // Link associated obj_param colliders and models
                        if (stageCtx.ObjectParams?.ObjectDefinitions.TryGetValue(obj.ObjID, out ObjectParamEntry paramEntry) == true)
                        {
                            if (paramEntry.Hitbox != null)
                            {
                                AttachHitboxCollider(objGO, paramEntry.Hitbox, scale);
                            }

                            foreach (var modelRef in paramEntry.Models)
                            {
                                GameObject modelInstance = RelFolderResolver.FindAndInstantiateModelAsset(modelRef.FileName, stageCtx.BaseDirectory);
                                if (modelInstance != null)
                                {
                                    modelInstance.transform.SetParent(objGO.transform, false);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void AttachHitboxCollider(GameObject go, ObjectHitbox hb, float scale)
        {
            if (hb.HitboxShape == 2)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(hb.UnknownFloat2 * scale * 2f, hb.UnknownFloat3 * scale * 2f, hb.UnknownFloat4 * scale * 2f);
            }
            else if (hb.HitboxShape is 0 or 1)
            {
                SphereCollider sphere = go.AddComponent<SphereCollider>();
                sphere.radius = hb.UnknownFloat6 * scale;
            }
        }

        private static void BuildObjectParamHierarchy(ObjectParamData data, GameObject rootGO, float scale)
        {
            if (data?.ObjectDefinitions == null) return;

            foreach (var kvp in data.ObjectDefinitions)
            {
                int objId = kvp.Key;
                ObjectParamEntry entry = kvp.Value;
                string defName = SetObjectDefinitions.GetDefinitionName(objId);

                GameObject objGO = CreateChild(rootGO.transform, $"[Obj_{objId:000}] {defName}");

                RelObjectParamComponent comp = objGO.AddComponent<RelObjectParamComponent>();
                comp.objID = objId;
                comp.objectName = defName;
                comp.groupOneCount = entry.GroupOneEntries.Count;
                comp.modelCount = entry.Models.Count;
                comp.animationCount = entry.Animations.Count;
                comp.particleBindingCount = entry.ParticleSoundReferences?.ParticleBindings.Count ?? 0;
                comp.soundBindingCount = entry.ParticleSoundReferences?.SoundBindings.Count ?? 0;

                if (entry.Hitbox != null)
                {
                    RelObjectHitboxComponent hitbox = objGO.AddComponent<RelObjectHitboxComponent>();
                    hitbox.hitboxShape = entry.Hitbox.HitboxShape;
                    hitbox.dimensions = new Vector3(entry.Hitbox.UnknownFloat2, entry.Hitbox.UnknownFloat3, entry.Hitbox.UnknownFloat4);
                    hitbox.radius = entry.Hitbox.UnknownFloat6;
                    hitbox.paramInt5 = entry.Hitbox.UnknownInt5;
                    hitbox.paramInt9 = entry.Hitbox.UnknownInt9;

                    AttachHitboxCollider(objGO, entry.Hitbox, scale);
                }

                if (entry.Models.Count > 0)
                {
                    GameObject modelsContainer = CreateChild(objGO.transform, "Models");
                    foreach (var m in entry.Models)
                    {
                        CreateChild(modelsContainer.transform, $"[ID_{m.Id}] {m.FileName} (Dist: {m.RenderDistance:F0})");
                    }
                }

                if (entry.Animations.Count > 0)
                {
                    GameObject animsContainer = CreateChild(objGO.transform, $"Animations ({entry.Animations.Count})");
                    for (int a = 0; a < entry.Animations.Count; a++)
                    {
                        var anim = entry.Animations[a];
                        string aName = !string.IsNullOrEmpty(anim.BoneAnimName)
                            ? anim.BoneAnimName
                            : (!string.IsNullOrEmpty(anim.TexAnimName) ? anim.TexAnimName : $"Anim_{anim.UnknownIdentifier1}");

                        CreateChild(animsContainer.transform, $"[{a:00}] {aName}");
                    }
                }

                if (entry.ParticleSoundReferences != null)
                {
                    var ps = entry.ParticleSoundReferences;
                    if (ps.ParticleBindings.Count > 0 || ps.SoundBindings.Count > 0)
                    {
                        GameObject fxContainer = CreateChild(objGO.transform, "Events & Sound");
                        foreach (var pb in ps.ParticleBindings)
                        {
                            CreateChild(fxContainer.transform, $"[FX] {pb.ParticleName} -> {pb.EventName}");
                        }
                        foreach (var sb in ps.SoundBindings)
                        {
                            CreateChild(fxContainer.transform, $"[Sound_{sb.SoundId}] {sb.EventName}");
                        }
                    }
                }
            }
        }

        private static void BuildObjectParticleInfoHierarchy(ObjectParticleInfoData data, GameObject rootGO)
        {
            if (data?.Entries == null) return;

            for (int i = 0; i < data.Entries.Count; i++)
            {
                var entry = data.Entries[i];
                GameObject pGO = CreateChild(rootGO.transform, $"[{entry.ParticleIndex:000}] {entry.ParticleName}");

                RelObjectParticleInfoComponent comp = pGO.AddComponent<RelObjectParticleInfoComponent>();
                comp.particleIndex = entry.ParticleIndex;
                comp.particleName = entry.ParticleName;
                comp.particleFileName = entry.ParticleFileName;
                comp.mysteryFloat = entry.MysteryFloat;
                comp.mysteryInt = entry.MysteryInt;
            }
        }

        private static void BuildLndEffectHierarchy(LndEffectData data, GameObject rootGO)
        {
            if (data == null) return;

            RelEnvironmentComponent env = rootGO.AddComponent<RelEnvironmentComponent>();
            env.fog = data.Fog ?? new LndFogData();
            env.playerLight1 = data.PlayerLight1 ?? new LndLightData();
            env.playerLight2 = data.PlayerLight2 ?? new LndLightData();
            env.playerLightAmbient = data.PlayerLightAmbient ?? new LndLightData();
            env.topGradient = data.TopGradient ?? new LndGradientData();
            env.bottomGradient = data.BottomGradient ?? new LndGradientData();
            env.sunPosition = data.SunPosition;

            if (data.PlayerLight1 != null) CreateLightGO("Player Light 1", data.PlayerLight1, rootGO.transform);
            if (data.PlayerLight2 != null) CreateLightGO("Player Light 2", data.PlayerLight2, rootGO.transform);

            if (data.SunPosition != Vector3.zero)
            {
                GameObject sun = CreateChild(rootGO.transform, "Sun Light");
                Light l = sun.AddComponent<Light>();
                l.type = UnityEngine.LightType.Directional;
                l.color = Color.white;
                sun.transform.forward = -data.SunPosition.normalized;
            }
        }

        private static void BuildLndEnemyLightHierarchy(LndEnemyLightData data, GameObject rootGO)
        {
            if (data == null) return;
            if (data.Light1 != null) CreateLightGO("Enemy Light 1", data.Light1, rootGO.transform);
            if (data.Light2 != null) CreateLightGO("Enemy Light 2", data.Light2, rootGO.transform);
        }

        private static void BuildFogBankHierarchy(List<LndFogData> fogs, GameObject rootGO)
        {
            if (fogs == null) return;
            for (int i = 0; i < fogs.Count; i++)
            {
                CreateChild(rootGO.transform, $"FogPreset_{i:00}");
            }
        }

        private static void BuildLndCommonHierarchy(LndCommonData data, GameObject rootGO)
        {
            if (data != null)
            {
                CreateChild(rootGO.transform, $"SceneLink_NBL_{data.NblFilenameFragment}");
            }
        }

        private static void BuildEnemyLayoutHierarchy(EnemyLayoutData data, GameObject rootGO)
        {
            if (data?.Spawns == null) return;

            for (int i = 0; i < data.Spawns.Count; i++)
            {
                GameObject waveGO = CreateChild(rootGO.transform, $"SpawnWave_{i:00}");

                foreach (var monster in data.Spawns[i])
                {
                    GameObject mGO = CreateChild(waveGO.transform, $"[Monster_{monster.MonsterNum:000}] Count_{monster.Count}");

                    RelEnemySpawnComponent comp = mGO.AddComponent<RelEnemySpawnComponent>();
                    comp.spawnIndex = i;
                    comp.monsterNum = monster.MonsterNum;
                    comp.element = monster.Element;
                    comp.count = monster.Count;
                    comp.levelModifier = monster.LevelModifier;
                }
            }
        }

        private static void BuildQuestListHierarchy(List<QuestListingData> list, GameObject rootGO)
        {
            if (list == null) return;
            foreach (var q in list)
            {
                CreateChild(rootGO.transform, $"Quest_{q.QuestNumber:000}_{q.FileName}");
            }
        }

        private static void BuildFileListHierarchy(FileListData data, GameObject rootGO)
        {
            if (data?.Categories == null) return;

            foreach (var cat in data.Categories)
            {
                GameObject catGO = CreateChild(rootGO.transform, $"Category_{cat.CategoryIndex:02d}");
                foreach (var fn in cat.FileNames)
                {
                    CreateChild(catGO.transform, fn);
                }
            }
        }

        private static GameObject CreateLightGO(string name, LndLightData lightData, Transform parent)
        {
            GameObject lGO = CreateChild(parent, name);
            Light l = lGO.AddComponent<Light>();
            l.type = UnityEngine.LightType.Directional;
            l.color = lightData?.LightColor ?? Color.white;

            if (lightData != null && lightData.Direction != Vector3.zero)
            {
                lGO.transform.forward = -lightData.Direction.normalized;
            }

            return lGO;
        }
        #endregion
    }
}