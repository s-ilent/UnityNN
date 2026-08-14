// File: Marathon/Rel/RelResolver.cs
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class RelResolver
    {
        public static bool TryResolveOffset(int ptr, uint fileSize, uint baseAddr, out uint resolvedOffset)
        {
            resolvedOffset = 0;
            if (ptr <= 0) return false;
            uint uPtr = (uint)ptr;

            if (baseAddr != 0 && uPtr >= baseAddr)
            {
                uint resolved = uPtr - baseAddr;
                if (resolved < fileSize)
                {
                    resolvedOffset = resolved;
                    return true;
                }
            }

            if (uPtr < fileSize)
            {
                resolvedOffset = uPtr;
                return true;
            }

            return false;
        }

        public static uint ResolveOffset(int ptr, uint fileSize, uint baseAddr = 0)
        {
            if (TryResolveOffset(ptr, fileSize, baseAddr, out uint resolved))
            {
                return resolved;
            }
            return 0;
        }

        public static RelFileType IdentifyRelType(string filename, byte[] rawData)
        {
            string lowerName = string.IsNullOrEmpty(filename) ? "" : filename.ToLowerInvariant();

            if (lowerName.Equals("filelist.rel") || lowerName.Equals("filelist.xnr"))
                return RelFileType.QuestList;
            if (lowerName.Contains("filelist"))
                return RelFileType.FileList;
            if (lowerName.Contains("collision") || lowerName.Contains("colli") || lowerName.EndsWith("col.rel") || lowerName.EndsWith("col.xnr"))
                return RelFileType.Collision;
            if (lowerName.Contains("set") || lowerName.Contains("layout"))
                return RelFileType.SetLayout;
            if (lowerName.Contains("effect") || lowerName.Contains("env"))
                return RelFileType.LndEffect;
            if (lowerName.Contains("enemylight") || lowerName.Contains("enemy_light"))
                return RelFileType.LndEnemyLight;
            if (lowerName.Contains("fogbank") || lowerName.Contains("fog_bank"))
                return RelFileType.FogBank;
            if (lowerName.Contains("common"))
                return RelFileType.LndCommon;
            if (lowerName.Contains("block") || lowerName.Contains("route"))
                return RelFileType.StageRouteBlock;
            if (lowerName.Contains("questlist"))
                return RelFileType.QuestList;

            // Exclude non-layout parameter files
            if (lowerName.Contains("obj_param") || lowerName.Contains("npc_param") || lowerName.Contains("object_param"))
                return RelFileType.Unknown;

            if ((lowerName.StartsWith("enemy") || lowerName.Contains("param") || lowerName.Contains("data") || lowerName.Contains("drop") || lowerName.Contains("atk") || lowerName.Contains("spawn")) &&
                (lowerName.EndsWith(".rel") || lowerName.EndsWith(".xnr") || lowerName.EndsWith(".gnr") || lowerName.EndsWith(".znr")))
                return RelFileType.EnemyLayout;

            // If filename matching returned Unknown, inspect raw binary payload
            return DetectRelTypeFromData(rawData);
        }

        public static RelFileType DetectRelTypeFromData(byte[] rawData)
        {
            if (rawData == null || rawData.Length < 16) return RelFileType.Unknown;

            // Check for primitive collision signatures ("qua\0" or "tri\0")
            for (int i = 0; i < rawData.Length - 4; i++)
            {
                if ((rawData[i] == 0x71 && rawData[i + 1] == 0x75 && rawData[i + 2] == 0x61 && rawData[i + 3] == 0x00) ||
                    (rawData[i] == 0x74 && rawData[i + 1] == 0x72 && rawData[i + 2] == 0x69 && rawData[i + 3] == 0x00))
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
                uint rawFileSizeField = reader.ReadUInt32();
                uint headerLoc = reader.ReadUInt32();

                if ((rawFileSizeField & 0xFF000000) != 0 || headerLoc > payload.Length)
                {
                    reader.IsBigEndian = true;
                    reader.JumpTo(8);
                    headerLoc = reader.ReadUInt32();
                }

                uint baseAddr = ComputeBaseAddress(reader, headerLoc, fileSize);

                if (headerLoc + 8 <= fileSize)
                {
                    reader.JumpTo(headerLoc);

                    // 1. Test 16-Category FileList (*filelist.rel)
                    int activeCatCount = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        if (headerLoc + (i + 1) * 4 > fileSize) break;
                        int catPtr = reader.ReadInt32();
                        if (catPtr > 0 && TryResolveOffset(catPtr, fileSize, baseAddr, out uint resCatPtr))
                        {
                            if (resCatPtr + 8 <= fileSize)
                            {
                                long prevPos = reader.BaseStream.Position;
                                reader.JumpTo(resCatPtr);
                                int cSize = reader.ReadInt32();
                                int cAddr = reader.ReadInt32();
                                reader.JumpTo(prevPos);

                                if (cSize > 0 && cSize < 5000 && TryResolveOffset(cAddr, fileSize, baseAddr, out _))
                                {
                                    activeCatCount++;
                                }
                            }
                        }
                    }
                    if (activeCatCount >= 1)
                    {
                        return RelFileType.FileList;
                    }

                    // 2. Test SetLayout
                    reader.JumpTo(headerLoc);
                    short areaID = reader.ReadInt16();
                    short mapCount = reader.ReadInt16();
                    int mainListPtr = reader.ReadInt32();

                    if (mapCount > 0 && mapCount < 100 && TryResolveOffset(mainListPtr, fileSize, baseAddr, out uint resMainListPtr))
                    {
                        if (resMainListPtr + 8 <= fileSize)
                        {
                            reader.JumpTo(resMainListPtr);
                            reader.ReadInt16(); // mapNumber
                            short listCount = reader.ReadInt16();
                            int listPtr = reader.ReadInt32();
                            if (listCount >= 0 && listCount < 200 && TryResolveOffset(listPtr, fileSize, baseAddr, out _))
                            {
                                return RelFileType.SetLayout;
                            }
                        }
                    }

                    // 3. Test EnemyLayout
                    reader.JumpTo(headerLoc);
                    int eListPtr = reader.ReadInt32();
                    int eListCount = reader.ReadInt32();

                    if (eListCount > 0 && eListCount < 500 && TryResolveOffset(eListPtr, fileSize, baseAddr, out uint resEListPtr))
                    {
                        if (resEListPtr + 24 <= fileSize)
                        {
                            return RelFileType.EnemyLayout;
                        }
                    }

                    // 4. Test LndEffect vs LndEnemyLight
                    reader.JumpTo(headerLoc);
                    int p1 = reader.ReadInt32();
                    int p2 = reader.ReadInt32();
                    int p3 = reader.ReadInt32();
                    int p4 = reader.ReadInt32();

                    if (TryResolveOffset(p1, fileSize, baseAddr, out _) &&
                        TryResolveOffset(p2, fileSize, baseAddr, out _) &&
                        TryResolveOffset(p3, fileSize, baseAddr, out _))
                    {
                        if (p4 != 0 && TryResolveOffset(p4, fileSize, baseAddr, out _))
                            return RelFileType.LndEffect;
                        else
                            return RelFileType.LndEnemyLight;
                    }

                    // 5. Test FogBank
                    if (headerLoc >= 0x2C && (headerLoc - 0x10) % 28 == 0)
                    {
                        return RelFileType.FogBank;
                    }

                    // 6. Test QuestList
                    reader.JumpTo(headerLoc);
                    int qListPtr = reader.ReadInt32();
                    int qCount = reader.ReadInt32();
                    if (qCount > 0 && qCount < 1000 && TryResolveOffset(qListPtr, fileSize, baseAddr, out uint resQListPtr))
                    {
                        if (resQListPtr + 8 <= fileSize)
                        {
                            reader.JumpTo(resQListPtr);
                            reader.ReadInt32(); // QuestNumber
                            int strPtr = reader.ReadInt32();
                            if (TryResolveOffset(strPtr, fileSize, baseAddr, out _))
                            {
                                return RelFileType.QuestList;
                            }
                        }
                    }
                }
            }

            return RelFileType.Unknown;
        }

        private static byte[] ExtractPayload(byte[] rawData)
        {
            if (rawData == null || rawData.Length < 16) return rawData;

            using (MemoryStream stream = new MemoryStream(rawData))
            {
                BinaryReaderEx reader = new BinaryReaderEx(stream);
                string sig = new string(reader.ReadChars(4));
                long containerStart = -1;

                if (sig == "NXIF" || sig.EndsWith("IF"))
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

        private static uint ComputeBaseAddress(BinaryReaderEx reader, uint headerLoc, uint fileSize)
        {
            uint baseAddr = 0;
            if (headerLoc > fileSize)
            {
                baseAddr = headerLoc - 0x10;
            }
            else if (headerLoc + 8 <= fileSize)
            {
                reader.JumpTo(headerLoc);
                int ptrVal1 = reader.ReadInt32();
                int ptrVal2 = reader.ReadInt32();

                if (ptrVal1 > (int)fileSize && ptrVal1 < 0x0FFFFFFF)
                {
                    uint candidateBase = (uint)ptrVal1 - 0x10;
                    if (candidateBase % 16 == 0 && (uint)ptrVal1 >= candidateBase && ((uint)ptrVal1 - candidateBase) < fileSize)
                    {
                        baseAddr = candidateBase;
                    }
                }
                else if (ptrVal2 > (int)fileSize && ptrVal2 < 0x0FFFFFFF)
                {
                    uint candidateBase = (uint)ptrVal2 - 0x10;
                    if (candidateBase % 16 == 0 && (uint)ptrVal2 >= candidateBase && ((uint)ptrVal2 - candidateBase) < fileSize)
                    {
                        baseAddr = candidateBase;
                    }
                }
            }
            reader.Offset = baseAddr;
            return baseAddr;
        }

        public static object ParseRelBytes(byte[] rawData, string filename, out RelFileType relType)
        {
            relType = IdentifyRelType(filename, rawData);

            byte[] payload = ExtractPayload(rawData);

            using (MemoryStream stream = new MemoryStream(payload))
            {
                BinaryReaderEx reader = new BinaryReaderEx(stream);
                reader.JumpTo(4);
                uint fileSize = (uint)stream.Length;
                uint rawFileSizeField = reader.ReadUInt32();
                uint headerLoc = reader.ReadUInt32();

                if ((rawFileSizeField & 0xFF000000) != 0 || headerLoc > stream.Length)
                {
                    reader.IsBigEndian = true;
                    reader.JumpTo(8);
                    headerLoc = reader.ReadUInt32();
                }

                uint baseAddr = ComputeBaseAddress(reader, headerLoc, fileSize);

                if (headerLoc > fileSize)
                {
                    headerLoc = 0x10;
                }

                reader.JumpTo(headerLoc);

                List<RelFileType> typesToTry = new List<RelFileType>();
                if (relType != RelFileType.Unknown)
                {
                    typesToTry.Add(relType);
                }

                foreach (RelFileType t in System.Enum.GetValues(typeof(RelFileType)))
                {
                    if (t != RelFileType.Unknown && !typesToTry.Contains(t))
                    {
                        typesToTry.Add(t);
                    }
                }

                foreach (RelFileType candidateType in typesToTry)
                {
                    try
                    {
                        reader.JumpTo(headerLoc);
                        object parsed = ExecuteParser(candidateType, reader, fileSize, baseAddr, headerLoc);
                        if (parsed != null && IsNonEmptyRelData(parsed))
                        {
                            relType = candidateType;
                            return parsed;
                        }
                    }
                    catch (System.Exception)
                    {
                        // Try next candidate parser
                    }
                }

                throw new System.IO.InvalidDataException($"Unable to parse REL file '{filename}': Unrecognized or corrupted format structure.");
            }
        }

        private static object ExecuteParser(RelFileType type, BinaryReaderEx reader, uint fileSize, uint baseAddr, uint headerLoc)
        {
            switch (type)
            {
                case RelFileType.SetLayout:
                    return SetFileParser.Parse(reader, fileSize);
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
                case RelFileType.Collision:
                    return CollisionParser.Parse(reader, fileSize, headerLoc);
                case RelFileType.FileList:
                    return FileListParser.Parse(reader, baseAddr, headerLoc);
                default:
                    return null;
            }
        }

        public static bool IsNonEmptyRelData(object parsedData)
        {
            if (parsedData == null) return false;
            if (parsedData is SetFileData setData) return setData.MapData != null && setData.MapData.Count > 0;
            if (parsedData is CollisionMeshData colData) return colData.Vertices != null && colData.Vertices.Count > 0;
            if (parsedData is EnemyLayoutData enemyData) return enemyData.Spawns != null && enemyData.Spawns.Count > 0;
            if (parsedData is FileListData fileListData) return fileListData.Categories != null && fileListData.Categories.Count > 0;
            if (parsedData is LndEffectData effectData) return effectData != null;
            if (parsedData is List<LndFogData> fogs) return fogs != null && fogs.Count > 0;
            if (parsedData is LndCommonData commonData) return commonData != null;
            if (parsedData is LndEnemyLightData enemyLight) return enemyLight != null;
            if (parsedData is List<QuestListingData> questList) return questList != null && questList.Count > 0;
            if (parsedData is StageBlockRouteData routeData) return routeData != null && routeData.Offsets.Count > 0;
            return true;
        }

        #region Unity Scene Builder
        public static GameObject ResolveRelAsset(object parsedData, RelFileType relType, string assetName, float scale = 0.05f, UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            GameObject rootGO = new GameObject(assetName);

            switch (relType)
            {
                case RelFileType.SetLayout:
                    if (parsedData is SetFileData setData) BuildSetLayoutHierarchy(setData, rootGO, scale);
                    break;
                case RelFileType.LndEffect:
                    if (parsedData is LndEffectData effectData) BuildLndEffectHierarchy(effectData, rootGO);
                    break;
                case RelFileType.LndEnemyLight:
                    if (parsedData is LndEnemyLightData enemyLightData) BuildLndEnemyLightHierarchy(enemyLightData, rootGO);
                    break;
                case RelFileType.FogBank:
                    if (parsedData is List<LndFogData> fogs) BuildFogBankHierarchy(fogs, rootGO);
                    break;
                case RelFileType.LndCommon:
                    if (parsedData is LndCommonData commonData) BuildLndCommonHierarchy(commonData, rootGO);
                    break;
                case RelFileType.EnemyLayout:
                    if (parsedData is EnemyLayoutData enemyData) BuildEnemyLayoutHierarchy(enemyData, rootGO);
                    break;
                case RelFileType.QuestList:
                    if (parsedData is List<QuestListingData> qList) BuildQuestListHierarchy(qList, rootGO);
                    break;
                case RelFileType.FileList:
                    if (parsedData is FileListData fList) BuildFileListHierarchy(fList, rootGO);
                    break;
                case RelFileType.Collision:
                    if (parsedData is CollisionMeshData colData)
                    {
                        Mesh mesh = CollisionParser.CreateUnityMeshAndColliders(colData, scale, $"{assetName}_CollisionMesh", rootGO);
                        if (mesh != null)
                        {
                            if (ctx != null)
                            {
                                ctx.AddObjectToAsset("CollisionMesh", mesh);
                            }
                            MeshFilter mf = rootGO.AddComponent<MeshFilter>();
                            mf.sharedMesh = mesh;
                            MeshCollider mc = rootGO.AddComponent<MeshCollider>();
                            mc.sharedMesh = mesh;
                        }
                    }
                    break;
            }

            return rootGO;
        }

        private static void BuildSetLayoutHierarchy(SetFileData data, GameObject rootGO, float scale)
        {
            if (data == null || data.MapData == null) return;
            foreach (var map in data.MapData)
            {
                GameObject mapGO = new GameObject($"Map_{map.MapNumber:00}");
                mapGO.transform.SetParent(rootGO.transform, false);

                for (int h = 0; h < map.Headers.Count; h++)
                {
                    var header = map.Headers[h];
                    GameObject groupGO = new GameObject($"Group_{header.ListIndex:00}");
                    groupGO.transform.SetParent(mapGO.transform, false);

                    foreach (var obj in header.Objects)
                    {
                        string defName = SetObjectDefinitions.GetDefinitionName(obj.ObjID);
                        GameObject objGO = new GameObject($"[Obj_{obj.ObjID:000}] {defName}");
                        objGO.transform.SetParent(groupGO.transform, false);

                        Vector3 pos = obj.Position;
                        pos.x *= -1f * scale;
                        pos.y *= scale;
                        pos.z *= scale;

                        objGO.transform.localPosition = pos;
                        objGO.transform.localEulerAngles = new Vector3(-obj.Rotation.x, -obj.Rotation.y, -obj.Rotation.z);

                        RelObjectMetadataComponent metaComp = objGO.AddComponent<RelObjectMetadataComponent>();
                        metaComp.objID = obj.ObjID;
                        metaComp.objectName = defName;
                        metaComp.originalPosition = obj.Position;
                        metaComp.originalRotation = obj.Rotation;
                        metaComp.headerInt1 = obj.HeaderInt1;
                        metaComp.headerInt2 = obj.HeaderInt2;
                        metaComp.headerInt3 = obj.HeaderInt3;
                        metaComp.metadata = obj.Metadata;
                    }
                }
            }
        }

        private static void BuildLndEffectHierarchy(LndEffectData data, GameObject rootGO)
        {
            if (data == null) return;
            RelEnvironmentComponent envComp = rootGO.AddComponent<RelEnvironmentComponent>();
            envComp.fog = data.Fog ?? new LndFogData();
            envComp.playerLight1 = data.PlayerLight1 ?? new LndLightData();
            envComp.playerLight2 = data.PlayerLight2 ?? new LndLightData();
            envComp.playerLightAmbient = data.PlayerLightAmbient ?? new LndLightData();
            envComp.topGradient = data.TopGradient ?? new LndGradientData();
            envComp.bottomGradient = data.BottomGradient ?? new LndGradientData();
            envComp.sunPosition = data.SunPosition;

            if (data.PlayerLight1 != null) CreateLightGO("Player Light 1", data.PlayerLight1, rootGO.transform);
            if (data.PlayerLight2 != null) CreateLightGO("Player Light 2", data.PlayerLight2, rootGO.transform);

            GameObject sunGO = new GameObject("Sun Light");
            sunGO.transform.SetParent(rootGO.transform, false);
            Light sunLight = sunGO.AddComponent<Light>();
            sunLight.type = UnityEngine.LightType.Directional;
            sunLight.color = Color.white;
            if (data.SunPosition != Vector3.zero)
                sunGO.transform.forward = -data.SunPosition.normalized;
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
                GameObject fogGO = new GameObject($"FogPreset_{i:00}");
                fogGO.transform.SetParent(rootGO.transform, false);
            }
        }

        private static void BuildLndCommonHierarchy(LndCommonData data, GameObject rootGO)
        {
            if (data == null) return;
            GameObject sceneGO = new GameObject($"SceneLink_NBL_{data.NblFilenameFragment}");
            sceneGO.transform.SetParent(rootGO.transform, false);
        }

        private static void BuildEnemyLayoutHierarchy(EnemyLayoutData data, GameObject rootGO)
        {
            if (data == null || data.Spawns == null) return;
            for (int i = 0; i < data.Spawns.Count; i++)
            {
                GameObject spawnGO = new GameObject($"SpawnWave_{i:00}");
                spawnGO.transform.SetParent(rootGO.transform, false);

                foreach (var monster in data.Spawns[i])
                {
                    GameObject mGO = new GameObject($"[Monster_{monster.MonsterNum:000}] Count_{monster.Count}");
                    mGO.transform.SetParent(spawnGO.transform, false);

                    RelEnemySpawnComponent sComp = mGO.AddComponent<RelEnemySpawnComponent>();
                    sComp.spawnIndex = i;
                    sComp.monsterNum = monster.MonsterNum;
                    sComp.element = monster.Element;
                    sComp.count = monster.Count;
                    sComp.levelModifier = monster.LevelModifier;
                }
            }
        }

        private static void BuildQuestListHierarchy(List<QuestListingData> list, GameObject rootGO)
        {
            if (list == null) return;
            foreach (var q in list)
            {
                GameObject qGO = new GameObject($"Quest_{q.QuestNumber:000}_{q.FileName}");
                qGO.transform.SetParent(rootGO.transform, false);
            }
        }

        private static void BuildFileListHierarchy(FileListData data, GameObject rootGO)
        {
            if (data == null || data.Categories == null) return;
            foreach (var cat in data.Categories)
            {
                GameObject catGO = new GameObject($"Category_{cat.CategoryIndex:02d}");
                catGO.transform.SetParent(rootGO.transform, false);

                foreach (var fn in cat.FileNames)
                {
                    GameObject fileGO = new GameObject(fn);
                    fileGO.transform.SetParent(catGO.transform, false);
                }
            }
        }

        private static GameObject CreateLightGO(string name, LndLightData lightData, Transform parent)
        {
            GameObject lGO = new GameObject(name);
            lGO.transform.SetParent(parent, false);
            Light l = lGO.AddComponent<Light>();
            l.type = UnityEngine.LightType.Directional;
            l.color = lightData != null ? lightData.LightColor : Color.white;
            if (lightData != null && lightData.Direction != Vector3.zero)
                lGO.transform.forward = -lightData.Direction.normalized;
            return lGO;
        }
        #endregion
    }
}