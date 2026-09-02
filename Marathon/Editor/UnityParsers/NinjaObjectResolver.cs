// File: Marathon/UnityParsers/NinjaObjectResolver.cs
using UnityNN;
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace UnityNN.Editor
{
    public static class NinjaObjectResolver
    {
        public static GameObject ResolveObject(
            NinjaObject objData,
            NinjaTextureList texList,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            NinjaImportSettings settings,
            out List<Transform> outNodeTransforms)
        {
            outNodeTransforms = new List<Transform>();
            if (objData == null) return null;
            settings ??= NinjaImportSettings.Default;

            // Resolve bone/node name strings if present in linked .xnn files
            NinjaNodeNameResolver.ResolveNodeNames(objData, null, ctx?.assetPath, ctx, out _);

            GameObject rootGO = new GameObject(assetName);

            // 1. Build Full Node / Bone Hierarchy
            for (int i = 0; i < objData.Nodes.Count; i++)
            {
                NinjaNode node = objData.Nodes[i];
                string nodeName = !string.IsNullOrEmpty(node.Name) ? node.Name : $"Node_{i:0000}";
                GameObject nodeGO = new GameObject(nodeName);

                nodeGO.transform.localPosition = NinjaCoordinateUtility.ToUnityPosition(node.Translation, settings.Scale);
                nodeGO.transform.localEulerAngles = NinjaCoordinateUtility.ToUnityEuler(node.Rotation);
                nodeGO.transform.localScale = (node.Scaling == Vector3.zero) ? Vector3.one : node.Scaling;

                if (node.ParentIndex >= 0 && node.ParentIndex < outNodeTransforms.Count)
                    nodeGO.transform.SetParent(outNodeTransforms[node.ParentIndex], false);
                else
                    nodeGO.transform.SetParent(rootGO.transform, false);

                outNodeTransforms.Add(nodeGO.transform);
            }

            // 2. Resolve Materials
            List<Material> materials = settings.ImportMaterials
                ? NinjaMaterialResolver.ResolveMaterials(
                    objData,
                    texList,
                    assetName,
                    ctx,
                    settings)
                : new List<Material>();

            // Apply Per-Material User Overrides from Inspector Remap Table
            if (settings.MaterialRemaps != null)
            {
                foreach (var remap in settings.MaterialRemaps)
                {
                    if (remap.overrideMaterial != null && remap.slotIndex >= 0 && remap.slotIndex < materials.Count)
                    {
                        materials[remap.slotIndex] = remap.overrideMaterial;
                    }
                }
            }

            // 3. Dispatch to Mesh Import Mode
            switch (settings.MeshImportMode)
            {
                case MeshImportMode.SingleSkinnedMesh:
                    BuildSingleSkinnedMesh(objData, rootGO, outNodeTransforms, materials, assetName, settings, ctx);
                    break;
                case MeshImportMode.CombinedByNode:
                    BuildCombinedNodeMeshes(objData, rootGO, outNodeTransforms, materials, assetName, settings, ctx);
                    break;
                case MeshImportMode.IndividualSubObjects:
                default:
                    BuildIndividualSubObjects(objData, rootGO, outNodeTransforms, materials, assetName, settings, ctx);
                    break;
            }

            return rootGO;
        }

        public static bool HasSkinning(NinjaObject objData)
        {
            if (objData?.VertexLists == null) return false;
            for (int i = 0; i < objData.VertexLists.Count; i++)
            {
                var vl = objData.VertexLists[i];
                if (vl?.BoneMatrixIndices != null && vl.BoneMatrixIndices.Count > 0)
                    return true;
            }
            return false;
        }
        
        #region Mode 1: Single Skinned Mesh
        private static void BuildSingleSkinnedMesh(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            string assetName,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            int estimatedVerts = 0;
            foreach (var vl in objData.VertexLists) estimatedVerts += vl.Vertices.Count;

            MeshBuffer buffer = new MeshBuffer(estimatedVerts);

            foreach (var subObj in objData.SubObjects)
            {
                foreach (var meshSet in subObj.MeshSets)
                {
                    int fallbackNode = (meshSet.NodeIndex >= 0 && meshSet.NodeIndex < allNodeTransforms.Count) ? meshSet.NodeIndex : 0;
                    buffer.AppendMeshSet(objData, meshSet, settings.Scale, null, null, fallbackNode, meshSet.MaterialIndex);
                }
            }

            Mesh mesh = buffer.BuildMesh($"{assetName}_SkinnedMesh");
            if (mesh == null) return;

            Transform[] bones = allNodeTransforms.ToArray();
            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                bindPoses[b] = bones[b].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
            }

            mesh.bindposes = bindPoses;
            mesh.RecalculateBounds();

            if (ctx != null) ctx.AddObjectToAsset("SkinnedMesh", mesh);

            SkinnedMeshRenderer smr = rootGO.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones.Length > 0 ? bones[0] : rootGO.transform;
            smr.sharedMaterials = MapMaterials(buffer.GetSortedSubmeshKeys(), materials);

            if (settings.GenerateMeshColliders)
            {
                MeshCollider mc = rootGO.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
        }
        #endregion

        #region Mode 2: Combined Node Meshes
        private static void BuildCombinedNodeMeshes(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            string assetName,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            // If the asset contains skinned vertex lists, build a unified skeleton SkinnedMesh
            if (HasSkinning(objData))
            {
                BuildSingleSkinnedMesh(objData, rootGO, allNodeTransforms, materials, assetName, settings, ctx);
                return;
            }

            for (int n = 0; n < objData.Nodes.Count; n++)
            {
                Transform nodeTr = allNodeTransforms[n];
                Dictionary<int, List<NinjaMeshSet>> byMat = new Dictionary<int, List<NinjaMeshSet>>();

                foreach (var subObj in objData.SubObjects)
                {
                    foreach (var ms in subObj.MeshSets)
                    {
                        if (ms.NodeIndex == n)
                        {
                            if (!byMat.ContainsKey(ms.MaterialIndex))
                                byMat[ms.MaterialIndex] = new List<NinjaMeshSet>();

                            byMat[ms.MaterialIndex].Add(ms);
                        }
                    }
                }

                if (byMat.Count == 0) continue;
                bool isSingle = byMat.Count == 1;

                foreach (var kvp in byMat)
                {
                    int matIdx = kvp.Key;
                    List<NinjaMeshSet> sets = kvp.Value;
                    GameObject targetGO = isSingle ? nodeTr.gameObject : new GameObject($"Mat_{matIdx:00}");

                    if (!isSingle)
                    {
                        targetGO.transform.SetParent(nodeTr, false);
                    }

                    BuildRigidNodeMeshSection(objData, rootGO, nodeTr, targetGO, sets, matIdx, n, materials, assetName, settings, ctx);
                }
            }
        }

        private static void BuildRigidNodeMeshSection(
            NinjaObject objData,
            GameObject rootGO,
            Transform nodeTr,
            GameObject targetGO,
            List<NinjaMeshSet> meshSets,
            int matIdx,
            int nodeIdx,
            List<Material> materials,
            string assetName,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            int estimatedVerts = 0;
            foreach (var ms in meshSets)
            {
                if (ms.VertexListIndex >= 0 && ms.VertexListIndex < objData.VertexLists.Count)
                {
                    estimatedVerts += objData.VertexLists[ms.VertexListIndex].Vertices.Count;
                }
            }

            Matrix4x4 nodeXform = nodeTr.worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
            MeshBuffer buffer = new MeshBuffer(estimatedVerts);

            foreach (var ms in meshSets)
            {
                buffer.AppendMeshSet(objData, ms, settings.Scale, nodeXform, null, 0, 0);
            }

            Mesh mesh = buffer.BuildMesh($"{assetName}_Node_{nodeIdx}_Mat_{matIdx}");
            if (mesh == null) return;

            Material assignedMat = GetMaterialOrStandard(matIdx, materials);
            if (ctx != null) ctx.AddObjectToAsset($"Mesh_Node_{nodeIdx}_Mat_{matIdx}", mesh);

            targetGO.AddComponent<MeshFilter>().sharedMesh = mesh;
            targetGO.AddComponent<MeshRenderer>().sharedMaterial = assignedMat;

            if (settings.GenerateMeshColliders)
            {
                MeshCollider mc = targetGO.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
        }
        #endregion

        #region Mode 3: Individual Sub-Objects
        private static void BuildIndividualSubObjects(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            string assetName,
            NinjaImportSettings settings,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            int subObjIdx = 0;
            foreach (NinjaSubObject subObj in objData.SubObjects)
            {
                foreach (NinjaMeshSet ms in subObj.MeshSets)
                {
                    if (ms.VertexListIndex < 0 || ms.VertexListIndex >= objData.VertexLists.Count ||
                        ms.PrimitiveListIndex < 0 || ms.PrimitiveListIndex >= objData.PrimitiveLists.Count)
                        continue;

                    var vList = objData.VertexLists[ms.VertexListIndex];
                    Transform parentTr = (ms.NodeIndex >= 0 && ms.NodeIndex < allNodeTransforms.Count)
                        ? allNodeTransforms[ms.NodeIndex] : rootGO.transform;

                    bool isSkinned = vList.BoneMatrixIndices.Count > 0;
                    Matrix4x4? nodeXform = (!isSkinned && parentTr != rootGO.transform)
                        ? parentTr.worldToLocalMatrix * rootGO.transform.localToWorldMatrix : (Matrix4x4?)null;

                    MeshBuffer buffer = new MeshBuffer(vList.Vertices.Count);
                    buffer.AppendMeshSet(objData, ms, settings.Scale, nodeXform, null, 0, 0);

                    Mesh mesh = buffer.BuildMesh($"{assetName}_Mesh_{subObjIdx}");
                    if (mesh == null) continue;

                    if (ctx != null) ctx.AddObjectToAsset($"Mesh_{subObjIdx}", mesh);

                    GameObject meshGO = new GameObject($"SubObj_{subObjIdx}");
                    meshGO.transform.SetParent(parentTr, false);
                    Material mat = GetMaterialOrStandard(ms.MaterialIndex, materials);

                    if (isSkinned)
                    {
                        SkinnedMeshRenderer smr = meshGO.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.sharedMaterial = mat;

                        Transform[] bones = new Transform[vList.BoneMatrixIndices.Count];
                        Matrix4x4[] binds = new Matrix4x4[vList.BoneMatrixIndices.Count];
                        for (int b = 0; b < vList.BoneMatrixIndices.Count; b++)
                        {
                            int nIdx = vList.BoneMatrixIndices[b];
                            if (nIdx >= 0 && nIdx < allNodeTransforms.Count)
                            {
                                bones[b] = allNodeTransforms[nIdx];
                                binds[b] = allNodeTransforms[nIdx].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
                            }
                        }
                        mesh.bindposes = binds;
                        smr.bones = bones;
                        smr.rootBone = parentTr;
                    }
                    else
                    {
                        meshGO.AddComponent<MeshFilter>().sharedMesh = mesh;
                        meshGO.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    }

                    if (settings.GenerateMeshColliders)
                    {
                        MeshCollider mc = meshGO.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                    }

                    subObjIdx++;
                }
            }
        }
        #endregion

        #region Helpers & MeshBuffer
        public static Mesh CreateUnityMesh(
            NinjaVertexList vList,
            NinjaPrimitiveList pList,
            float scale,
            string name,
            Matrix4x4? transformMatrix = null)
        {
            if (vList?.Vertices == null || vList.Vertices.Count == 0) return null;
            NinjaObject dummy = new NinjaObject();
            dummy.VertexLists.Add(vList);
            dummy.PrimitiveLists.Add(pList);
            NinjaMeshSet ms = new NinjaMeshSet { VertexListIndex = 0, PrimitiveListIndex = 0 };

            MeshBuffer buffer = new MeshBuffer(vList.Vertices.Count);
            buffer.AppendMeshSet(dummy, ms, scale, transformMatrix, null, 0, 0);
            return buffer.BuildMesh(name);
        }

        public static List<int> DecodeIndices(NinjaPrimitiveList pList)
        {
            if (pList?.IndexIndices == null || pList.IndexIndices.Count < 3) return new List<int>();

            List<int> triangles = new List<int>(pList.IndexIndices.Count * 2);

            if (pList.StripIndices != null && pList.StripIndices.Count > 0)
            {
                int cursor = 0;
                for (int s = 0; s < pList.StripIndices.Count; s++)
                {
                    int len = pList.StripIndices[s];
                    if (len < 3 || cursor + len > pList.IndexIndices.Count) { cursor += len; continue; }

                    for (int i = 0; i < len - 2; i++)
                    {
                        ushort a = pList.IndexIndices[cursor + i];
                        ushort b = pList.IndexIndices[cursor + i + 1];
                        ushort c = pList.IndexIndices[cursor + i + 2];
                        if (a == b || b == c || a == c) continue;

                        if (i % 2 == 1) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
                        else { triangles.Add(a); triangles.Add(c); triangles.Add(b); }
                    }
                    cursor += len;
                }
            }
            else
            {
                for (int i = 0; i < pList.IndexIndices.Count - 2; i += 3)
                {
                    ushort a = pList.IndexIndices[i];
                    ushort b = pList.IndexIndices[i + 1];
                    ushort c = pList.IndexIndices[i + 2];
                    if (a == b || b == c || a == c) continue;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                }
            }
            return triangles;
        }

        private static Material GetMaterialOrStandard(int matIdx, List<Material> materials)
        {
            return (matIdx >= 0 && matIdx < materials.Count && materials[matIdx] != null)
                ? materials[matIdx] : new Material(Shader.Find("Standard"));
        }

        private static Material[] MapMaterials(List<int> matKeys, List<Material> materials)
        {
            Material[] array = new Material[matKeys.Count];
            for (int i = 0; i < matKeys.Count; i++)
            {
                array[i] = GetMaterialOrStandard(matKeys[i], materials);
            }
            return array;
        }

        private class MeshBuffer
        {
            public readonly List<Vector3> Positions;
            public readonly List<Vector3> Normals;
            public readonly List<Vector4> Tangents;
            public readonly List<Color32> Colors;
            public readonly List<Vector2> UVs;
            public readonly List<Vector2> UV2s;
            public readonly List<BoneWeight> BoneWeights;
            public readonly Dictionary<int, List<int>> SubmeshTriangles;
            public bool HasWeights;
            public bool NoNormals;
            public bool NoTangents;

            private readonly Dictionary<(int vListIdx, int origVIdx, int fallbackBoneIdx), int> m_VertexRemap;

            public MeshBuffer(int vertexCapacity = 0)
            {
                int cap = Math.Max(0, vertexCapacity);
                Positions = new List<Vector3>(cap);
                Normals = new List<Vector3>(cap);
                Tangents = new List<Vector4>(cap);
                Colors = new List<Color32>(cap);
                UVs = new List<Vector2>(cap);
                UV2s = new List<Vector2>(cap);
                BoneWeights = new List<BoneWeight>(cap);
                SubmeshTriangles = new Dictionary<int, List<int>>();
                m_VertexRemap = new Dictionary<(int, int, int), int>();
            }

            public List<int> GetSortedSubmeshKeys()
            {
                var keys = new List<int>(SubmeshTriangles.Keys);
                keys.Sort();
                return keys;
            }

            public void AppendMeshSet(
                NinjaObject objData,
                NinjaMeshSet meshSet,
                float scale,
                Matrix4x4? localTransform,
                Func<byte, int> bonePaletteRemap,
                int fallbackBoneIdx,
                int submeshKey)
            {
                if (meshSet.VertexListIndex < 0 || meshSet.VertexListIndex >= objData.VertexLists.Count ||
                    meshSet.PrimitiveListIndex < 0 || meshSet.PrimitiveListIndex >= objData.PrimitiveLists.Count)
                    return;

                var vList = objData.VertexLists[meshSet.VertexListIndex];
                var pList = objData.PrimitiveLists[meshSet.PrimitiveListIndex];
                if (vList.Vertices == null || vList.Vertices.Count == 0)
                    return;

                bool isSkinned = vList.BoneMatrixIndices != null && vList.BoneMatrixIndices.Count > 0;
                if (isSkinned) HasWeights = true;
                if ((vList.Format & XboxVertexType.NND_VTXTYPE_XB_NORMAL) == 0)
                    NoNormals = true;
                if ((vList.Format & XboxVertexType.NND_VTXTYPE_XB_TANGENT) == 0)
                    NoTangents = true;

                bool applyXform = localTransform.HasValue && localTransform.Value != Matrix4x4.identity;
                Matrix4x4 xform = localTransform.GetValueOrDefault(Matrix4x4.identity);

                if (!SubmeshTriangles.TryGetValue(submeshKey, out List<int> tris))
                {
                    tris = new List<int>();
                    SubmeshTriangles[submeshKey] = tris;
                }

                List<int> decoded = DecodeIndices(pList);
                int vListIdx = meshSet.VertexListIndex;

                for (int t = 0; t + 2 < decoded.Count; t += 3)
                {
                    int i0 = decoded[t];
                    int i1 = decoded[t + 1];
                    int i2 = decoded[t + 2];

                    if (i0 < 0 || i0 >= vList.Vertices.Count ||
                        i1 < 0 || i1 >= vList.Vertices.Count ||
                        i2 < 0 || i2 >= vList.Vertices.Count)
                    {
                        continue;
                    }

                    tris.Add(GetOrAddVertex(vList, vListIdx, i0, scale, applyXform, xform, isSkinned, bonePaletteRemap, fallbackBoneIdx));
                    tris.Add(GetOrAddVertex(vList, vListIdx, i1, scale, applyXform, xform, isSkinned, bonePaletteRemap, fallbackBoneIdx));
                    tris.Add(GetOrAddVertex(vList, vListIdx, i2, scale, applyXform, xform, isSkinned, bonePaletteRemap, fallbackBoneIdx));
                }
            }

            private int GetOrAddVertex(
                NinjaVertexList vList,
                int vListIdx,
                int origVIdx,
                float scale,
                bool applyXform,
                Matrix4x4 xform,
                bool isSkinned,
                Func<byte, int> bonePaletteRemap,
                int fallbackBoneIdx)
            {
                var remapKey = (vListIdx, origVIdx, fallbackBoneIdx);
                if (m_VertexRemap.TryGetValue(remapKey, out int existingIdx))
                {
                    return existingIdx;
                }

                int newIdx = Positions.Count;
                m_VertexRemap[remapKey] = newIdx;

                NinjaVertex vert = vList.Vertices[origVIdx];
                if (vert == null)
                {
                    Positions.Add(Vector3.zero);
                    Normals.Add(Vector3.up);
                    Tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                    UVs.Add(Vector2.zero);
                    if (UV2s.Count > 0) UV2s.Add(Vector2.zero);
                    Colors.Add(new Color32(255, 255, 255, 255));
                    if (HasWeights) BoneWeights.Add(new BoneWeight { boneIndex0 = fallbackBoneIdx, weight0 = 1.0f });
                    return newIdx;
                }

                Vector3 rawPos = vert.Position.GetValueOrDefault();
                Vector3 pos = NinjaCoordinateUtility.ToUnityPosition(rawPos, scale);
                if (applyXform) pos = xform.MultiplyPoint3x4(pos);
                Positions.Add(pos);

                Vector3 rawNorm = vert.Normals.GetValueOrDefault(Vector3.up);
                Vector3 norm = NinjaCoordinateUtility.ToUnityNormal(rawNorm);
                if (applyXform) norm = xform.MultiplyVector(norm).normalized;
                Normals.Add(norm);

                Vector3 rawTan = vert.Tangent.GetValueOrDefault(Vector3.right);
                Vector4 tan = NinjaCoordinateUtility.ToUnityTangent(rawTan);
                if (applyXform)
                {
                    Vector3 transformedTan = xform.MultiplyVector(new Vector3(tan.x, tan.y, tan.z)).normalized;
                    tan = new Vector4(transformedTan.x, transformedTan.y, transformedTan.z, tan.w);
                }
                Tangents.Add(tan);

                if (vert.TextureCoordinates != null && vert.TextureCoordinates.Count > 0)
                    UVs.Add(NinjaCoordinateUtility.ToUnityUV(vert.TextureCoordinates[0]));
                else
                    UVs.Add(Vector2.zero);

                if (vert.TextureCoordinates != null && vert.TextureCoordinates.Count > 1)
                {
                    while (UV2s.Count < Positions.Count - 1) UV2s.Add(Vector2.zero);
                    UV2s.Add(NinjaCoordinateUtility.ToUnityUV(vert.TextureCoordinates[1]));
                }
                else if (UV2s.Count > 0)
                {
                    UV2s.Add(Vector2.zero);
                }

                if (vert.VertexColours != null && vert.VertexColours.Length >= 4)
                    Colors.Add(new Color32(vert.VertexColours[2], vert.VertexColours[1], vert.VertexColours[0], vert.VertexColours[3]));
                else
                    Colors.Add(new Color32(255, 255, 255, 255));

                BoneWeight bw = new BoneWeight();
                if (isSkinned && vert.Weight.HasValue && vert.MatrixIndices != null && vert.MatrixIndices.Length >= 4)
                {
                    bw.boneIndex0 = bonePaletteRemap != null ? bonePaletteRemap(vert.MatrixIndices[0]) : (vList.BoneMatrixIndices != null && vert.MatrixIndices[0] < vList.BoneMatrixIndices.Count ? vList.BoneMatrixIndices[vert.MatrixIndices[0]] : fallbackBoneIdx);
                    bw.boneIndex1 = bonePaletteRemap != null ? bonePaletteRemap(vert.MatrixIndices[1]) : (vList.BoneMatrixIndices != null && vert.MatrixIndices[1] < vList.BoneMatrixIndices.Count ? vList.BoneMatrixIndices[vert.MatrixIndices[1]] : fallbackBoneIdx);
                    bw.boneIndex2 = bonePaletteRemap != null ? bonePaletteRemap(vert.MatrixIndices[2]) : (vList.BoneMatrixIndices != null && vert.MatrixIndices[2] < vList.BoneMatrixIndices.Count ? vList.BoneMatrixIndices[vert.MatrixIndices[2]] : fallbackBoneIdx);
                    bw.boneIndex3 = bonePaletteRemap != null ? bonePaletteRemap(vert.MatrixIndices[3]) : (vList.BoneMatrixIndices != null && vert.MatrixIndices[3] < vList.BoneMatrixIndices.Count ? vList.BoneMatrixIndices[vert.MatrixIndices[3]] : fallbackBoneIdx);

                    Vector3 w = vert.Weight.Value;
                    bw.weight0 = w.x;
                    bw.weight1 = w.y;
                    bw.weight2 = w.z;
                    bw.weight3 = Mathf.Max(0f, 1.0f - (w.x + w.y + w.z));
                }
                else
                {
                    bw.boneIndex0 = fallbackBoneIdx;
                    bw.weight0 = 1.0f;
                }
                BoneWeights.Add(bw);

                return newIdx;
            }

            public Mesh BuildMesh(string meshName)
            {
                if (Positions.Count == 0) return null;
                Mesh mesh = new Mesh { name = meshName };
                if (Positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.vertices = Positions.ToArray();
                mesh.normals = Normals.ToArray();
                mesh.tangents = Tangents.ToArray();
                mesh.uv = UVs.ToArray();
                if (UV2s.Count == Positions.Count) mesh.uv2 = UV2s.ToArray();
                mesh.colors32 = Colors.ToArray();
                if (HasWeights) mesh.boneWeights = BoneWeights.ToArray();

                var sortedKeys = GetSortedSubmeshKeys();
                mesh.subMeshCount = sortedKeys.Count;
                for (int i = 0; i < sortedKeys.Count; i++)
                    mesh.SetTriangles(SubmeshTriangles[sortedKeys[i]], i);

                if (NoNormals) mesh.RecalculateNormals();
                if (NoTangents) mesh.RecalculateTangents();

                mesh.RecalculateBounds();
                return mesh;
            }
        }
        #endregion
    }
}