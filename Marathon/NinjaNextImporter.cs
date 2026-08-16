// File: Marathon/NinjaNextImporter.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Marathon.Formats.Mesh.Ninja;
using Marathon.Formats.Archive;

namespace SilentTools
{
    public enum MeshImportMode
    {
        [InspectorName("Combined Meshes by Node (Multi-Material)")]
        CombinedByNode = 0,

        [InspectorName("Single Skinned Mesh (Unified Skeleton)")]
        SingleSkinnedMesh = 1,

        [InspectorName("Individual Sub-Objects (Legacy Hierarchy)")]
        IndividualSubObjects = 2
    }

    [ScriptedImporter(1, new[] {
        // Xbox / PC formats
        "xno", "xna", "xnj", "xnm", "xnv", "xnt", "xnn", "xnc", "xnl", "xnd", "xng", "xne", "xni", "xnf", "xnr", "rel", "nbl",
        // GameCube / Wii formats
        "gno", "gna", "gnj", "gnm", "gnv", "gnt", "gnn", "gnc", "gnl", "gnr", "gbl",
        // PS2 / PSP formats
        "zno", "znm", "znt", "znn", "znr", "zbl"
    })]
    public class NinjaNextImporter : ScriptedImporter
    {
        [Header("Mesh Settings")]
        public float m_Scale = 0.10f;
        public MeshImportMode m_MeshImportMode = MeshImportMode.CombinedByNode;

        [Header("Material Settings")]
        public bool m_ImportMaterials = true;
        public MaterialLocation m_MaterialLocation = MaterialLocation.EmbedInPrefab;
        public MaterialSearch m_MaterialSearch = MaterialSearch.RecursiveSubFolder;
        public MaterialNaming m_MaterialNaming = MaterialNaming.ByMaterialName;
        public string m_MaterialSearchPath = "Assets/Materials";

        [Header("Animation Settings")]
        public bool m_ImportAnimation = true;
        public string[] m_NodeHierarchyTarget;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string ext = Path.GetExtension(ctx.assetPath).ToLowerInvariant();
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            Texture2D icon = NinjaIconResolver.GetIconForExtension(ext);

            // -----------------------------------------------------------------
            // 1. REL / XNR Stage Layout & Environment Files (.rel, .xnr, .gnr, .znr)
            // -----------------------------------------------------------------
            if (ext == ".rel" || ext == ".xnr" || ext == ".gnr" || ext == ".znr")
            {
                byte[] rawData;
                try
                {
                    rawData = File.ReadAllBytes(ctx.assetPath);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to read REL/XNR file {ctx.assetPath}:\n{ex}");
                    return;
                }

                try
                {
                    RelFileType relType;
                    object parsedRel = RelResolver.ParseRelBytes(rawData, Path.GetFileName(ctx.assetPath), out relType);

                    if (parsedRel != null)
                    {
                        GameObject relRoot = RelResolver.ResolveRelAsset(parsedRel, relType, assetName, m_Scale, ctx);
                        if (relRoot != null)
                        {
                            ctx.AddObjectToAsset("main", relRoot, icon);
                            ctx.SetMainObject(relRoot);
                            return;
                        }
                    }

                    Debug.LogError($"Failed to parse REL/XNR file {ctx.assetPath}: File produced no valid layout, collision, or environment data.");
                    return;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to parse REL/XNR file {ctx.assetPath}:\n{ex}");
                    return;
                }
            }

