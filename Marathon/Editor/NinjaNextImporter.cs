// File: Marathon/Editor/NinjaNextImporter.cs
using UnityNN;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Marathon.Formats.Mesh.Ninja;
using Marathon.Formats.Archive;
using Marathon.Formats.Particle;

namespace UnityNN.Editor
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

    [Serializable]
    public class MaterialRemapEntry
    {
        public int slotIndex;
        public string originalName = "";
        public Material overrideMaterial;
    }

    [Serializable]
    public class TextureRemapEntry
    {
        public int textureIndex;
        public string originalFileName = "";
        public Texture2D overrideTexture;
    }

    [Serializable]
    public class NinjaImportSettings
    {
        // Mesh & Geometry
        public float Scale = 0.10f;
        public MeshImportMode MeshImportMode = MeshImportMode.CombinedByNode;
        public bool GenerateMeshColliders = false;

        // Materials
        public bool ImportMaterials = true;
        public MaterialLocation MaterialLocation = MaterialLocation.EmbedInPrefab;
        public MaterialSearch MaterialSearch = MaterialSearch.RecursiveSubFolder;
        public MaterialNaming MaterialNaming = MaterialNaming.ByMaterialName;
        public string MaterialSearchPath = "Assets/Materials";
        public List<MaterialRemapEntry> MaterialRemaps = new List<MaterialRemapEntry>();

        // Textures
        public string[] TextureSearchPaths = Array.Empty<string>();
        public List<TextureRemapEntry> TextureRemaps = new List<TextureRemapEntry>();

        // Animation
        public bool ImportAnimation = true;
        public bool GenerateAnimatorController = false;
        public string[] NodeHierarchyTarget = Array.Empty<string>();

        public static NinjaImportSettings Default => new NinjaImportSettings();
    }

    [ScriptedImporter(3, new[] {
        // Xbox / PC formats
        "xno", "xna", "xnj", "xnm", "xnv", "xnt", "xnn", "xnc", "xnl", "xnd", "xng", "xne", "xni", "xnf", "xnr", 
        // GameCube / Wii formats
        "gno", "gna", "gnj", "gnm", "gnv", "gnt", "gnn", "gnc", "gnl", "gnr", "gbl",
        // PS2 / PSP formats
        "zno", "znm", "znt", "znn", "znr", "zbl",
        // PSU-specific formats
        "rel", "nbl","dat"
    })]
    public class NinjaNextImporter : ScriptedImporter
    {
        [Header("Mesh Settings")]
        public float m_Scale = 0.10f;
        public MeshImportMode m_MeshImportMode = MeshImportMode.CombinedByNode;
        public bool m_GenerateMeshColliders = false;

        [Header("Material Settings")]
        public bool m_ImportMaterials = true;
        public MaterialLocation m_MaterialLocation = MaterialLocation.EmbedInPrefab;
        public MaterialSearch m_MaterialSearch = MaterialSearch.RecursiveSubFolder;
        public MaterialNaming m_MaterialNaming = MaterialNaming.ByMaterialName;
        public string m_MaterialSearchPath = "Assets/Materials";
        public List<MaterialRemapEntry> m_MaterialRemaps = new List<MaterialRemapEntry>();

        [Header("Texture Search Paths (Ordered by Priority)")]
        public string[] m_TextureSearchPaths = Array.Empty<string>();

        [Header("Texture Remap Settings (XNT)")]
        public List<TextureRemapEntry> m_TextureRemaps = new List<TextureRemapEntry>();

        [Header("Animation Settings")]
        public bool m_ImportAnimation = true;
        public bool m_GenerateAnimatorController = false;
        public string[] m_NodeHierarchyTarget;

        public NinjaImportSettings GetSettings() => new NinjaImportSettings
        {
            Scale = m_Scale,
            MeshImportMode = m_MeshImportMode,
            GenerateMeshColliders = m_GenerateMeshColliders,
            ImportMaterials = m_ImportMaterials,
            MaterialLocation = m_MaterialLocation,
            MaterialSearch = m_MaterialSearch,
            MaterialNaming = m_MaterialNaming,
            MaterialSearchPath = m_MaterialSearchPath,
            MaterialRemaps = m_MaterialRemaps,
            TextureSearchPaths = m_TextureSearchPaths ?? Array.Empty<string>(),
            TextureRemaps = m_TextureRemaps,
            ImportAnimation = m_ImportAnimation,
            GenerateAnimatorController = m_GenerateAnimatorController,
            NodeHierarchyTarget = m_NodeHierarchyTarget ?? Array.Empty<string>()
        };

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string ext = Path.GetExtension(ctx.assetPath).ToLowerInvariant();
            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            Texture2D icon = NinjaIconResolver.GetIconForExtension(ext);
            NinjaImportSettings settings = GetSettings();

            switch (ext)
            {
                case ".nbl":
                case ".gbl":
                case ".zbl":
                    ImportArchive(ctx, assetName, ext, icon);
                    break;

                case ".dat":
                    ImportParticleEffect(ctx, assetName, settings, icon);
                    break;

                case ".rel":
                case ".xnr":
                case ".gnr":
                case ".znr":
                    ImportRelStage(ctx, assetName, settings, icon);
                    break;

                default:
                    ImportNinjaAsset(ctx, assetName, ext, settings, icon);
                    break;
            }
        }

        #region Format Importers

        private void ImportArchive(AssetImportContext ctx, string assetName, string ext, Texture2D icon)
        {
            try
            {
                using (FileStream fs = File.OpenRead(ctx.assetPath))
                {
                    NblArchive nbl = NblArchive.Load(fs);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"NBL Archive: {assetName}{ext}");
                    sb.AppendLine($"Chunks: {nbl.Chunks.Count} | Total Files: {nbl.Entries.Count}\n");

                    for (int c = 0; c < nbl.Chunks.Count; c++)
                    {
                        var ch = nbl.Chunks[c];
                        sb.AppendLine($"Chunk [{c}] {ch.ChunkID} (v0x{ch.FileVersion:X4}): {ch.Entries.Count} files");
                        for (int e = 0; e < ch.Entries.Count; e++)
                        {
                            var entry = ch.Entries[e];
                            sb.AppendLine($"  - [{e:000}] {entry.Header.FileName ?? "<unnamed>"} ({entry.Header.FileSize} bytes)");
                        }
                    }

                    TextAsset summaryAsset = new TextAsset(sb.ToString());
                    ctx.AddObjectToAsset("main", summaryAsset, icon);
                    ctx.SetMainObject(summaryAsset);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaNextImporter] Failed to load archive {ctx.assetPath}:\n{ex}");
            }
        }

        private void ImportParticleEffect(AssetImportContext ctx, string assetName, NinjaImportSettings settings, Texture2D icon)
        {
            try
            {
                using (FileStream fs = File.OpenRead(ctx.assetPath))
                {
                    ParticleEffectFile particleFile = new ParticleEffectFile();
                    particleFile.Load(fs);

                    if (particleFile.IsValid)
                    {
                        GameObject effectRoot = ParticleEffectResolver.ResolveParticleEffect(particleFile, assetName, settings.Scale, ctx, settings);
                        if (effectRoot != null)
                        {
                            ctx.AddObjectToAsset("main", effectRoot, icon);
                            ctx.SetMainObject(effectRoot);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaNextImporter] Failed to load particle effect {ctx.assetPath}:\n{ex}");
            }
        }

        private void ImportRelStage(AssetImportContext ctx, string assetName, NinjaImportSettings settings, Texture2D icon)
        {
            try
            {
                byte[] rawData = File.ReadAllBytes(ctx.assetPath);
                object parsedRel = RelResolver.ParseRelBytes(rawData, Path.GetFileName(ctx.assetPath), out RelFileType relType);

                if (parsedRel != null)
                {
                    GameObject relRoot = RelResolver.ResolveRelAsset(parsedRel, relType, assetName, settings.Scale, ctx);
                    if (relRoot != null)
                    {
                        ctx.AddObjectToAsset("main", relRoot, icon);
                        ctx.SetMainObject(relRoot);
                        return;
                    }
                }

                Debug.LogError($"[NinjaNextImporter] Failed to parse REL/XNR file {ctx.assetPath}: No valid layout or collision data generated.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaNextImporter] Failed to parse REL/XNR file {ctx.assetPath}:\n{ex}");
            }
        }

        private void ImportNinjaAsset(AssetImportContext ctx, string assetName, string ext, NinjaImportSettings settings, Texture2D icon)
        {
            NinjaNext loader = new NinjaNext();
            try
            {
                loader.Load(ctx.assetPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NinjaNextImporter] Failed to load NinjaNext file {ctx.assetPath}:\n{ex}");
                return;
            }

            if (loader.Data == null) return;

            // Apply texture overrides if an XNT TextureList exists
            ApplyTextureOverrides(loader.Data.TextureList, settings.TextureRemaps);

            // 1. Standalone Motion / Animation Assets (.xnm, .xnv, .gnm, .znm) without 3D mesh
            bool isStandaloneMotion = (ext is ".xnm" or ".xnv" or ".gnm" or ".gnv" or ".znm") && loader.Data.Object == null;
            if (isStandaloneMotion)
            {
                NinjaMotion mot = loader.Data.Motion ?? loader.Data.MaterialMotion;
                if (mot != null)
                {
                    NinjaObject associatedObj = null;
                    string[] targets = (settings.NodeHierarchyTarget != null && settings.NodeHierarchyTarget.Length > 0)
                        ? settings.NodeHierarchyTarget
                        : NinjaMotionResolver.ResolveNodeHierarchyTargets(ctx.assetPath, ctx, out associatedObj);
            
                    AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                        mot,
                        assetName,
                        settings.Scale,
                        targets,
                        settings.MeshImportMode,
                        associatedObj
                    );
            
                    if (clip != null)
                    {
                        ctx.AddObjectToAsset("main", clip, icon);
                        ctx.SetMainObject(clip);
                        return;
                    }
                }
            }

            // 2. 3D Model Construction (.xnj, .xno, .xna, .gno, .zno)
            GameObject rootGO = null;
            List<Transform> nodeTransforms = new List<Transform>();

            if (loader.Data.Object != null)
            {
                rootGO = NinjaObjectResolver.ResolveObject(
                    loader.Data.Object,
                    loader.Data.TextureList,
                    assetName,
                    ctx,
                    settings,
                    out nodeTransforms
                );
            }

            // 3. Camera / Light Objects (.xnc, .xnl, etc.)
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

            // 4. Animation Setup & Controller Resolution (includes embedded XNJ motions)
            if (rootGO != null)
            {
                if (settings.ImportAnimation)
                {
                    NinjaAnimatorResolver.SetupModelAnimations(
                        loader,
                        rootGO,
                        nodeTransforms,
                        assetName,
                        ctx.assetPath,
                        settings,
                        ctx
                    );
                }

                ctx.AddObjectToAsset("main", rootGO, icon);
                ctx.SetMainObject(rootGO);
                return;
            }

            // 5. Non-instantiable Support / Metadata Assets (.xnt, .xnn, etc.)
            TextAsset textAsset = CreateSummaryTextAsset(loader.Data, assetName, ext);
            ctx.AddObjectToAsset("main", textAsset, icon);
            ctx.SetMainObject(textAsset);
        }

        #endregion

        #region Helpers

        private void ApplyTextureOverrides(NinjaTextureList texList, List<TextureRemapEntry> remaps)
        {
            if (texList?.NinjaTextureFiles == null || remaps == null || remaps.Count == 0) return;

            foreach (var remap in remaps)
            {
                if (remap.overrideTexture != null && remap.textureIndex >= 0 && remap.textureIndex < texList.NinjaTextureFiles.Count)
                {
                    string overridePath = AssetDatabase.GetAssetPath(remap.overrideTexture);
                    if (!string.IsNullOrEmpty(overridePath))
                    {
                        texList.NinjaTextureFiles[remap.textureIndex].FileName = Path.GetFileName(overridePath);
                    }
                }
            }
        }

        private static TextAsset CreateSummaryTextAsset(NinjaNext.FormatData data, string assetName, string extension)
        {
            StringBuilder sb = new StringBuilder();

            if (data?.TextureList?.NinjaTextureFiles != null)
            {
                sb.AppendLine($"Ninja Texture List ({assetName}{extension})");
                sb.AppendLine($"Textures ({data.TextureList.NinjaTextureFiles.Count}):");
                for (int i = 0; i < data.TextureList.NinjaTextureFiles.Count; i++)
                {
                    var tf = data.TextureList.NinjaTextureFiles[i];
                    sb.AppendLine($"  [{i:00}] {tf.FileName} (GlobalIndex: {tf.GlobalIndex}, Bank: {tf.Bank})");
                }
            }
            else if (data?.NodeNameList?.NinjaNodeNames != null)
            {
                sb.AppendLine($"Ninja Node Name List ({assetName}{extension})");
                sb.AppendLine($"Names ({data.NodeNameList.NinjaNodeNames.Count}):");
                for (int i = 0; i < data.NodeNameList.NinjaNodeNames.Count; i++)
                {
                    sb.AppendLine($"  [{i:0000}] {data.NodeNameList.NinjaNodeNames[i]}");
                }
            }
            else if (data?.EffectList != null)
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

        #endregion
    }
}