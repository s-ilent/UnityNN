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
        /// <summary>
        /// Resolves a NinjaObject into a Unity GameObject hierarchy with Meshes and Materials.
        /// </summary>
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
            out List<Transform> outNodeTransforms)
        {
            outNodeTransforms = new List<Transform>();
            if (objData == null) return null;

            string nameSource;
            NinjaNodeNameList resolvedNodeNames = NinjaNodeNameResolver.ResolveNodeNames(objData, null, ctx?.assetPath, ctx, out nameSource);

            GameObject rootGO = new GameObject(assetName);

            // 1. Build Full Node Hierarchy
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

                nodeGO.transform.localPosition = pos;
                nodeGO.transform.localEulerAngles = new Vector3(rot.x, -rot.y, -rot.z);
                nodeGO.transform.localScale = node.Scaling;

                if (node.ParentIndex >= 0 && node.ParentIndex < outNodeTransforms.Count)
                    nodeGO.transform.SetParent(outNodeTransforms[node.ParentIndex], false);
                else
                    nodeGO.transform.SetParent(rootGO.transform, false);

                outNodeTransforms.Add(nodeGO.transform);
            }

            // 2. Resolve Materials
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

            // 3. Dispatch to Target Mesh Import Mode
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

        #region Mode 1: Single Skinned Mesh (Unified Character Skeleton)
        private static void BuildSingleSkinnedMesh(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            float scale,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            List<Vector3> allPositions = new List<Vector3>();
            List<Vector3> allNormals = new List<Vector3>();
            List<Vector4> allTangents = new List<Vector4>();
            List<Color32> allColors = new List<Color32>();
            List<Vector2> allUVs = new List<Vector2>();
            List<BoneWeight> allBoneWeights = new List<BoneWeight>();

            Dictionary<int, List<int>> materialTriangles = new Dictionary<int, List<int>>();
            HashSet<int> usedMaterialIndices = new HashSet<int>();

            foreach (var subObj in objData.SubObjects)
            {
                foreach (var meshSet in subObj.MeshSets)
                {
                    if (meshSet.VertexListIndex < 0 || meshSet.VertexListIndex >= objData.VertexLists.Count ||
                        meshSet.PrimitiveListIndex < 0 || meshSet.PrimitiveListIndex >= objData.PrimitiveLists.Count)
                    {
                        continue;
                    }

                    NinjaVertexList vList = objData.VertexLists[meshSet.VertexListIndex];
                    NinjaPrimitiveList pList = objData.PrimitiveLists[meshSet.PrimitiveListIndex];
                    int matIdx = meshSet.MaterialIndex;

                    if (!materialTriangles.ContainsKey(matIdx))
                    {
                        materialTriangles[matIdx] = new List<int>();
                    }
                    usedMaterialIndices.Add(matIdx);

                    int baseVertexOffset = allPositions.Count;
                    bool isSkinned = vList.BoneMatrixIndices != null && vList.BoneMatrixIndices.Count > 0;
                    int fallbackNodeIdx = (meshSet.NodeIndex >= 0 && meshSet.NodeIndex < allNodeTransforms.Count) ? meshSet.NodeIndex : 0;

                    // Append Vertices with globalized bone palette remapping
                    for (int v = 0; v < vList.Vertices.Count; v++)
                    {
                        NinjaVertex vert = vList.Vertices[v];
                        if (vert == null) continue;

                        Vector3 pos = vert.Position.GetValueOrDefault();
                        pos.x *= -1f * scale;
                        pos.y *= scale;
                        pos.z *= scale;
                        allPositions.Add(pos);

                        Vector3 norm = vert.Normals.GetValueOrDefault(Vector3.up);
                        norm.x *= -1f;
                        allNormals.Add(norm.normalized);

                        Vector3 tan = vert.Tangent.GetValueOrDefault(Vector3.right);
                        allTangents.Add(new Vector4(-tan.x, tan.y, tan.z, 1.0f));

                        if (vert.TextureCoordinates != null && vert.TextureCoordinates.Count > 0)
                        {
                            Vector2 uv = vert.TextureCoordinates[0];
                            allUVs.Add(new Vector2(uv.x, 1.0f - uv.y));
                        }
                        else
                        {
                            allUVs.Add(Vector2.zero);
                        }

                        if (vert.VertexColours != null && vert.VertexColours.Length >= 4)
                        {
                            allColors.Add(new Color32(vert.VertexColours[2], vert.VertexColours[1], vert.VertexColours[0], vert.VertexColours[3]));
                        }
                        else
                        {
                            allColors.Add(new Color32(255, 255, 255, 255));
                        }

                        BoneWeight bw = new BoneWeight();
                        if (isSkinned && vert.Weight.HasValue && vert.MatrixIndices != null && vert.MatrixIndices.Length >= 4)
                        {
                            // Remap local palette index -> global node index in allNodeTransforms
                            bw.boneIndex0 = RemapGlobalBoneIndex(vert.MatrixIndices[0], vList.BoneMatrixIndices, fallbackNodeIdx);
                            bw.boneIndex1 = RemapGlobalBoneIndex(vert.MatrixIndices[1], vList.BoneMatrixIndices, fallbackNodeIdx);
                            bw.boneIndex2 = RemapGlobalBoneIndex(vert.MatrixIndices[2], vList.BoneMatrixIndices, fallbackNodeIdx);
                            bw.boneIndex3 = RemapGlobalBoneIndex(vert.MatrixIndices[3], vList.BoneMatrixIndices, fallbackNodeIdx);

                            Vector3 w = vert.Weight.Value;
                            bw.weight0 = w.x;
                            bw.weight1 = w.y;
                            bw.weight2 = w.z;
                            bw.weight3 = Mathf.Max(0f, 1.0f - (w.x + w.y + w.z));
                        }
                        else
                        {
                            // Rigid 100% weight to owning node
                            bw.boneIndex0 = fallbackNodeIdx;
                            bw.weight0 = 1.0f;
                        }
                        allBoneWeights.Add(bw);
                    }

                    // Decode and offset triangle indices into the target material's submesh list
                    List<int> decodedTris = DecodeIndices(pList);
                    for (int t = 0; t < decodedTris.Count; t++)
                    {
                        materialTriangles[matIdx].Add(baseVertexOffset + decodedTris[t]);
                    }
                }
            }

            if (allPositions.Count == 0) return;

            Mesh mesh = new Mesh { name = $"{assetName}_SkinnedMesh" };
            if (allPositions.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = allPositions.ToArray();
            mesh.normals = allNormals.ToArray();
            mesh.tangents = allTangents.ToArray();
            mesh.uv = allUVs.ToArray();
            mesh.colors32 = allColors.ToArray();
            mesh.boneWeights = allBoneWeights.ToArray();

            // Set up multi-material submesh layout
            List<int> sortedMatKeys = new List<int>(materialTriangles.Keys);
            sortedMatKeys.Sort();

            mesh.subMeshCount = sortedMatKeys.Count;
            Material[] assignedMaterials = new Material[sortedMatKeys.Count];

            for (int s = 0; s < sortedMatKeys.Count; s++)
            {
                int mKey = sortedMatKeys[s];
                mesh.SetTriangles(materialTriangles[mKey], s);
                assignedMaterials[s] = (mKey >= 0 && mKey < materials.Count && materials[mKey] != null)
                    ? materials[mKey] : new Material(Shader.Find("Standard"));
            }

            // Build full bindposes matrix array from transform hierarchy
            Transform[] bones = allNodeTransforms.ToArray();
            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                bindPoses[b] = bones[b].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
            }
            mesh.bindposes = bindPoses;
            mesh.RecalculateBounds();

            if (ctx != null)
            {
                ctx.AddObjectToAsset("SkinnedMesh", mesh);
            }

            SkinnedMeshRenderer smr = rootGO.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones.Length > 0 ? bones[0] : rootGO.transform;
            smr.sharedMaterials = assignedMaterials;
        }

        private static int RemapGlobalBoneIndex(byte localPaletteIdx, List<int> bonePalette, int fallbackNodeIdx)
        {
            if (bonePalette != null && localPaletteIdx < bonePalette.Count)
            {
                return bonePalette[localPaletteIdx];
            }
            return fallbackNodeIdx;
        }
        #endregion

        #region Mode 2: Combined Node Meshes (Multi-Material per Node)
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
                Transform nodeTransform = allNodeTransforms[n];
                List<NinjaMeshSet> nodeMeshSets = new List<NinjaMeshSet>();

                foreach (var subObj in objData.SubObjects)
                {
                    foreach (var ms in subObj.MeshSets)
                    {
                        if (ms.NodeIndex == n)
                        {
                            nodeMeshSets.Add(ms);
                        }
                    }
                }

                if (nodeMeshSets.Count == 0) continue;

                bool nodeIsSkinned = false;
                HashSet<int> localBoneIndexSet = new HashSet<int>();

                foreach (var ms in nodeMeshSets)
                {
                    if (ms.VertexListIndex >= 0 && ms.VertexListIndex < objData.VertexLists.Count)
                    {
                        var vl = objData.VertexLists[ms.VertexListIndex];
                        if (vl.BoneMatrixIndices != null && vl.BoneMatrixIndices.Count > 0)
                        {
                            nodeIsSkinned = true;
                            foreach (int bIdx in vl.BoneMatrixIndices)
                            {
                                if (bIdx >= 0 && bIdx < allNodeTransforms.Count)
                                    localBoneIndexSet.Add(bIdx);
                            }
                        }
                    }
                }

                List<int> localBonePalette = new List<int>(localBoneIndexSet);
                Dictionary<int, int> globalToLocalBoneMap = new Dictionary<int, int>();
                for (int b = 0; b < localBonePalette.Count; b++)
                {
                    globalToLocalBoneMap[localBonePalette[b]] = b;
                }

                List<Vector3> positions = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<Vector4> tangents = new List<Vector4>();
                List<Color32> colors = new List<Color32>();
                List<Vector2> uvs = new List<Vector2>();
                List<BoneWeight> boneWeights = new List<BoneWeight>();

                Dictionary<int, List<int>> materialTriangles = new Dictionary<int, List<int>>();

                Matrix4x4 nodeToLocal = nodeTransform.worldToLocalMatrix * rootGO.transform.localToWorldMatrix;

                foreach (var ms in nodeMeshSets)
                {
                    if (ms.VertexListIndex < 0 || ms.VertexListIndex >= objData.VertexLists.Count ||
                        ms.PrimitiveListIndex < 0 || ms.PrimitiveListIndex >= objData.PrimitiveLists.Count)
                    {
                        continue;
                    }

                    var vList = objData.VertexLists[ms.VertexListIndex];
                    var pList = objData.PrimitiveLists[ms.PrimitiveListIndex];
                    int matIdx = ms.MaterialIndex;

                    if (!materialTriangles.ContainsKey(matIdx))
                    {
                        materialTriangles[matIdx] = new List<int>();
                    }

                    int baseOffset = positions.Count;
                    bool meshSetSkinned = vList.BoneMatrixIndices != null && vList.BoneMatrixIndices.Count > 0;

                    for (int v = 0; v < vList.Vertices.Count; v++)
                    {
                        NinjaVertex vert = vList.Vertices[v];
                        if (vert == null) continue;

                        Vector3 pos = vert.Position.GetValueOrDefault();
                        pos.x *= -1f * scale;
                        pos.y *= scale;
                        pos.z *= scale;

                        Vector3 norm = vert.Normals.GetValueOrDefault(Vector3.up);
                        norm.x *= -1f;

                        if (!nodeIsSkinned)
                        {
                            pos = nodeToLocal.MultiplyPoint3x4(pos);
                            norm = nodeToLocal.MultiplyVector(norm).normalized;
                        }

                        positions.Add(pos);
                        normals.Add(norm);

                        Vector3 tan = vert.Tangent.GetValueOrDefault(Vector3.right);
                        Vector3 tanScaled = new Vector3(-tan.x, tan.y, tan.z);
                        if (!nodeIsSkinned) tanScaled = nodeToLocal.MultiplyVector(tanScaled).normalized;
                        tangents.Add(new Vector4(tanScaled.x, tanScaled.y, tanScaled.z, 1.0f));

                        if (vert.TextureCoordinates != null && vert.TextureCoordinates.Count > 0)
                        {
                            Vector2 uv = vert.TextureCoordinates[0];
                            uvs.Add(new Vector2(uv.x, 1.0f - uv.y));
                        }
                        else
                        {
                            uvs.Add(Vector2.zero);
                        }

                        if (vert.VertexColours != null && vert.VertexColours.Length >= 4)
                        {
                            colors.Add(new Color32(vert.VertexColours[2], vert.VertexColours[1], vert.VertexColours[0], vert.VertexColours[3]));
                        }
                        else
                        {
                            colors.Add(new Color32(255, 255, 255, 255));
                        }

                        if (nodeIsSkinned)
                        {
                            BoneWeight bw = new BoneWeight();
                            if (meshSetSkinned && vert.Weight.HasValue && vert.MatrixIndices != null && vert.MatrixIndices.Length >= 4)
                            {
                                bw.boneIndex0 = RemapLocalBoneIndex(vert.MatrixIndices[0], vList.BoneMatrixIndices, globalToLocalBoneMap);
                                bw.boneIndex1 = RemapLocalBoneIndex(vert.MatrixIndices[1], vList.BoneMatrixIndices, globalToLocalBoneMap);
                                bw.boneIndex2 = RemapLocalBoneIndex(vert.MatrixIndices[2], vList.BoneMatrixIndices, globalToLocalBoneMap);
                                bw.boneIndex3 = RemapLocalBoneIndex(vert.MatrixIndices[3], vList.BoneMatrixIndices, globalToLocalBoneMap);

                                Vector3 w = vert.Weight.Value;
                                bw.weight0 = w.x;
                                bw.weight1 = w.y;
                                bw.weight2 = w.z;
                                bw.weight3 = Mathf.Max(0f, 1.0f - (w.x + w.y + w.z));
                            }
                            else
                            {
                                int localIdx = globalToLocalBoneMap.ContainsKey(n) ? globalToLocalBoneMap[n] : 0;
                                bw.boneIndex0 = localIdx;
                                bw.weight0 = 1.0f;
                            }
                            boneWeights.Add(bw);
                        }
                    }

                    List<int> decodedTris = DecodeIndices(pList);
                    for (int t = 0; t < decodedTris.Count; t++)
                    {
                        materialTriangles[matIdx].Add(baseOffset + decodedTris[t]);
                    }
                }

                if (positions.Count == 0) continue;

                Mesh mesh = new Mesh { name = $"{assetName}_Node_{n}_Mesh" };
                if (positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.vertices = positions.ToArray();
                mesh.normals = normals.ToArray();
                mesh.tangents = tangents.ToArray();
                mesh.uv = uvs.ToArray();
                mesh.colors32 = colors.ToArray();
                if (nodeIsSkinned) mesh.boneWeights = boneWeights.ToArray();

                List<int> sortedMatKeys = new List<int>(materialTriangles.Keys);
                sortedMatKeys.Sort();
                mesh.subMeshCount = sortedMatKeys.Count;
                Material[] assignedMaterials = new Material[sortedMatKeys.Count];

                for (int s = 0; s < sortedMatKeys.Count; s++)
                {
                    int mKey = sortedMatKeys[s];
                    mesh.SetTriangles(materialTriangles[mKey], s);
                    assignedMaterials[s] = (mKey >= 0 && mKey < materials.Count && materials[mKey] != null)
                        ? materials[mKey] : new Material(Shader.Find("Standard"));
                }

                if (nodeIsSkinned)
                {
                    Transform[] localBones = new Transform[localBonePalette.Count];
                    Matrix4x4[] localBindposes = new Matrix4x4[localBonePalette.Count];
                    for (int b = 0; b < localBonePalette.Count; b++)
                    {
                        int gIdx = localBonePalette[b];
                        localBones[b] = allNodeTransforms[gIdx];
                        localBindposes[b] = allNodeTransforms[gIdx].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
                    }

                    mesh.bindposes = localBindposes;
                    mesh.RecalculateBounds();

                    if (ctx != null) ctx.AddObjectToAsset($"NodeMesh_{n}", mesh);

                    SkinnedMeshRenderer smr = nodeTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = mesh;
                    smr.bones = localBones;
                    smr.rootBone = nodeTransform;
                    smr.sharedMaterials = assignedMaterials;
                }
                else
                {
                    mesh.RecalculateBounds();
                    if (ctx != null) ctx.AddObjectToAsset($"NodeMesh_{n}", mesh);

                    MeshFilter mf = nodeTransform.gameObject.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    MeshRenderer mr = nodeTransform.gameObject.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = assignedMaterials;
                }
            }
        }

        private static int RemapLocalBoneIndex(byte paletteIndex, List<int> vListBonePalette, Dictionary<int, int> globalToLocalMap)
        {
            if (vListBonePalette != null && paletteIndex < vListBonePalette.Count)
            {
                int globalNodeIdx = vListBonePalette[paletteIndex];
                if (globalToLocalMap.TryGetValue(globalNodeIdx, out int localIdx))
                {
                    return localIdx;
                }
            }
            return 0;
        }
        #endregion

        #region Mode 3: Individual Sub-Objects (Legacy Hierarchy)
        private static void BuildIndividualSubObjects(
            NinjaObject objData,
            GameObject rootGO,
            List<Transform> allNodeTransforms,
            List<Material> materials,
            float scale,
            string assetName,
            UnityEditor.AssetImporters.AssetImportContext ctx)
        {
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

                    Transform parentNode = (meshSet.NodeIndex >= 0 && meshSet.NodeIndex < allNodeTransforms.Count)
                        ? allNodeTransforms[meshSet.NodeIndex] : rootGO.transform;

                    bool isSkinned = vList.BoneMatrixIndices.Count > 0;

                    Matrix4x4? nodeLocalXform = null;
                    if (!isSkinned && parentNode != rootGO.transform)
                    {
                        nodeLocalXform = parentNode.worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
                    }

                    Mesh mesh = CreateUnityMesh(vList, pList, scale, $"{assetName}_Mesh_{subObjIndex}", nodeLocalXform);
                    if (mesh == null) continue;

                    if (ctx != null) ctx.AddObjectToAsset($"Mesh_{subObjIndex}", mesh);

                    GameObject meshGO = new GameObject($"SubObj_{subObjIndex}");
                    meshGO.transform.SetParent(parentNode, false);

                    Material mat = (meshSet.MaterialIndex >= 0 && meshSet.MaterialIndex < materials.Count)
                        ? materials[meshSet.MaterialIndex] : new Material(Shader.Find("Standard"));

                    if (isSkinned)
                    {
                        SkinnedMeshRenderer smr = meshGO.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.sharedMaterial = mat;

                        Transform[] bones = new Transform[vList.BoneMatrixIndices.Count];
                        Matrix4x4[] subBindPoses = new Matrix4x4[vList.BoneMatrixIndices.Count];
                        for (int b = 0; b < vList.BoneMatrixIndices.Count; b++)
                        {
                            int nodeIdx = vList.BoneMatrixIndices[b];
                            if (nodeIdx >= 0 && nodeIdx < allNodeTransforms.Count)
                            {
                                bones[b] = allNodeTransforms[nodeIdx];
                                subBindPoses[b] = allNodeTransforms[nodeIdx].worldToLocalMatrix * rootGO.transform.localToWorldMatrix;
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
        }
        #endregion

        #region Geometry & Triangulation Utility
        public static Mesh CreateUnityMesh(
            NinjaVertexList vList,
            NinjaPrimitiveList pList,
            float scale,
            string name,
            Matrix4x4? transformMatrix = null)
        {
            if (vList == null || vList.Vertices == null || vList.Vertices.Count == 0) return null;

            Mesh mesh = new Mesh { name = name };
            if (vList.Vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            Vector3[] positions = new Vector3[vList.Vertices.Count];
            Vector3[] normals = new Vector3[vList.Vertices.Count];
            Vector4[] tangents = new Vector4[vList.Vertices.Count];
            Color32[] colors = new Color32[vList.Vertices.Count];
            Vector2[] uv0 = new Vector2[vList.Vertices.Count];
            BoneWeight[] boneWeights = new BoneWeight[vList.Vertices.Count];

            bool hasNormals = false, hasTangents = false, hasColors = false, hasUV = false, hasWeights = false;

            Matrix4x4 xform = transformMatrix.HasValue ? transformMatrix.Value : Matrix4x4.identity;
            bool applyXform = transformMatrix.HasValue && transformMatrix.Value != Matrix4x4.identity;

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

                    if (applyXform) pos = xform.MultiplyPoint3x4(pos);
                    positions[i] = pos;
                }

                if (v.Normals.HasValue)
                {
                    hasNormals = true;
                    Vector3 n = v.Normals.Value;
                    n.x *= -1f;

                    if (applyXform) n = xform.MultiplyVector(n).normalized;
                    normals[i] = n;
                }

                if (v.Tangent.HasValue)
                {
                    hasTangents = true;
                    Vector3 t = v.Tangent.Value;
                    Vector3 tScaled = new Vector3(-t.x, t.y, t.z);

                    if (applyXform) tScaled = xform.MultiplyVector(tScaled).normalized;
                    tangents[i] = new Vector4(tScaled.x, tScaled.y, tScaled.z, 1.0f);
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
            if (pList == null || pList.IndexIndices == null || pList.IndexIndices.Count < 3) return triangles;

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

                        if (a == b || b == c || a == c) continue;

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
                for (int i = 0; i < pList.IndexIndices.Count - 2; i += 3)
                {
                    ushort a = pList.IndexIndices[i];
                    ushort b = pList.IndexIndices[i + 1];
                    ushort c = pList.IndexIndices[i + 2];

                    if (a == b || b == c || a == c) continue;

                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                }
            }

            return triangles;
        }
        #endregion
    }
}