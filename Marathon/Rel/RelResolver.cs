// File: Marathon/Rel/RelResolver.cs
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public class RelFormatDescriptor
    {
        public RelFileType FileType;
        public string[] FilenameKeywords;
        public Func<BinaryReaderEx, uint, uint, uint, object> Parser;
        public Func<object, bool> HasContent;
        public Action<object, GameObject, float, string, UnityEditor.AssetImporters.AssetImportContext> BuildHierarchy;
    }

    public static class RelResolver
    {
        public static readonly List<RelFormatDescriptor> RelFormats = new()
        {
            new RelFormatDescriptor
            {
                FileType = RelFileType.QuestList,
                FilenameKeywords = new[] { "filelist.rel", "filelist.xnr", "questlist" },
                Parser = (r, size, baseAddr, loc) => QuestListParser.Parse(r, baseAddr),
                HasContent = data => data is List<QuestListingData> ql && ql.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildQuestListHierarchy((List<QuestListingData>)data, root)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.FileList,
                FilenameKeywords = new[] { "scene_filelist", "obj_unit_filelist", "filelist" },
                Parser = (r, size, baseAddr, loc) => FileListParser.Parse(r, baseAddr, loc),
                HasContent = data => data is FileListData fl && fl.Categories != null && fl.Categories.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildFileListHierarchy((FileListData)data, root)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.Collision,
                FilenameKeywords = new[] { "collision", "colli", "col.rel", "col.xnr", ".nxr" },
                Parser = (r, size, baseAddr, loc) => CollisionParser.Parse(r, size, loc),
                HasContent = data => data is CollisionMeshData col && col.Vertices != null && col.Vertices.Count > 0 && col.Triangles != null && col.Triangles.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => CollisionParser.CreateUnityMeshAndColliders((CollisionMeshData)data, scale, $"{root.name}_CollisionMesh", root, ctx)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.SetLayout,
                FilenameKeywords = new[] { "set", "layout" },
                Parser = (r, size, baseAddr, loc) => SetFileParser.Parse(r, size),
                HasContent = data => data is SetFileData set && set.MapData != null && set.MapData.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildSetLayoutHierarchy((SetFileData)data, root, scale, path, ctx)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.ObjectParam,
                FilenameKeywords = new[] { "obj_param", "npc_param", "object_param" },
                Parser = (r, size, baseAddr, loc) => ObjectParamParser.Parse(r, size, baseAddr, loc),
                HasContent = data => data is ObjectParamData op && op.ObjectDefinitions != null && op.ObjectDefinitions.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildObjectParamHierarchy((ObjectParamData)data, root, scale)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.ObjectParticleInfo,
                FilenameKeywords = new[] { "obj_particle_info", "particle_info" },
                Parser = (r, size, baseAddr, loc) => ObjectParticleInfoParser.Parse(r, size, baseAddr, loc),
                HasContent = data => data is ObjectParticleInfoData part && part.Entries != null && part.Entries.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildObjectParticleInfoHierarchy((ObjectParticleInfoData)data, root)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.LndEffect,
                FilenameKeywords = new[] { "effect", "env" },
                Parser = (r, size, baseAddr, loc) => LndEffectParser.Parse(r, size),
                HasContent = data => data is LndEffectData eff && (eff.Fog != null || eff.PlayerLight1 != null || eff.SunPosition != Vector3.zero),
                BuildHierarchy = (data, root, scale, path, ctx) => BuildLndEffectHierarchy((LndEffectData)data, root)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.LndEnemyLight,
                FilenameKeywords = new[] { "enemylight", "enemy_light" },
                Parser = (r, size, baseAddr, loc) => LndEnemyLightParser.Parse(r, size),
                HasContent = data => data is LndEnemyLightData el && el.Light1 != null,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildLndEnemyLightHierarchy((LndEnemyLightData)data, root)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.FogBank,
                FilenameKeywords = new[] { "fogbank", "fog_bank" },
                Parser = (r, size, baseAddr, loc) => FogBankParser.Parse(r, baseAddr, loc),
                HasContent = data => data is List<LndFogData> fogs && fogs.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildFogBankHierarchy((List<LndFogData>)data, root)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.LndCommon,
                FilenameKeywords = new[] { "common" },
                Parser = (r, size, baseAddr, loc) => LndCommonParser.Parse(r, baseAddr),
                HasContent = data => data is LndCommonData com && (!string.IsNullOrEmpty(com.NblFilenameFragment) || com.UnknownFloat != 0f),
                BuildHierarchy = (data, root, scale, path, ctx) => BuildLndCommonHierarchy((LndCommonData)data, root)
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.StageRouteBlock,
                FilenameKeywords = new[] { "block", "route" },
                Parser = (r, size, baseAddr, loc) => StageBlockRouteParser.Parse(r, size, loc),
                HasContent = data => data is StageBlockRouteData rd && rd.Offsets != null && rd.Offsets.Count > 0,
                BuildHierarchy = null
            },
            new RelFormatDescriptor
            {
                FileType = RelFileType.EnemyLayout,
                FilenameKeywords = new[] { "enemy", "spawn" },
                Parser = (r, size, baseAddr, loc) => EnemyLayoutParser.Parse(r, baseAddr),
                HasContent = data => data is EnemyLayoutData ed && ed.Spawns != null && ed.Spawns.Count > 0,
                BuildHierarchy = (data, root, scale, path, ctx) => BuildEnemyLayoutHierarchy((EnemyLayoutData)data, root)
            }
        };

        #region Offset Resolution & Address Rebasing
        public static bool TryResolveOffset(int ptr, uint fileSize, uint baseAddr, out uint resolvedOffset)
        {
            resolvedOffset = 0;
            if (ptr <= 0) return false;
            uint uPtr = (uint)ptr;

            if (baseAddr != 0 && uPtr >= baseAddr)
            {
                uint rebased = uPtr - baseAddr;
                if (rebased < fileSize)
                {
                    resolvedOffset = rebased;
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
        #endregion

        #region Format Identification & Type Detection
        public static RelFileType IdentifyRelType(string filename, byte[] rawData)
        {
            string name = string.IsNullOrEmpty(filename) ? "" : filename.ToLowerInvariant();

            foreach (var desc in RelFormats)
            {
                foreach (string kw in desc.FilenameKeywords)
                {
                    if (name.Contains(kw))
                        return desc.FileType;
                }
            }

            return DetectRelTypeFromData(rawData);
        }

        public static RelFileType DetectRelTypeFromData(byte[] rawData)
        {
            if (rawData == null || rawData.Length < 16) return RelFileType.Unknown;

            if ((rawData[0] == 'N' && rawData[1] == 'X' && rawData[2] == 'R' && rawData[3] == 0) ||
                (rawData.Length >= 0x64 && rawData[0x60] == 'N' && rawData[0x61] == 'X' && rawData[0x62] == 'R' && rawData[0x63] == 0))
            {
                return RelFileType.Collision;
            }

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

                if ((rawSize & 0xFF000000) != 0 || headerLoc > payload.Length)
                {
                    reader.IsBigEndian = true;
                    reader.JumpTo(8);
                    headerLoc = reader.ReadUInt32();
                }

                uint baseAddr = ComputeBaseAddress(reader, headerLoc, fileSize);
                if (headerLoc + 4 > fileSize) return RelFileType.Unknown;

                foreach (var desc in RelFormats)
                {
                    try
                    {
                        reader.JumpTo(headerLoc);
                        object parsed = desc.Parser(reader, fileSize, baseAddr, headerLoc);
                        if (parsed != null && desc.HasContent(parsed))
                        {
                            return desc.FileType;
                        }
                    }
                    catch { }
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

        public static uint ComputeBaseAddress(BinaryReaderEx reader, uint headerLoc, uint fileSize)
        {
            uint baseAddr = 0;
            if (headerLoc > fileSize) return headerLoc - 0x10;

            if (headerLoc + 4 <= fileSize)
            {
                reader.JumpTo(headerLoc);
                int p1 = reader.ReadInt32();
                int p2 = reader.BaseStream.Position + 4 <= fileSize ? reader.ReadInt32() : 0;

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

                if (p1 > (int)fileSize && p1 < 0x0FFFFFFF)
                {
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
            }

            reader.Offset = baseAddr;
            return baseAddr;
        }
        #endregion

        #region Parsing Pipeline
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

                // Priority: matched descriptor first, then others
                List<RelFormatDescriptor> descriptorsToTry = new List<RelFormatDescriptor>();
                foreach (var desc in RelFormats)
                {
                    if (desc.FileType == relType)
                    {
                        descriptorsToTry.Insert(0, desc);
                    }
                    else
                    {
                        descriptorsToTry.Add(desc);
                    }
                }

                foreach (var desc in descriptorsToTry)
                {
                    try
                    {
                        reader.JumpTo(headerLoc);
                        object parsed = desc.Parser(reader, fileSize, baseAddr, headerLoc);
                        if (parsed != null && desc.HasContent(parsed))
                        {
                            relType = desc.FileType;
                            return parsed;
                        }
                    }
                    catch { }
                }

                throw new InvalidDataException($"Unable to parse REL file '{filename}': Unrecognized structure.");
            }
        }
        #endregion

        #region Unity Asset Resolution
        public static GameObject ResolveRelAsset(
            object parsedData,
            RelFileType relType,
            string assetName,
            float scale = 0.05f,
            UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            GameObject rootGO = new GameObject(assetName);

            foreach (var desc in RelFormats)
            {
                if (desc.FileType == relType && desc.BuildHierarchy != null)
                {
                    desc.BuildHierarchy(parsedData, rootGO, scale, ctx?.assetPath, ctx);
                    return rootGO;
                }
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