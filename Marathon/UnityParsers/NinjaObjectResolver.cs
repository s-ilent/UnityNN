// File: Marathon/UnityParsers/NinjaObjectResolver.cs
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    public static class NinjaObjectResolver
    {
        public static GameObject ResolveObject(
            NinjaObject objData,
            NinjaTextureList texList,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            float scale,
            MeshImportMode importMode,
            bool importMaterials,
            MaterialLocation materialLocation,
            MaterialSearch materialSearch,
            MaterialNaming materialNaming,
            string materialSearchPath,
            string[] textureSearchPaths,
            List<MaterialRemapEntry> materialRemaps,
            out List<Transform> outNodeTransforms)
        {
            outNodeTransforms = new List<Transform>();
            if (objData == null) return null;

            // Resolve bone/node name strings if present in linked .xnn files
            NinjaNodeNameResolver.ResolveNodeNames(objData, null, ctx?.assetPath, ctx, out _);

            GameObject rootGO = new GameObject(assetName);

            // 1. Build Full Node / Bone Hierarchy
            for (int i = 0; i < objData.Nodes.Count; i++)
            {
                NinjaNode node = objData.Nodes[i];
                string nodeName = !string.IsNullOrEmpty(node.Name) ? node.Name : $"Node_{i:0000}";
                GameObject nodeGO = new GameObject(nodeName);

                Vector3 pos = new Vector3(-node.Translation.x * scale, node.Translation.y * scale, node.Translation.z * scale);
                Vector3 rot = node.Rotation;

                if (float.IsNaN(rot.x) || float.IsInfinity(rot.x)) rot.x = 0f;
                if (float.IsNaN(rot.y) || float.IsInfinity(rot.y)) rot.y = 0f;
                if (float.IsNaN(rot.z) || float.IsInfinity(rot.z)) rot.z = 0f;

                nodeGO.transform.localPosition = pos;
                nodeGO.transform.localEulerAngles = new Vector3(rot.x, -rot.y, -rot.z);
                nodeGO.transform.localScale = (node.Scaling == Vector3.zero) ? Vector3.one : node.Scaling;

                if (node.ParentIndex >= 0 && node.ParentIndex < outNodeTransforms.Count)
                    nodeGO.transform.SetParent(outNodeTransforms[node.ParentIndex], false);
                else
                    nodeGO.transform.SetParent(rootGO.transform, false);

                outNodeTransforms.Add(nodeGO.transform);
            }

            // 2. Resolve Materials (Passing prioritized texture search paths)
            List<Material> materials = importMaterials
                ? NinjaMaterialResolver.ResolveMaterials(
                    objData,
                    texList,
                    assetName,
                    ctx,
                    materialLocation,
                    materialSearch,
                    materialNaming,
                    materialSearchPath,
                    textureSearchPaths)
                : new List<Material>();

            // Apply Per-Material User Overrides from Inspector Remap Table
            if (materialRemaps != null)
            {
                foreach (var remap in materialRemaps)
                {
                    if (remap.overrideMaterial != null && remap.slotIndex >= 0 && remap.slotIndex < materials.Count)
                    {
                        materials[remap.slotIndex] = remap.overrideMaterial;
                    }
                }
            }

            // 3. Dispatch to Mesh Import Mode
            switch (importMode)
            {
                case MeshImportMode.SingleSkinnedMesh:
                    BuildSingleSkinnedMesh(objData, rootGO, outNodeTransforms, materials, scale, assetName, ctx);
                    break;
                case MeshImportMode.CombinedByNode:
                    BuildCombinedNodeMeshes(objData, rootGO, outNodeTransforms, materials, scale, assetName, ctx);
                    break;
                case MeshImportMode.IndividualSubObjects:
                default:
                    BuildIndividualSubObjects(objData, rootGO, outNodeTransforms, materials, scale, assetName, ctx);
                    break;
            }

            return rootGO;
        }

        #region Mode 1: Single Skinned Mesh
        private static void BuildSingleSkinnedMesh(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            float scale,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            MeshBuffer buffer = new MeshBuffer();

            foreach (var subObj in objData.SubObjects)
            {
                foreach (var meshSet in subObj.MeshSets)
                {
                    int fallbackNode = (meshSet.NodeIndex >= 0 && meshSet.NodeIndex < allNodeTransforms.Count) ? meshSet.NodeIndex : 0;
                    buffer.AppendMeshSet(objData, meshSet, scale, null, null, fallbackNode, meshSet.MaterialIndex);
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
        }
        #endregion

        #region Mode 2: Combined Node Meshes
        private static void BuildCombinedNodeMeshes(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            float scale,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
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

                    BuildNodeMeshSection(objData, rootGO, nodeTr, targetGO, sets, matIdx, n, materials, scale, assetName, ctx);
                }
            }
        }

        private static void BuildNodeMeshSection(
            NinjaObject objData,
            GameObject rootGO,
            Transform nodeTr,
            GameObject targetGO,
            List<NinjaMeshSet> meshSets,
            int matIdx,
            int nodeIdx,
            List<Material> materials,
            float scale,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            bool isSkinned = false;
            HashSet<int> boneSet = new HashSet<int>();

            foreach (var ms in meshSets)
            {
                if (ms.VertexListIndex >= 0 && ms.VertexListIndex < objData.VertexLists.Count)
                {
                    var vl = objData.VertexLists[ms.VertexListIndex];
                    if (vl.BoneMatrixIndices != null && vl.BoneMatrixIndices.Count > 0)
                    {
                        isSkinned = true;
                        foreach (int b in vl.BoneMatrixIndices) boneSet.Add(b);
                    }
                }
            }

            List<int> localPalette = new List<int>(boneSet);
            Dictionary<int, int> globalToLocal = new Dictionary<int, int>();
            for (int b = 0; b < localPalette.Count; b++)
            {
                globalToLocal[localPalette[b]] = b;
            }

            Matrix4x4? nodeXform = isSkinned ? (Matrix4x4?)null : nodeTr.worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
            MeshBuffer buffer = new MeshBuffer();

            foreach (var ms in meshSets)
            {
                var vList = objData.VertexLists[ms.VertexListIndex];
                Func<byte, int> remap = isSkinned ? (b) => {
                    if (vList.BoneMatrixIndices != null && b < vList.BoneMatrixIndices.Count)
                    {
                        int g = vList.BoneMatrixIndices[b];
                        if (globalToLocal.TryGetValue(g, out int l)) return l;
                    }
                    return 0;
                } : (Func<byte, int>)null;

                int fallbackLocal = globalToLocal.TryGetValue(nodeIdx, out int lf) ? lf : 0;
                buffer.AppendMeshSet(objData, ms, scale, nodeXform, remap, fallbackLocal, 0);
            }

            Mesh mesh = buffer.BuildMesh($"{assetName}_Node_{nodeIdx}_Mat_{matIdx}");
            if (mesh == null) return;

            Material assignedMat = GetMaterialOrStandard(matIdx, materials);
            if (ctx != null) ctx.AddObjectToAsset($"Mesh_Node_{nodeIdx}_Mat_{matIdx}", mesh);

            if (isSkinned)
            {
                Transform[] localBones = new Transform[localPalette.Count];
                Matrix4x4[] localBinds = new Matrix4x4[localPalette.Count];
                for (int b = 0; b < localPalette.Count; b++)
                {
                    localBones[b] = rootGO.transform.GetChild(localPalette[b]);
                    localBinds[b] = localBones[b].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
                }
                mesh.bindposes = localBinds;
                mesh.RecalculateBounds();

                SkinnedMeshRenderer smr = targetGO.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = localBones;
                smr.rootBone = nodeTr;
                smr.sharedMaterial = assignedMat;
            }
            else
            {
                mesh.RecalculateBounds();
                targetGO.AddComponent<MeshFilter>().sharedMesh = mesh;
                targetGO.AddComponent<MeshRenderer>().sharedMaterial = assignedMat;
            }
        }
        #endregion

        #region Mode 3: Individual Sub-Objects
        private static void BuildIndividualSubObjects(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            float scale,
            string assetName,
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

                    MeshBuffer buffer = new MeshBuffer();
                    buffer.AppendMeshSet(objData, ms, scale, nodeXform, null, 0, 0);

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
            if (vList == null || vList.Vertices == null || vList.Vertices.Count == 0) return null;
            NinjaObject dummy = new NinjaObject();
            dummy.VertexLists.Add(vList);
            dummy.PrimitiveLists.Add(pList);
            NinjaMeshSet ms = new NinjaMeshSet { VertexListIndex = 0, PrimitiveListIndex = 0 };

            MeshBuffer buffer = new MeshBuffer();
            buffer.AppendMeshSet(dummy, ms, scale, transformMatrix, null, 0, 0);
            return buffer.BuildMesh(name);
        }

        public static List<int> DecodeIndices(NinjaPrimitiveList pList)
        {
            List<int> triangles = new List<int>();
            if (pList?.IndexIndices == null || pList.IndexIndices.Count < 3) return triangles;

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
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector4> Tangents = new List<Vector4>();
            public readonly List<Color32> Colors = new List<Color32>();
            public readonly List<Vector2> UVs = new List<Vector2>();
            public readonly List<BoneWeight> BoneWeights = new List<BoneWeight>();
            public readonly Dictionary<int, List<int>> SubmeshTriangles = new Dictionary<int, List<int>>();
            public bool HasWeights;

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
                int baseOffset = Positions.Count;
                bool isSkinned = vList.BoneMatrixIndices != null && vList.BoneMatrixIndices.Count > 0;
                if (isSkinned) HasWeights = true;

                bool applyXform = localTransform.HasValue && localTransform.Value != Matrix4x4.identity;
                Matrix4x4 xform = localTransform.GetValueOrDefault(Matrix4x4.identity);

                for (int v = 0; v < vList.Vertices.Count; v++)
                {
                    NinjaVertex vert = vList.Vertices[v];
                    if (vert == null) continue;

                    Vector3 pos = vert.Position.GetValueOrDefault();
                    pos = new Vector3(-pos.x * scale, pos.y * scale, pos.z * scale);
                    if (applyXform) pos = xform.MultiplyPoint3x4(pos);
                    Positions.Add(pos);

                    Vector3 norm = vert.Normals.GetValueOrDefault(Vector3.up);
                    norm = new Vector3(-norm.x, norm.y, norm.z).normalized;
                    if (applyXform) norm = xform.MultiplyVector(norm).normalized;
                    Normals.Add(norm);

                    Vector3 tan = vert.Tangent.GetValueOrDefault(Vector3.right);
                    Vector3 tanScaled = new Vector3(-tan.x, tan.y, tan.z).normalized;
                    if (applyXform) tanScaled = xform.MultiplyVector(tanScaled).normalized;
                    Tangents.Add(new Vector4(tanScaled.x, tanScaled.y, tanScaled.z, 1.0f));

                    if (vert.TextureCoordinates != null && vert.TextureCoordinates.Count > 0)
                        UVs.Add(new Vector2(vert.TextureCoordinates[0].x, 1.0f - vert.TextureCoordinates[0].y));
                    else
                        UVs.Add(Vector2.zero);

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
                }

                if (!SubmeshTriangles.TryGetValue(submeshKey, out List<int> tris))
                {
                    tris = new List<int>();
                    SubmeshTriangles[submeshKey] = tris;
                }

                List<int> decoded = DecodeIndices(pList);
                for (int t = 0; t < decoded.Count; t++)
                    tris.Add(baseOffset + decoded[t]);
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
                mesh.colors32 = Colors.ToArray();
                if (HasWeights) mesh.boneWeights = BoneWeights.ToArray();

                var sortedKeys = GetSortedSubmeshKeys();
                mesh.subMeshCount = sortedKeys.Count;
                for (int i = 0; i < sortedKeys.Count; i++)
                    mesh.SetTriangles(SubmeshTriangles[sortedKeys[i]], i);

                mesh.RecalculateBounds();
                return mesh;
            }
        }
        #endregion
    }
}