using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "xno")]
    public class XNOImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        [Header("Import Settings")]
        public float m_Scale = 0.05f;

        [Header("Material Settings")]
        public bool m_ImportMaterials = true;
        public MaterialLocation m_MaterialLocation = MaterialLocation.EmbedInPrefab;
        public MaterialSearch m_MaterialSearch = MaterialSearch.RecursiveSubFolder;
        public MaterialNaming m_MaterialNaming = MaterialNaming.ByMaterialName;
        public string m_MaterialSearchPath = "Assets/Materials";

        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            NinjaNext loader = new NinjaNext();
            try
            {
                loader.Load(ctx.assetPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load XNO file: {ctx.assetPath}.\nException: {ex}");
                return;
            }

            if (loader.Data.Object == null) return;

            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            GameObject rootGO = NinjaObjectResolver.ResolveObject(
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

            ctx.AddObjectToAsset("main", rootGO);
            ctx.SetMainObject(rootGO);
        }

        public static Mesh CreateUnityMesh(NinjaVertexList vList, NinjaPrimitiveList pList, float scale, string name)
        {
            return NinjaObjectResolver.CreateUnityMesh(vList, pList, scale, name);
        }

        public static List<int> DecodeIndices(NinjaPrimitiveList pList)
        {
            return NinjaObjectResolver.DecodeIndices(pList);
        }
    }
}