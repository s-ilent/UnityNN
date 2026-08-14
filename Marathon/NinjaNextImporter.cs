using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    [ScriptedImporter(1, new[] {
        // Xbox / PC formats
        "xno", "xna", "xnj", "xnm", "xnv", "xnt", "xnn", "xnc", "xnl", "xnd", "xng", "xne", "xni", "xnf", "xnr", "rel",
        // GameCube / Wii formats
        "gno", "gna", "gnj", "gnm", "gnv", "gnt", "gnn", "gnc", "gnl", "gnr",
        // PS2 / PSP formats
        "zno", "znm", "znt", "znn", "znr"
    })]
    public class NinjaNextImporter : ScriptedImporter
    {
        [Header("Import Settings")]
        public float m_Scale = 0.10f;

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

                RelFileType relType;
                object parsedRel = RelResolver.ParseRelBytes(rawData, Path.GetFileName(ctx.assetPath), out relType);

                if (parsedRel != null)
                {
                    GameObject relRoot = RelResolver.ResolveRelAsset(parsedRel, relType, assetName, m_Scale);
                    ctx.AddObjectToAsset("main", relRoot, icon);
                    ctx.SetMainObject(relRoot);
                    return;
                }
            }

            // -----------------------------------------------------------------
            // 2. Core Binary Loader (.xno, .xna, .xnj, .xnm, .xnt, etc.)
            // -----------------------------------------------------------------
            NinjaNext loader = new NinjaNext();
            try
            {
                loader.Load(ctx.assetPath);
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

                    AnimationClip clip = NinjaMotionResolver.ResolveMotion(mot, assetName, m_Scale, targets);
                    if (clip != null)
                    {
                        ctx.AddObjectToAsset("main", clip, icon);
                        ctx.SetMainObject(clip);
                        return;
                    }
                }
            }

            // -----------------------------------------------------------------
            // 4. Model & Hierarchy Assets (.xno, .xna, .xnj, etc.)
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
            // 6. Embedded / Linked Animation Resolution for Model Packages
            // -----------------------------------------------------------------
            if (rootGO != null)
            {
                if (m_ImportAnimation)
                {
                    NinjaMotion nodeMotion = loader.Data.Motion;
                    NinjaMotion matMotion = loader.Data.MaterialMotion;

                    NinjaMotionResolver.ResolveLinkedMotions(ctx.assetPath, ctx, out NinjaMotion extraNodeMot, out NinjaMotion extraMatMot, out _, out _);
                    if (nodeMotion == null) nodeMotion = extraNodeMot;
                    if (matMotion == null) matMotion = extraMatMot;

                    if (nodeMotion != null)
                    {
                        AnimationClip nodeClip = NinjaMotionResolver.ResolveMotion(nodeMotion, $"{assetName}_Animation", m_Scale, rootGO, nodeTransforms);
                        if (nodeClip != null) ctx.AddObjectToAsset("NodeAnimation", nodeClip);
                    }

                    if (matMotion != null)
                    {
                        AnimationClip matClip = NinjaMotionResolver.ResolveMotion(matMotion, $"{assetName}_MaterialAnimation", m_Scale, rootGO, nodeTransforms);
                        if (matClip != null) ctx.AddObjectToAsset("MaterialAnimation", matClip);
                    }

                    if (nodeMotion != null || matMotion != null)
                    {
                        rootGO.AddComponent<Animator>();
                    }
                }

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