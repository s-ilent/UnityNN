using UnityEngine;
using UnityEditor;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "xnm")]
    public class XNMImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        [Header("Import Settings")]
        public float m_Scale = 0.05f;

        public string[] m_nodeHierarchyTarget;

        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            string shortName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            NinjaNext loader = new NinjaNext();
            try
            {
                loader.Load(ctx.assetPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{shortName}: Failed to load motion asset:\n{ex}");
                return;
            }

            NinjaMotion mot = loader.Data.Motion ?? loader.Data.MaterialMotion;
            if (mot == null) return;

            string[] targets = m_nodeHierarchyTarget;
            if (targets == null || targets.Length == 0)
            {
                // Resolve target node names from associated .xna / .xnn / .xnj / .xno file
                targets = NinjaMotionResolver.ResolveNodeHierarchyTargets(ctx.assetPath, ctx);
            }

            AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                mot,
                shortName,
                m_Scale,
                targets
            );

            Texture2D icon = NinjaIconResolver.GetIconForExtension(".xnm");
            ctx.AddObjectToAsset("main", clip, icon);
            ctx.SetMainObject(clip);
        }
    }
}