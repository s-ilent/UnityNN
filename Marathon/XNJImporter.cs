using UnityEngine;
using UnityEditor;
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
                    m_MaterialSearchPath
                );
            }
            else
            {
                rootGO = new GameObject(assetName);
            }

            if (loader.Data.Motion != null && m_ImportAnimation)
            {
                AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                    loader.Data.Motion,
                    $"{assetName}_Animation",
                    m_Scale,
                    rootGO
                );

                ctx.AddObjectToAsset("AnimationClip", clip);
                rootGO.AddComponent<Animator>();
            }

            ctx.AddObjectToAsset("main", rootGO);
            ctx.SetMainObject(rootGO);
        }
    }
}