using UnityEngine;
using UnityEditor;

using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "xna")]
    public class XNAImporter : UnityEditor.AssetImporters.ScriptedImporter
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
                Debug.LogError($"Failed to load XNA package: {ctx.assetPath}. Exception: {ex.Message}");
                return;
            }

            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            GameObject rootGO = new GameObject(assetName);

            // 1. Process Embedded Model Chunk
            if (loader.Data.Object != null)
            {
                NinjaObject objData = loader.Data.Object;
                NinjaTextureList texList = loader.Data.TextureList;

                List<Transform> nodeTransforms = new List<Transform>();
                for (int i = 0; i < objData.Nodes.Count; i++)
                {
                    NinjaNode node = objData.Nodes[i];
                    GameObject nGO = new GameObject(!string.IsNullOrEmpty(node.Name) ? node.Name : $"Node_{i:0000}");
                    Vector3 pos = node.Translation;
                    pos.x *= -1f * m_Scale;
                    pos.y *= m_Scale;
                    pos.z *= m_Scale;

                    nGO.transform.localPosition = pos;
                    nGO.transform.localEulerAngles = new Vector3(node.Rotation.x, -node.Rotation.y, node.Rotation.z);
                    nGO.transform.localScale = node.Scaling;

                    if (node.ParentIndex >= 0 && node.ParentIndex < nodeTransforms.Count)
                    {
                        nGO.transform.SetParent(nodeTransforms[node.ParentIndex], false);
                    }
                    else
                    {
                        nGO.transform.SetParent(rootGO.transform, false);
                    }
                    nodeTransforms.Add(nGO.transform);
                }

                // Resolve Materials using NinjaMaterialResolver
                List<Material> materials = new List<Material>();
                if (m_ImportMaterials)
                {
                    materials = NinjaMaterialResolver.ResolveMaterials(
                        objData,
                        texList,
                        assetName,
                        ctx,
                        m_MaterialLocation,
                        m_MaterialSearch,
                        m_MaterialNaming,
                        m_MaterialSearchPath
                    );
                }

                // Build Meshes
                int subObjIndex = 0;
                foreach (var subObj in objData.SubObjects)
                {
                    foreach (var meshSet in subObj.MeshSets)
                    {
                        if (meshSet.VertexListIndex < 0 || meshSet.VertexListIndex >= objData.VertexLists.Count ||
                            meshSet.PrimitiveListIndex < 0 || meshSet.PrimitiveListIndex >= objData.PrimitiveLists.Count)
                        {
                            continue;
                        }

                        var vList = objData.VertexLists[meshSet.VertexListIndex];
                        var pList = objData.PrimitiveLists[meshSet.PrimitiveListIndex];

                        Mesh mesh = XNOImporter.CreateUnityMesh(vList, pList, m_Scale, $"{assetName}_Mesh_{subObjIndex}");
                        if (mesh == null) continue;

                        ctx.AddObjectToAsset($"Mesh_{subObjIndex}", mesh);

                        GameObject meshGO = new GameObject($"SubObj_{subObjIndex}");
                        Transform parentNode = (meshSet.NodeIndex >= 0 && meshSet.NodeIndex < nodeTransforms.Count)
                            ? nodeTransforms[meshSet.NodeIndex] : rootGO.transform;
                        meshGO.transform.SetParent(parentNode, false);

                        Material mat = (meshSet.MaterialIndex >= 0 && meshSet.MaterialIndex < materials.Count)
                            ? materials[meshSet.MaterialIndex] : new Material(Shader.Find("Standard"));

                        MeshFilter mf = meshGO.AddComponent<MeshFilter>();
                        mf.sharedMesh = mesh;
                        MeshRenderer mr = meshGO.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = mat;

                        subObjIndex++;
                    }
                }
            }

            // 2. Process Embedded Motion Chunk
            if (loader.Data.Motion != null)
            {
                Animator animator = rootGO.AddComponent<Animator>();
            }

            ctx.AddObjectToAsset("main", rootGO);
            ctx.SetMainObject(rootGO);
        }
    }
}