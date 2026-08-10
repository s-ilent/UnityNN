using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    public static class NinjaObjectResolver
    {
        /// <summary>
        /// Resolves a NinjaObject into a Unity GameObject hierarchy with Meshes and Materials.
        /// </summary>
        public static GameObject ResolveObject(
            NinjaObject objData,
            NinjaTextureList texList,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            float scale,
            bool importMaterials,
            MaterialLocation materialLocation,
            MaterialSearch materialSearch,
            MaterialNaming materialNaming,
            string materialSearchPath,
            out List<Transform> outNodeTransforms)
        {
            outNodeTransforms = new List<Transform>();

            if (objData == null) return null;

            string nameSource;
            NinjaNodeNameList resolvedNodeNames = NinjaNodeNameResolver.ResolveNodeNames(objData, null, ctx?.assetPath, ctx, out nameSource);

            GameObject rootGO = new GameObject(assetName);

            for (int i = 0; i < objData.Nodes.Count; i++)
            {
                NinjaNode node = objData.Nodes[i];
                string nodeName = !string.IsNullOrEmpty(node.Name) ? node.Name : $"Node_{i:0000}";
                GameObject nodeGO = new GameObject(nodeName);

                Vector3 pos = node.Translation;
                pos.x *= -1f * scale;
                pos.y *= scale;
                pos.z *= scale;

                Vector3 rot = node.Rotation;
                if (float.IsNaN(rot.x) || float.IsInfinity(rot.x)) rot.x = 0f;
                if (float.IsNaN(rot.y) || float.IsInfinity(rot.y)) rot.y = 0f;
                if (float.IsNaN(rot.z) || float.IsInfinity(rot.z)) rot.z = 0f;
                rot.y *= -1f;

                nodeGO.transform.localPosition = pos;
                nodeGO.transform.localEulerAngles = rot;
                nodeGO.transform.localScale = node.Scaling;

                if (node.ParentIndex >= 0 && node.ParentIndex < outNodeTransforms.Count)
                    nodeGO.transform.SetParent(outNodeTransforms[node.ParentIndex], false);
                else
                    nodeGO.transform.SetParent(rootGO.transform, false);

                outNodeTransforms.Add(nodeGO.transform);
            }

            List<Material> materials = new List<Material>();
            if (importMaterials)
            {
                materials = NinjaMaterialResolver.ResolveMaterials(
                    objData,
                    texList,
                    assetName,
                    ctx,
                    materialLocation,
                    materialSearch,
                    materialNaming,
                    materialSearchPath
                );
            }

            int subObjIndex = 0;
            foreach (NinjaSubObject subObj in objData.SubObjects)
            {
                foreach (NinjaMeshSet meshSet in subObj.MeshSets)
                {
                    if (meshSet.VertexListIndex < 0 || meshSet.VertexListIndex >= objData.VertexLists.Count ||
                        meshSet.PrimitiveListIndex < 0 || meshSet.PrimitiveListIndex >= objData.PrimitiveLists.Count)
                    {
                        continue;
                    }

                    NinjaVertexList vList = objData.VertexLists[meshSet.VertexListIndex];
                    NinjaPrimitiveList pList = objData.PrimitiveLists[meshSet.PrimitiveListIndex];

                    Mesh mesh = CreateUnityMesh(vList, pList, scale, $"{assetName}_Mesh_{subObjIndex}");
                    if (mesh == null) continue;

                    ctx.AddObjectToAsset($"Mesh_{subObjIndex}", mesh);

                    GameObject meshGO = new GameObject($"SubObj_{subObjIndex}");
                    Transform parentNode = (meshSet.NodeIndex >= 0 && meshSet.NodeIndex < outNodeTransforms.Count)
                        ? outNodeTransforms[meshSet.NodeIndex] : rootGO.transform;
                    meshGO.transform.SetParent(parentNode, false);

                    Material mat = (meshSet.MaterialIndex >= 0 && meshSet.MaterialIndex < materials.Count)
                        ? materials[meshSet.MaterialIndex] : new Material(Shader.Find("Standard"));

                    if (vList.BoneMatrixIndices.Count > 0)
                    {
                        SkinnedMeshRenderer smr = meshGO.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.sharedMaterial = mat;

                        Transform[] bones = new Transform[vList.BoneMatrixIndices.Count];
                        Matrix4x4[] subBindPoses = new Matrix4x4[vList.BoneMatrixIndices.Count];
                        for (int b = 0; b < vList.BoneMatrixIndices.Count; b++)
                        {
                            int nodeIdx = vList.BoneMatrixIndices[b];
                            if (nodeIdx >= 0 && nodeIdx < outNodeTransforms.Count)
                            {
                                bones[b] = outNodeTransforms[nodeIdx];
                                subBindPoses[b] = outNodeTransforms[nodeIdx].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
                            }
                        }
                        mesh.bindposes = subBindPoses;
                        smr.bones = bones;
                        smr.rootBone = parentNode;
                    }
                    else
                    {
                        MeshFilter mf = meshGO.AddComponent<MeshFilter>();
                        mf.sharedMesh = mesh;
                        MeshRenderer mr = meshGO.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = mat;
                    }

                    subObjIndex++;
                }
            }

            return rootGO;
        }

        public static GameObject ResolveObject(
            NinjaObject objData,
            NinjaTextureList texList,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            float scale,
            bool importMaterials,
            MaterialLocation materialLocation,
            MaterialSearch materialSearch,
            MaterialNaming materialNaming,
            string materialSearchPath)
        {
            List<Transform> dummy;
            return ResolveObject(objData, texList, assetName, ctx, scale, importMaterials, materialLocation, materialSearch, materialNaming, materialSearchPath, out dummy);
        }

        public static Mesh CreateUnityMesh(NinjaVertexList vList, NinjaPrimitiveList pList, float scale, string name)
        {
            if (vList == null || vList.Vertices == null || vList.Vertices.Count == 0) return null;

            Mesh mesh = new Mesh { name = name };

            Vector3[] positions = new Vector3[vList.Vertices.Count];
            Vector3[] normals = new Vector3[vList.Vertices.Count];
            Vector4[] tangents = new Vector4[vList.Vertices.Count];
            Color32[] colors = new Color32[vList.Vertices.Count];
            Vector2[] uv0 = new Vector2[vList.Vertices.Count];
            BoneWeight[] boneWeights = new BoneWeight[vList.Vertices.Count];

            bool hasNormals = false, hasTangents = false, hasColors = false, hasUV = false, hasWeights = false;

            for (int i = 0; i < vList.Vertices.Count; i++)
            {
                NinjaVertex v = vList.Vertices[i];
                if (v == null) continue;

                if (v.Position.HasValue)
                {
                    Vector3 pos = v.Position.Value;
                    pos.x *= -1f * scale;
                    pos.y *= scale;
                    pos.z *= scale;
                    positions[i] = pos;
                }

                if (v.Normals.HasValue)
                {
                    hasNormals = true;
                    Vector3 n = v.Normals.Value;
                    n.x *= -1f;
                    normals[i] = n;
                }

                if (v.Tangent.HasValue)
                {
                    hasTangents = true;
                    Vector3 t = v.Tangent.Value;
                    tangents[i] = new Vector4(-t.x, t.y, t.z, 1.0f);
                }

                if (v.VertexColours != null && v.VertexColours.Length >= 4)
                {
                    hasColors = true;
                    colors[i] = new Color32(v.VertexColours[2], v.VertexColours[1], v.VertexColours[0], v.VertexColours[3]);
                }

                if (v.TextureCoordinates != null && v.TextureCoordinates.Count > 0)
                {
                    hasUV = true;
                    Vector2 uv = v.TextureCoordinates[0];
                    uv.y = 1.0f - uv.y;
                    uv0[i] = uv;
                }

                if (v.Weight.HasValue && v.MatrixIndices != null && v.MatrixIndices.Length >= 4)
                {
                    hasWeights = true;
                    BoneWeight bw = new BoneWeight
                    {
                        boneIndex0 = v.MatrixIndices[0],
                        boneIndex1 = v.MatrixIndices[1],
                        boneIndex2 = v.MatrixIndices[2],
                        boneIndex3 = v.MatrixIndices[3]
                    };

                    Vector3 w = v.Weight.Value;
                    bw.weight0 = w.x;
                    bw.weight1 = w.y;
                    bw.weight2 = w.z;
                    bw.weight3 = Mathf.Max(0f, 1.0f - (w.x + w.y + w.z));
                    boneWeights[i] = bw;
                }
            }

            mesh.vertices = positions;
            if (hasNormals) mesh.normals = normals;
            if (hasTangents) mesh.tangents = tangents;
            if (hasColors) mesh.colors32 = colors;
            if (hasUV) mesh.uv = uv0;
            if (hasWeights) mesh.boneWeights = boneWeights;

            List<int> triangles = DecodeIndices(pList);
            if (triangles.Count >= 3)
            {
                mesh.triangles = triangles.ToArray();
            }

            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static List<int> DecodeIndices(NinjaPrimitiveList pList)
        {
            List<int> triangles = new List<int>();

            if (pList == null || pList.IndexIndices == null || pList.IndexIndices.Count < 3)
                return triangles;

            if (pList.StripIndices != null && pList.StripIndices.Count > 0)
            {
                int indexCursor = 0;
                for (int s = 0; s < pList.StripIndices.Count; s++)
                {
                    int stripLen = pList.StripIndices[s];
                    if (stripLen < 3 || indexCursor + stripLen > pList.IndexIndices.Count)
                    {
                        indexCursor += stripLen;
                        continue;
                    }

                    for (int i = 0; i < stripLen - 2; i++)
                    {
                        ushort a = pList.IndexIndices[indexCursor + i];
                        ushort b = pList.IndexIndices[indexCursor + i + 1];
                        ushort c = pList.IndexIndices[indexCursor + i + 2];

                        if (a == b || b == c || a == c)
                            continue;

                        if (i % 2 == 1)
                        {
                            triangles.Add(a);
                            triangles.Add(b);
                            triangles.Add(c);
                        }
                        else
                        {
                            triangles.Add(a);
                            triangles.Add(c);
                            triangles.Add(b);
                        }
                    }

                    indexCursor += stripLen;
                }
            }
            else
            {
                for (int i = 0; i < pList.IndexIndices.Count - 2; i++)
                {
                    ushort a = pList.IndexIndices[i];
                    ushort b = pList.IndexIndices[i + 1];
                    ushort c = pList.IndexIndices[i + 2];

                    if (a == b || b == c || a == c)
                        continue;

                    if (i % 2 == 1)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(b);
                    }
                }
            }

            int remainder = triangles.Count % 3;
            if (remainder > 0)
            {
                triangles.RemoveRange(triangles.Count - remainder, remainder);
            }

            return triangles;
        }
    }
}