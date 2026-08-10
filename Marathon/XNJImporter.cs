using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "xnj")]
    public class XNJImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        [Header("Import Settings")]
        public float m_Scale = 0.05f;

        [Header("Material Settings")]
        public bool m_ImportMaterials = true;
        public MaterialLocation m_MaterialLocation = MaterialLocation.EmbedInPrefab;
        public MaterialSearch m_MaterialSearch = MaterialSearch.RecursiveSubFolder;
        public MaterialNaming m_MaterialNaming = MaterialNaming.ByMaterialName;
        public string m_MaterialSearchPath = "Assets/Materials";

        [Header("Animation Settings")]
        public bool m_ImportAnimation = true;

        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            NinjaNext loader = new NinjaNext();
            try
            {
                loader.Load(ctx.assetPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load XNJ package: {ctx.assetPath}.\nException: {ex}");
                return;
            }

            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);

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
            else
            {
                rootGO = new GameObject(assetName);
            }

            if (m_ImportAnimation)
            {
                NinjaMotion nodeMotion = loader.Data.Motion;
                NinjaMotion matMotion = loader.Data.MaterialMotion;
                string nodeSource, matSource;

                NinjaMotionResolver.ResolveLinkedMotions(ctx.assetPath, ctx, out NinjaMotion extraNodeMot, out NinjaMotion extraMatMot, out nodeSource, out matSource);
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

            ctx.AddObjectToAsset("main", rootGO);
            ctx.SetMainObject(rootGO);
        }
    }
}