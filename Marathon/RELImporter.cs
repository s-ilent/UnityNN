using UnityEngine;
using UnityEditor;
using System.IO;

namespace SilentTools
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "rel")]
    public class RELImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        [Header("Import Settings")]
        public float m_Scale = 0.05f;

        [Header("Scene Settings")]
        public bool m_ApplyEnvironmentToScene = false;

        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            byte[] rawData;
            try
            {
                rawData = File.ReadAllBytes(ctx.assetPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to read REL file {ctx.assetPath}:\n{ex}");
                return;
            }

            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            RelFileType relType;
            object parsedData = RelResolver.ParseRelBytes(rawData, Path.GetFileName(ctx.assetPath), out relType);

            if (parsedData == null)
            {
                Debug.LogWarning($"Could not parse REL file {ctx.assetPath} (type: {relType})");
                return;
            }

            GameObject rootGO = RelResolver.ResolveRelAsset(parsedData, relType, assetName, m_Scale);

            if (m_ApplyEnvironmentToScene)
            {
                RelEnvironmentComponent envComp = rootGO.GetComponent<RelEnvironmentComponent>();
                if (envComp != null)
                {
                    envComp.ApplyEnvironmentToScene();
                }
            }

            ctx.AddObjectToAsset("main", rootGO);
            ctx.SetMainObject(rootGO);
        }
    }
}