            // -----------------------------------------------------------------
            // 2. Core Binary Loader (.xno, .xna, .xnj, .xnm, .xnt, .nbl, etc.)
            // -----------------------------------------------------------------
            NinjaNext loader = new NinjaNext();
            try
            {
                if (ext == ".nbl" || ext == ".gbl" || ext == ".zbl")
                {
                    using (FileStream fs = File.OpenRead(ctx.assetPath))
                    {
                        NblArchive nbl = NblArchive.Load(fs);
                        loader.Data = nbl.ToFormatData();
                    }
                }
                else
                {
                    loader.Load(ctx.assetPath);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load NinjaNext file {ctx.assetPath}:\n{ex}");
                return;
            }

            // -----------------------------------------------------------------
            // 3. Standalone Motion / Animation Assets (.xnm, .xnv, .gnm, .znm)
            // -----------------------------------------------------------------
            bool isStandaloneMotion = (ext == ".xnm" || ext == ".xnv" || ext == ".gnm" || ext == ".gnv" || ext == ".znm") 
                                      && loader.Data.Object == null;

            if (isStandaloneMotion)
            {
                NinjaMotion mot = loader.Data.Motion ?? loader.Data.MaterialMotion;
                if (mot != null)
                {
                    string[] targets = (m_NodeHierarchyTarget != null && m_NodeHierarchyTarget.Length > 0)
                        ? m_NodeHierarchyTarget
                        : NinjaMotionResolver.ResolveNodeHierarchyTargets(ctx.assetPath, ctx);

                    AnimationClip clip = NinjaMotionResolver.ResolveMotion(mot, assetName, m_Scale, targets, m_MeshImportMode);
                    if (clip != null)
                    {
                        ctx.AddObjectToAsset("main", clip, icon);
                        ctx.SetMainObject(clip);
                        return;
                    }
                }
            }

            // -----------------------------------------------------------------
            // 4. Model & Hierarchy Assets (.xno, .xna, .xnj, .nbl, etc.)
            // -----------------------------------------------------------------
            GameObject rootGO = null;
            List<Transform> nodeTransforms = new List<Transform>();

            if (loader.Data.Object != null)
            {
                rootGO = NinjaObjectResolver.ResolveObject(
                    loader.Data.Object,
                    loader.Data.TextureList,
                    assetName,
                    ctx,
                    m_Scale,
                    m_MeshImportMode,
                    m_ImportMaterials,
                    m_MaterialLocation,
                    m_MaterialSearch,
                    m_MaterialNaming,
                    m_MaterialSearchPath,
                    out nodeTransforms
                );
            }

            // -----------------------------------------------------------------
            // 5. Camera / Light Objects (.xnc, .xnl, etc.)
            // -----------------------------------------------------------------
            if (rootGO == null && loader.Data.Camera != null)
            {
                rootGO = new GameObject(assetName);
                rootGO.AddComponent<Camera>();
            }
            if (rootGO == null && loader.Data.Light != null)
            {
                rootGO = new GameObject(assetName);
                Light lightComp = rootGO.AddComponent<Light>();
                lightComp.type = UnityEngine.LightType.Directional;
            }

            // -----------------------------------------------------------------
            // 6. Animation Resolution (Animator & obj_param Animation Support)
            // -----------------------------------------------------------------
            if (rootGO != null && m_ImportAnimation)
            {
                List<AnimationClip> loadedClips = new List<AnimationClip>();
                HashSet<string> loadedClipNames = new HashSet<string>();

                // A. Embedded & Adjacent Matching Motions
                NinjaMotion nodeMotion = loader.Data.Motion;
                NinjaMotion matMotion = loader.Data.MaterialMotion;

                NinjaMotionResolver.ResolveLinkedMotions(ctx.assetPath, ctx, out NinjaMotion extraNodeMot, out NinjaMotion extraMatMot, out _, out _);
                if (nodeMotion == null) nodeMotion = extraNodeMot;
                if (matMotion == null) matMotion = extraMatMot;

                if (nodeMotion != null)
                {
                    AnimationClip nodeClip = NinjaMotionResolver.ResolveMotion(nodeMotion, $"{assetName}_Animation", m_Scale, rootGO, nodeTransforms, m_MeshImportMode);
                    if (nodeClip != null && loadedClipNames.Add(nodeClip.name))
                    {
                        ctx.AddObjectToAsset("NodeAnimation", nodeClip);
                        loadedClips.Add(nodeClip);
                    }
                }

                if (matMotion != null)
                {
                    AnimationClip matClip = NinjaMotionResolver.ResolveMotion(matMotion, $"{assetName}_MaterialAnimation", m_Scale, rootGO, nodeTransforms, m_MeshImportMode);
                    if (matClip != null && loadedClipNames.Add(matClip.name))
                    {
                        ctx.AddObjectToAsset("MaterialAnimation", matClip);
                        loadedClips.Add(matClip);
                    }
                }

                // B. obj_param.xnr Associated Animations Resolution
                ResolvedStageContext stageContext = RelFolderResolver.ResolveAdjacentStageFiles(ctx.assetPath, ctx);
                var matchedParam = RelFolderResolver.FindParamEntryForModel(stageContext.ObjectParams, assetName);

                if (matchedParam.HasValue)
                {
                    int objId = matchedParam.Value.Key;
                    ObjectParamEntry paramEntry = matchedParam.Value.Value;

                    RelObjectAnimationComponent animMeta = rootGO.AddComponent<RelObjectAnimationComponent>();
                    animMeta.objID = objId;

                    for (int a = 0; a < paramEntry.Animations.Count; a++)
                    {
                        var aRef = paramEntry.Animations[a];
                        ObjectAnimationEntryData entryData = new ObjectAnimationEntryData
                        {
                            id1 = aRef.UnknownIdentifier1,
                            id2 = aRef.UnknownIdentifier2,
                            boneAnimName = aRef.BoneAnimName,
                            texAnimName = aRef.TexAnimName,
                            paramFloat1 = aRef.UnknownFloat1,
                            paramFloat2 = aRef.UnknownFloat2,
                            paramFloat3 = aRef.UnknownFloat3,
                            paramFloat4 = aRef.UnknownFloat4,
                            paramFloat5 = aRef.UnknownFloat5,
                            paramFloat6 = aRef.UnknownFloat6
                        };

                        // 1. Resolve Bone Animation from obj_param
                        if (!string.IsNullOrEmpty(aRef.BoneAnimName))
                        {
                            string boneAnimPath = RelFolderResolver.FindAnimationFilePath(aRef.BoneAnimName, stageContext.BaseDirectory, false);
                            if (!string.IsNullOrEmpty(boneAnimPath))
                            {
                                try
                                {
                                    NinjaNext animLoader = new NinjaNext();
                                    animLoader.Load(boneAnimPath);
                                    if (animLoader.Data.Motion != null)
                                    {
                                        ctx.DependsOnSourceAsset(boneAnimPath);
                                        string clipId = $"Anim_{a}_{aRef.BoneAnimName}";
                                        AnimationClip paramClip = NinjaMotionResolver.ResolveMotion(animLoader.Data.Motion, clipId, m_Scale, rootGO, nodeTransforms, m_MeshImportMode);
                                        if (paramClip != null)
                                        {
                                            if (loadedClipNames.Add(paramClip.name))
                                            {
                                                ctx.AddObjectToAsset(clipId, paramClip);
                                                loadedClips.Add(paramClip);
                                            }
                                            entryData.boneClip = paramClip;
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogWarning($"Could not load obj_param bone anim {boneAnimPath}: {ex.Message}");
                                }
                            }
                        }

                        // 2. Resolve Texture / Material Animation from obj_param
                        if (!string.IsNullOrEmpty(aRef.TexAnimName))
                        {
                            string texAnimPath = RelFolderResolver.FindAnimationFilePath(aRef.TexAnimName, stageContext.BaseDirectory, true);
                            if (!string.IsNullOrEmpty(texAnimPath))
                            {
                                try
                                {
                                    NinjaNext animLoader = new NinjaNext();
                                    animLoader.Load(texAnimPath);
                                    NinjaMotion foundMatMot = animLoader.Data.MaterialMotion ?? animLoader.Data.Motion;
                                    if (foundMatMot != null)
                                    {
                                        ctx.DependsOnSourceAsset(texAnimPath);
                                        string clipId = $"MatAnim_{a}_{aRef.TexAnimName}";
                                        AnimationClip paramMatClip = NinjaMotionResolver.ResolveMotion(foundMatMot, clipId, m_Scale, rootGO, nodeTransforms, m_MeshImportMode);
                                        if (paramMatClip != null)
                                        {
                                            if (loadedClipNames.Add(paramMatClip.name))
                                            {
                                                ctx.AddObjectToAsset(clipId, paramMatClip);
                                                loadedClips.Add(paramMatClip);
                                            }
                                            entryData.materialClip = paramMatClip;
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogWarning($"Could not load obj_param material anim {texAnimPath}: {ex.Message}");
                                }
                            }
                        }

                        animMeta.animations.Add(entryData);
                    }
                }

                // C. Attach Animator Component (without creating an AnimatorController)
                if (loadedClips.Count > 0)
                {
                    rootGO.AddComponent<Animator>();
                }

                ctx.AddObjectToAsset("main", rootGO, icon);
                ctx.SetMainObject(rootGO);
                return;
            }

            if (rootGO != null)
            {
                ctx.AddObjectToAsset("main", rootGO, icon);
                ctx.SetMainObject(rootGO);
                return;
            }

            // -----------------------------------------------------------------
            // 7. Non-instantiable Support / Metadata Assets (.xnt, .xnn, etc.)
            // -----------------------------------------------------------------
            TextAsset textAsset = CreateSummaryTextAsset(loader.Data, assetName, ext);
            ctx.AddObjectToAsset("main", textAsset, icon);
            ctx.SetMainObject(textAsset);
        }

        private static TextAsset CreateSummaryTextAsset(NinjaNext.FormatData data, string assetName, string extension)
        {
            StringBuilder sb = new StringBuilder();

            if (data.TextureList != null && data.TextureList.NinjaTextureFiles != null)
            {
                sb.AppendLine($"Ninja Texture List ({assetName}{extension})");
                sb.AppendLine($"Textures ({data.TextureList.NinjaTextureFiles.Count}):");
                for (int i = 0; i < data.TextureList.NinjaTextureFiles.Count; i++)
                {
                    var tf = data.TextureList.NinjaTextureFiles[i];
                    sb.AppendLine($"  [{i:00}] {tf.FileName} (GlobalIndex: {tf.GlobalIndex}, Bank: {tf.Bank})");
                }
            }
            else if (data.NodeNameList != null && data.NodeNameList.NinjaNodeNames != null)
            {
                sb.AppendLine($"Ninja Node Name List ({assetName}{extension})");
                sb.AppendLine($"Names ({data.NodeNameList.NinjaNodeNames.Count}):");
                for (int i = 0; i < data.NodeNameList.NinjaNodeNames.Count; i++)
                {
                    sb.AppendLine($"  [{i:0000}] {data.NodeNameList.NinjaNodeNames[i]}");
                }
            }
            else if (data.EffectList != null)
            {
                sb.AppendLine($"Ninja Effect List ({assetName}{extension})");
                sb.AppendLine($"Effects ({data.EffectList.NinjaEffectFiles?.Count ?? 0}):");
                if (data.EffectList.NinjaEffectFiles != null)
                {
                    for (int i = 0; i < data.EffectList.NinjaEffectFiles.Count; i++)
                    {
                        sb.AppendLine($"  [{i:00}] {data.EffectList.NinjaEffectFiles[i].FileName}");
                    }
                }
            }
            else
            {
                sb.AppendLine($"Ninja Next Support Asset: {assetName}{extension}");
            }

            return new TextAsset(sb.ToString());
        }
    }
}