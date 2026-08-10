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

            if (loader.Data.Motion == null) return;

            AnimationClip clip = NinjaMotionResolver.ResolveMotion(
                loader.Data.Motion,
                shortName,
                m_Scale,
                m_nodeHierarchyTarget
            );

            ctx.AddObjectToAsset("main", clip);
            ctx.SetMainObject(clip);
        }
    }
}