using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class RelResolver
    {
        public static RelFileType IdentifyRelType(string filename, byte[] rawData)
        {
            string lowerName = filename.ToLower();
            if (lowerName.StartsWith("set_r") || lowerName.Contains("set_"))
                return RelFileType.SetLayout;
            if (lowerName.Equals("lndeffect.rel"))
                return RelFileType.LndEffect;
            if (lowerName.Equals("lndenemylight.rel"))
                return RelFileType.LndEnemyLight;
            if (lowerName.Equals("fogbank.rel"))
                return RelFileType.FogBank;
            if (lowerName.Equals("lndcommon.rel"))
                return RelFileType.LndCommon;
            if (lowerName.Contains("filelist.rel"))
                return RelFileType.QuestList;
            if (lowerName.StartsWith("enemy") && (lowerName.EndsWith(".rel") || lowerName.EndsWith(".xnr")))
                return RelFileType.EnemyLayout;

            return RelFileType.Unknown;
        }

        public static object ParseRelBytes(byte[] rawData, string filename, out RelFileType relType)
        {
            relType = IdentifyRelType(filename, rawData);
            using (MemoryStream stream = new MemoryStream(rawData))
            {
                BinaryReaderEx reader = new BinaryReaderEx(stream);

                long headerStartPos = 0;
                string sig = new string(reader.ReadChars(4));

                if (sig != "NXIF" && !sig.EndsWith("IF") && sig != "NXR\0")
                {
                    if (stream.Length >= 0x60)
                    {
                        reader.JumpTo(0x40);
                        headerStartPos = 0x40;
                    }
                }

                reader.JumpTo(headerStartPos + 4);
                uint fileSize = reader.ReadUInt32();
                uint headerLoc = reader.ReadUInt32();

                if ((fileSize & 0xFF000000) != 0 || headerLoc > stream.Length)
                {
                    reader.IsBigEndian = true;
                    reader.JumpTo(headerStartPos + 4);
                    fileSize = reader.ReadUInt32();
                    headerLoc = reader.ReadUInt32();
                }

                uint baseAddr = 0;
                if (headerLoc > stream.Length)
                {
                    baseAddr = headerLoc - 0x10;
                    headerLoc = 0x10;
                }

                reader.JumpTo(headerLoc);

                switch (relType)
                {
                    case RelFileType.SetLayout:
                        return SetFileParser.Parse(reader, baseAddr);
                    case RelFileType.LndEffect:
                        return LndEffectParser.Parse(reader, baseAddr);
                    case RelFileType.LndEnemyLight:
                        return LndEnemyLightParser.Parse(reader, baseAddr);
                    case RelFileType.FogBank:
                        return FogBankParser.Parse(reader, baseAddr, headerLoc);
                    case RelFileType.LndCommon:
                        return LndCommonParser.Parse(reader, baseAddr);
                    case RelFileType.EnemyLayout:
                        return EnemyLayoutParser.Parse(reader, baseAddr);
                    case RelFileType.QuestList:
                        return QuestListParser.Parse(reader, baseAddr);
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
                    BuildSetLayoutHierarchy((SetFileData)parsedData, rootGO, scale);
                    break;
                case RelFileType.LndEffect:
                    BuildLndEffectHierarchy((LndEffectData)parsedData, rootGO);
                    break;
                case RelFileType.LndEnemyLight:
                    BuildLndEnemyLightHierarchy((LndEnemyLightData)parsedData, rootGO);
                    break;
                case RelFileType.FogBank:
                    BuildFogBankHierarchy((List<LndFogData>)parsedData, rootGO);
                    break;
                case RelFileType.LndCommon:
                    BuildLndCommonHierarchy((LndCommonData)parsedData, rootGO);
                    break;
                case RelFileType.EnemyLayout:
                    BuildEnemyLayoutHierarchy((EnemyLayoutData)parsedData, rootGO);
                    break;
                case RelFileType.QuestList:
                    BuildQuestListHierarchy((List<QuestListingData>)parsedData, rootGO);
                    break;
            }

            return rootGO;
        }

        private static void BuildSetLayoutHierarchy(SetFileData data, GameObject rootGO, float scale)
        {
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
            RelEnvironmentComponent envComp = rootGO.AddComponent<RelEnvironmentComponent>();
            envComp.fog = data.Fog;
            envComp.playerLight1 = data.PlayerLight1;
            envComp.playerLight2 = data.PlayerLight2;
            envComp.playerLightAmbient = data.PlayerLightAmbient;
            envComp.topGradient = data.TopGradient;
            envComp.bottomGradient = data.BottomGradient;
            envComp.sunPosition = data.SunPosition;

            CreateLightGO("Player Light 1", data.PlayerLight1, rootGO.transform);
            CreateLightGO("Player Light 2", data.PlayerLight2, rootGO.transform);

            GameObject sunGO = new GameObject("Sun Light");
            sunGO.transform.SetParent(rootGO.transform, false);
            Light sunLight = sunGO.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = Color.white;
            if (data.SunPosition != Vector3.zero)
                sunGO.transform.forward = -data.SunPosition.normalized;
        }

        private static void BuildLndEnemyLightHierarchy(LndEnemyLightData data, GameObject rootGO)
        {
            CreateLightGO("Enemy Light 1", data.Light1, rootGO.transform);
            CreateLightGO("Enemy Light 2", data.Light2, rootGO.transform);
        }

        private static void BuildFogBankHierarchy(List<LndFogData> fogs, GameObject rootGO)
        {
            for (int i = 0; i < fogs.Count; i++)
            {
                GameObject fogGO = new GameObject($"FogPreset_{i:00}");
                fogGO.transform.SetParent(rootGO.transform, false);
            }
        }

        private static void BuildLndCommonHierarchy(LndCommonData data, GameObject rootGO)
        {
            GameObject sceneGO = new GameObject($"SceneLink_NBL_{data.NblFilenameFragment}");
            sceneGO.transform.SetParent(rootGO.transform, false);
        }

        private static void BuildEnemyLayoutHierarchy(EnemyLayoutData data, GameObject rootGO)
        {
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
            l.type = LightType.Directional;
            l.color = lightData.LightColor;
            if (lightData.Direction != Vector3.zero)
                lGO.transform.forward = -lightData.Direction.normalized;
            return lGO;
        }
        #endregion
    }
}