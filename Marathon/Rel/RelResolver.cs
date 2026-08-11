using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class RelResolver
    {
        public static uint ResolveOffset(int ptr, uint fileSize, uint baseAddr = 0)
        {
            if (ptr <= 0) return 0;
        
            if (baseAddr != 0 && (uint)ptr >= baseAddr)
            {
                uint resolved = (uint)(ptr - baseAddr);
                if (resolved < fileSize)
                {
                    return resolved;
                }
            }
        
            if ((uint)ptr < fileSize)
            {
                return (uint)ptr;
            }
        
            throw new System.IndexOutOfRangeException(
                $"Raw Pointer: 0x{ptr:X8} (Dec: {ptr}), " +
                $"Active Base Address: 0x{baseAddr:X8}, Resolved Offset: 0x{(ptr - (int)baseAddr):X8} is out of bounds " +
                $"for payload size of {fileSize} bytes. "
            );
        }

        public static RelFileType IdentifyRelType(string filename, byte[] rawData)
        {
            string lowerName = filename.ToLower();
            if (lowerName.Contains("set") || lowerName.Contains("layout"))
                return RelFileType.SetLayout;
            if (lowerName.Contains("effect"))
                return RelFileType.LndEffect;
            if (lowerName.Contains("enemylight") || lowerName.Contains("enemy_light"))
                return RelFileType.LndEnemyLight;
            if (lowerName.Contains("fogbank") || lowerName.Contains("fog_bank"))
                return RelFileType.FogBank;
            if (lowerName.Contains("common"))
                return RelFileType.LndCommon;
            if (lowerName.Contains("block") || lowerName.Contains("route"))
                return RelFileType.StageRouteBlock;
            if (lowerName.Contains("filelist"))
                return RelFileType.QuestList;
            if (lowerName.Contains("collision") || lowerName.Contains("colli"))
                return RelFileType.Collision;

            // Exclude non-layout parameter files from the layout importer
            if (lowerName.Contains("obj_param") || lowerName.Contains("npc_param") || lowerName.Contains("object_param"))
                return RelFileType.Unknown;

            if ((lowerName.StartsWith("enemy") || lowerName.Contains("param") || lowerName.Contains("data") || lowerName.Contains("drop") || lowerName.Contains("atk")) && (lowerName.EndsWith(".rel") || lowerName.EndsWith(".xnr")))
                return RelFileType.EnemyLayout;

            return RelFileType.Unknown;
        }

        public static object ParseRelBytes(byte[] rawData, string filename, out RelFileType relType)
        {
            relType = IdentifyRelType(filename, rawData);
            
            // 1. Extract raw NXR chunk payload if wrapped in an NXIF container
            byte[] payload = rawData;
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

                    reader.JumpTo(chunkStart + 4);
                    uint chunkSize = reader.ReadUInt32();
                    
                    // The total size is chunkSize + 8 bytes (chunkID + chunkSize fields)
                    uint totalChunkSize = chunkSize + 8; 

                    if (chunkStart + totalChunkSize <= stream.Length)
                    {
                        payload = new byte[totalChunkSize];
                        stream.Position = chunkStart;
                        stream.Read(payload, 0, (int)totalChunkSize);
                    }
                }
            }

            // 2. Parse unwrapped binary payload
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

                uint baseAddr = 0;
                if (headerLoc > stream.Length)
                {
                    baseAddr = headerLoc - 0x10;
                    headerLoc = 0x10;
                    reader.Offset = baseAddr;
                }

                reader.JumpTo(headerLoc);

                switch (relType)
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
                    default:
                        return null;
                }
            }
        }

        #region Unity Scene Builder
        public static GameObject ResolveRelAsset(object parsedData, RelFileType relType, string assetName, float scale = 0.05f)
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
                case RelFileType.Collision:
                    if (parsedData is CollisionMeshData colData)
                    {
                        Mesh mesh = CollisionParser.CreateUnityMesh(colData, scale, $"{assetName}_CollisionMesh");
                        if (mesh != null)
                        {
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