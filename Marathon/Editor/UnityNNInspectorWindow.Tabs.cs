// File: Marathon/Editor/UnityNNInspectorWindow.Tabs.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Overview Tab
        private void DrawOverviewTab()
        {
            var data = m_LoadedNinjaData.Data;

            EditorGUILayout.LabelField("Data Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Model Object (NXOB):", data.Object != null ? "Present" : "Absent");
            EditorGUILayout.LabelField("Node Name List (NXNN):", data.NodeNameList != null ? "Present" : "Absent");
            EditorGUILayout.LabelField("Texture List (NXTL):", data.TextureList != null ? "Present" : "Absent");
            EditorGUILayout.LabelField("Node Motion (NXNM/NXMA):", data.Motion != null ? "Present" : "Absent");
            EditorGUILayout.LabelField("Material Motion (NXNV):", data.MaterialMotion != null ? "Present" : "Absent");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chunk Summary", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Object Chunk (NXOB):", data.Object != null ? $"Present ({data.Object.Nodes.Count} nodes, {data.Object.SubObjects.Count} sub-objects)" : "Absent");
            EditorGUILayout.LabelField("Node Motion (NXMA/NXMO):", data.Motion != null ? $"Present ({data.Motion.ChunkID}, {data.Motion.SubMotions.Count} tracks, {data.Motion.Framerate} FPS)" : "Absent");
            EditorGUILayout.LabelField("Material Motion (NXNV):", data.MaterialMotion != null ? $"Present ({data.MaterialMotion.ChunkID}, {data.MaterialMotion.SubMotions.Count} tracks, {data.MaterialMotion.Framerate} FPS)" : "Absent");
            EditorGUILayout.LabelField("Texture List (NXTL):", data.TextureList != null ? $"Present ({data.TextureList.NinjaTextureFiles.Count} textures)" : "Absent");
            EditorGUILayout.LabelField("Node Name List (NXNN):", data.NodeNameList != null ? $"Present ({data.NodeNameList.NinjaNodeNames.Count} names, Sort: {data.NodeNameList.Type})" : "Absent");
            EditorGUILayout.LabelField("Effect List (NXEF):", data.EffectList != null ? $"Present ({data.EffectList.NinjaEffectFiles.Count} effects, {data.EffectList.NinjaTechniqueNames.Count} techniques)" : "Absent");
            EditorGUILayout.LabelField("Camera Chunk (NXCA):", data.Camera != null ? $"Present ({data.Camera.Type})" : "Absent");
            EditorGUILayout.LabelField("Light Chunk (NXLI):", data.Light != null ? $"Present ({data.Light.Type})" : "Absent");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            if (data.Object != null)
            {
                EditorGUILayout.LabelField("Object Metadata", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Bounding Center:", data.Object.Center.ToString("F4"));
                EditorGUILayout.LabelField("Bounding Radius:", data.Object.Radius.ToString("F4"));
                EditorGUILayout.LabelField("Max Node Depth:", data.Object.MaxNodeDepth.ToString());
                EditorGUILayout.LabelField("Matrix Index Count:", data.Object.MatrixIndexCount.ToString());
                EditorGUILayout.LabelField("Texture Count:", data.Object.TextureCount.ToString());
                EditorGUILayout.LabelField("Node Count:", data.Object.Nodes != null ? data.Object.Nodes.Count.ToString() : "0");
                EditorGUILayout.LabelField("SubObject Count:", data.Object.SubObjects != null ? data.Object.SubObjects.Count.ToString() : "0");
                EditorGUILayout.LabelField("Material Count:", data.Object.Materials != null ? data.Object.Materials.Count.ToString() : "0");
                EditorGUILayout.LabelField("Material Colours Count:", data.Object.MaterialColours != null ? data.Object.MaterialColours.Count.ToString() : "0");
                EditorGUILayout.LabelField("Material Logics Count:", data.Object.MaterialLogics != null ? data.Object.MaterialLogics.Count.ToString() : "0");
                EditorGUILayout.LabelField("Texture Map Count:", data.Object.TextureMaps != null ? data.Object.TextureMaps.Count.ToString() : "0");
                EditorGUILayout.LabelField("Vertex List Count:", data.Object.VertexLists != null ? data.Object.VertexLists.Count.ToString() : "0");
                EditorGUILayout.LabelField("Primitive List Count:", data.Object.PrimitiveLists != null ? data.Object.PrimitiveLists.Count.ToString() : "0");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.Motion != null)
            {
                EditorGUILayout.LabelField("Node Motion Metadata", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Chunk ID:", data.Motion.ChunkID ?? "N/A");
                EditorGUILayout.LabelField("Motion Type:", data.Motion.Type.ToString());
                EditorGUILayout.LabelField("Frame Range:", $"{data.Motion.StartFrame:F2} to {data.Motion.EndFrame:F2}");
                EditorGUILayout.LabelField("Framerate:", $"{data.Motion.Framerate:F2} FPS");
                EditorGUILayout.LabelField("SubMotions Count:", data.Motion.SubMotions != null ? data.Motion.SubMotions.Count.ToString() : "0");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.MaterialMotion != null)
            {
                EditorGUILayout.LabelField("Material Motion Metadata", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Chunk ID:", data.MaterialMotion.ChunkID ?? "N/A");
                EditorGUILayout.LabelField("Motion Type:", data.MaterialMotion.Type.ToString());
                EditorGUILayout.LabelField("Frame Range:", $"{data.MaterialMotion.StartFrame:F2} to {data.MaterialMotion.EndFrame:F2}");
                EditorGUILayout.LabelField("Framerate:", $"{data.MaterialMotion.Framerate:F2} FPS");
                EditorGUILayout.LabelField("SubMotions Count:", data.MaterialMotion.SubMotions != null ? data.MaterialMotion.SubMotions.Count.ToString() : "0");
                EditorGUI.indentLevel--;
            }
        }
        #endregion

        #region Node Tree Tab
        private void DrawNodeTreeTab()
        {
            var data = m_LoadedNinjaData.Data;
            if (data.Object == null || data.Object.Nodes == null)
            {
                EditorGUILayout.HelpBox("No Object/Node tree data present in this file.", MessageType.Info);
                return;
            }

            var nodes = data.Object.Nodes;

            EditorGUILayout.LabelField("Node Tree", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Nodes: {nodes.Count}", EditorStyles.boldLabel);
            if (GUILayout.Button(m_ExpandAllNodes ? "Collapse All" : "Expand All", GUILayout.Width(100)))
            {
                m_ExpandAllNodes = !m_ExpandAllNodes;
                for (int i = 0; i < nodes.Count; i++)
                    m_NodeFoldouts[i] = m_ExpandAllNodes;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
            m_NodeSearchFilter = EditorGUILayout.TextField(m_NodeSearchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(50))) m_NodeSearchFilter = "";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                string displayName = string.IsNullOrEmpty(n.Name) ? $"Node_{i:0000}" : n.Name;

                if (!string.IsNullOrEmpty(m_NodeSearchFilter) &&
                    !displayName.ToLower().Contains(m_NodeSearchFilter.ToLower()) &&
                    !i.ToString().Contains(m_NodeSearchFilter))
                {
                    continue;
                }

                if (!m_NodeFoldouts.ContainsKey(i))
                    m_NodeFoldouts[i] = m_ExpandAllNodes;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                m_NodeFoldouts[i] = EditorGUILayout.Foldout(
                    m_NodeFoldouts[i],
                    $"[{i}] {displayName} (Type: {n.Type})",
                    true
                );

                if (m_NodeFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Name:", n.Name ?? "");
                    EditorGUILayout.LabelField("Node Type:", n.Type.ToString());
                    EditorGUILayout.LabelField("Matrix Index:", n.MatrixIndex.ToString());
                    EditorGUILayout.LabelField("User Defined:", n.UserDefined.ToString("X8"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Hierarchy Indices:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Parent: {n.ParentIndex} | Child: {n.ChildIndex} | Sibling: {n.SiblingIndex}");

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Transform:", EditorStyles.boldLabel);
                    EditorGUILayout.Vector3Field("Translation", n.Translation);
                    EditorGUILayout.Vector3Field("Rotation", n.Rotation);
                    EditorGUILayout.Vector3Field("Scaling", n.Scaling);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Bounding Data:", EditorStyles.boldLabel);
                    EditorGUILayout.Vector3Field("Center", n.Center);
                    EditorGUILayout.FloatField("Radius", n.Radius);
                    EditorGUILayout.Vector3Field("Bounding Box", n.BoundingBox);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Inverse Initial Matrix:", EditorStyles.boldLabel);
                    Matrix4x4 mat = n.InvInitMatrix;
                    EditorGUILayout.LabelField($"R0: {mat.m00:F4}, {mat.m01:F4}, {mat.m02:F4}, {mat.m03:F4}");
                    EditorGUILayout.LabelField($"R1: {mat.m10:F4}, {mat.m11:F4}, {mat.m12:F4}, {mat.m13:F4}");
                    EditorGUILayout.LabelField($"R2: {mat.m20:F4}, {mat.m21:F4}, {mat.m22:F4}, {mat.m23:F4}");
                    EditorGUILayout.LabelField($"R3: {mat.m30:F4}, {mat.m31:F4}, {mat.m32:F4}, {mat.m33:F4}");

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
        }
        #endregion

        #region Meshes Tab
        private void DrawMeshesTab()
        {
            var data = m_LoadedNinjaData.Data;
            if (data.Object == null)
            {
                EditorGUILayout.HelpBox("No Object/Mesh data present in this file.", MessageType.Info);
                return;
            }

            var obj = data.Object;

            if (obj.SubObjects != null)
            {
                EditorGUILayout.LabelField("SubObjects & Mesh Sets", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total SubObjects: {obj.SubObjects.Count}");

                for (int i = 0; i < obj.SubObjects.Count; i++)
                {
                    var sub = obj.SubObjects[i];
                    if (sub == null) continue;

                    if (!m_SubObjectFoldouts.ContainsKey(i)) m_SubObjectFoldouts[i] = false;

                    int meshSetCount = sub.MeshSets != null ? sub.MeshSets.Count : 0;
                    int texCount = sub.TextureIndices != null ? sub.TextureIndices.Count : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_SubObjectFoldouts[i] = EditorGUILayout.Foldout(
                        m_SubObjectFoldouts[i],
                        $"SubObject [{i}] - Type: {sub.Type} | MeshSets: {meshSetCount} | Textures: {texCount}",
                        true
                    );

                    if (m_SubObjectFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("SubObject Type:", sub.Type.ToString());

                        if (sub.TextureIndices != null && sub.TextureIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField("Texture Indices:", string.Join(", ", sub.TextureIndices));
                        }

                        if (sub.MeshSets != null && sub.MeshSets.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Mesh Sets:", EditorStyles.boldLabel);

                            for (int j = 0; j < sub.MeshSets.Count; j++)
                            {
                                var ms = sub.MeshSets[j];
                                if (ms == null) continue;

                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                EditorGUILayout.LabelField($"MeshSet [{j}]", EditorStyles.boldLabel);
                                EditorGUILayout.LabelField($"Node Index: {ms.NodeIndex} | Matrix Index: {ms.MatrixIndex}");
                                EditorGUILayout.LabelField($"Material Index: {ms.MaterialIndex} | Shader Index: {ms.ShaderIndex}");
                                EditorGUILayout.LabelField($"Vertex List Index: {ms.VertexListIndex} | Primitive List Index: {ms.PrimitiveListIndex}");
                                EditorGUILayout.Vector3Field("Center", ms.Center);
                                EditorGUILayout.FloatField("Radius", ms.Radius);
                                EditorGUILayout.EndVertical();
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (obj.VertexLists != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Vertex Lists", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Vertex Lists: {obj.VertexLists.Count}");

                for (int i = 0; i < obj.VertexLists.Count; i++)
                {
                    var vl = obj.VertexLists[i];
                    if (vl == null) continue;

                    if (!m_VertexListFoldouts.ContainsKey(i)) m_VertexListFoldouts[i] = false;
                    if (!m_VertexListPages.ContainsKey(i)) m_VertexListPages[i] = 0;

                    int vCount = vl.Vertices != null ? vl.Vertices.Count : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_VertexListFoldouts[i] = EditorGUILayout.Foldout(
                        m_VertexListFoldouts[i],
                        $"Vertex List [{i}] - Count: {vCount} | Format: {vl.Format} | FVF: {vl.FlexibleVertexFormat}",
                        true
                    );

                    if (m_VertexListFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Type:", vl.Type.ToString());
                        EditorGUILayout.LabelField("Format:", vl.Format.ToString());
                        EditorGUILayout.LabelField("Flexible Vertex Format:", vl.FlexibleVertexFormat.ToString());
                        EditorGUILayout.LabelField("HDR Flags:", $"Common: {vl.HDRCommon}, Data: {vl.HDRData}, Lock: {vl.HDRLock}");

                        if (vl.BoneMatrixIndices != null && vl.BoneMatrixIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField("Bone Matrix Indices:", string.Join(", ", vl.BoneMatrixIndices));
                        }

                        if (vl.Vertices != null && vl.Vertices.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Vertices Inspection:", EditorStyles.boldLabel);

                            int totalVertices = vl.Vertices.Count;
                            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalVertices / ITEMS_PER_PAGE));
                            int currentPage = m_VertexListPages[i];

                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("Prev Page", GUILayout.Width(80)) && currentPage > 0)
                            {
                                currentPage--;
                            }
                            EditorGUILayout.LabelField($"Page {currentPage + 1} / {totalPages} (Vertices {currentPage * ITEMS_PER_PAGE} - {Mathf.Min(totalVertices, (currentPage + 1) * ITEMS_PER_PAGE) - 1})");
                            if (GUILayout.Button("Next Page", GUILayout.Width(80)) && currentPage < totalPages - 1)
                            {
                                currentPage++;
                            }
                            m_VertexListPages[i] = currentPage;
                            EditorGUILayout.EndHorizontal();

                            int startIdx = currentPage * ITEMS_PER_PAGE;
                            int endIdx = Mathf.Min(totalVertices, (currentPage + 1) * ITEMS_PER_PAGE);

                            for (int vIdx = startIdx; vIdx < endIdx; vIdx++)
                            {
                                var v = vl.Vertices[vIdx];
                                if (v == null) continue;

                                EditorGUILayout.LabelField($"Vertex [{vIdx}]:", EditorStyles.miniBoldLabel);
                                EditorGUI.indentLevel++;

                                if (v.Position.HasValue) EditorGUILayout.Vector3Field("Pos", v.Position.Value);
                                if (v.Normals.HasValue) EditorGUILayout.Vector3Field("Normal", v.Normals.Value);
                                if (v.Tangent.HasValue) EditorGUILayout.Vector3Field("Tangent", v.Tangent.Value);
                                if (v.Binormals.HasValue) EditorGUILayout.Vector3Field("Binormal", v.Binormals.Value);
                                if (v.Weight.HasValue) EditorGUILayout.Vector3Field("Weight", v.Weight.Value);

                                if (v.MatrixIndices != null && v.MatrixIndices.Length > 0)
                                    EditorGUILayout.LabelField("Matrix Indices:", string.Join(", ", v.MatrixIndices));

                                if (v.VertexColours != null && v.VertexColours.Length >= 4)
                                    EditorGUILayout.ColorField("Color", new Color32(v.VertexColours[0], v.VertexColours[1], v.VertexColours[2], v.VertexColours[3]));

                                if (v.VertexColours2 != null && v.VertexColours2.Length >= 4)
                                    EditorGUILayout.ColorField("Color2", new Color32(v.VertexColours2[0], v.VertexColours2[1], v.VertexColours2[2], v.VertexColours2[3]));

                                if (v.TextureCoordinates != null && v.TextureCoordinates.Count > 0)
                                {
                                    for (int uvIdx = 0; uvIdx < v.TextureCoordinates.Count; uvIdx++)
                                        EditorGUILayout.Vector2Field($"UV[{uvIdx}]", v.TextureCoordinates[uvIdx]);
                                }

                                EditorGUI.indentLevel--;
                            }
                        }

                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (obj.PrimitiveLists != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Primitive Lists", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Primitive Lists: {obj.PrimitiveLists.Count}");

                for (int i = 0; i < obj.PrimitiveLists.Count; i++)
                {
                    var pl = obj.PrimitiveLists[i];
                    if (pl == null) continue;

                    if (!m_PrimitiveListFoldouts.ContainsKey(i)) m_PrimitiveListFoldouts[i] = false;

                    int stripCount = pl.StripIndices != null ? pl.StripIndices.Count : 0;
                    int indexCount = pl.IndexIndices != null ? pl.IndexIndices.Count : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_PrimitiveListFoldouts[i] = EditorGUILayout.Foldout(
                        m_PrimitiveListFoldouts[i],
                        $"Primitive List [{i}] - Type: {pl.Type} | Format: {pl.Format} | Strip Count: {stripCount} | Index Count: {indexCount}",
                        true
                    );

                    if (m_PrimitiveListFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Primitive Type:", pl.Type.ToString());
                        EditorGUILayout.LabelField("Format:", pl.Format.ToString());
                        EditorGUILayout.LabelField("Index Buffer:", pl.IndexBuffer.ToString());
                        EditorGUILayout.LabelField("Reserved:", $"[0]: {pl.Reserved0}, [1]: {pl.Reserved1}");

                        if (pl.StripIndices != null && pl.StripIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField($"Strip Indices ({pl.StripIndices.Count}):", EditorStyles.boldLabel);
                            string stripStr = string.Join(", ", pl.StripIndices.GetRange(0, Mathf.Min(pl.StripIndices.Count, 100)));
                            if (pl.StripIndices.Count > 100) stripStr += "...";
                            EditorGUILayout.HelpBox(stripStr, MessageType.None);
                        }

                        if (pl.IndexIndices != null && pl.IndexIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField($"Index Indices ({pl.IndexIndices.Count}):", EditorStyles.boldLabel);
                            string indexStr = string.Join(", ", pl.IndexIndices.GetRange(0, Mathf.Min(pl.IndexIndices.Count, 100)));
                            if (pl.IndexIndices.Count > 100) indexStr += "...";
                            EditorGUILayout.HelpBox(indexStr, MessageType.None);
                        }

                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion

        #region Materials Tab
        private void DrawMaterialsTab()
        {
            var data = m_LoadedNinjaData.Data;
            if (data.Object == null && data.TextureList == null)
            {
                EditorGUILayout.HelpBox("No Material or Texture data present in this file.", MessageType.Info);
                return;
            }

            var obj = data.Object;

            if (obj != null)
            {
                if (obj.Materials != null)
                {
                    EditorGUILayout.LabelField("Materials Definitions", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Materials: {obj.Materials.Count}");

                    for (int i = 0; i < obj.Materials.Count; i++)
                    {
                        var mat = obj.Materials[i];
                        if (mat == null) continue;

                        if (!m_MaterialFoldouts.ContainsKey(i)) m_MaterialFoldouts[i] = false;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        m_MaterialFoldouts[i] = EditorGUILayout.Foldout(
                            m_MaterialFoldouts[i],
                            $"Material [{i}] - Type: {mat.Type} | Flag: {mat.Flag}",
                            true
                        );

                        if (m_MaterialFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.LabelField("Material Type:", mat.Type.ToString());
                            EditorGUILayout.LabelField("Material Flag:", mat.Flag.ToString());
                            EditorGUILayout.LabelField("User Defined:", mat.UserDefined.ToString());
                            EditorGUILayout.LabelField("Reserved Data:", $"[0]: {mat.Reserved0}, [1]: {mat.Reserved1}, [2]: {mat.Reserved2}");
                            EditorGUILayout.LabelField("Internal Offsets:", $"Colour: 0x{mat.MaterialColourOffset:X8}, Logic: 0x{mat.MaterialLogicOffset:X8}, TexMap: 0x{mat.MaterialTexMapDescriptionOffset:X8}");
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                if (obj.MaterialColours != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Material Colours", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Material Colours: {obj.MaterialColours.Count}");

                    for (int i = 0; i < obj.MaterialColours.Count; i++)
                    {
                        var mc = obj.MaterialColours[i];
                        if (mc == null) continue;

                        if (!m_MaterialColourFoldouts.ContainsKey(i)) m_MaterialColourFoldouts[i] = false;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        m_MaterialColourFoldouts[i] = EditorGUILayout.Foldout(
                            m_MaterialColourFoldouts[i],
                            $"Material Colour [{i}] (Offset: 0x{mc.Offset:X8})",
                            true
                        );

                        if (m_MaterialColourFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.Vector4Field("Diffuse", mc.Diffuse);
                            EditorGUILayout.Vector4Field("Ambient", mc.Ambient);
                            EditorGUILayout.Vector4Field("Specular", mc.Specular);
                            EditorGUILayout.Vector4Field("Emissive", mc.Emissive);
                            EditorGUILayout.FloatField("Power", mc.Power);
                            EditorGUILayout.LabelField("Reserved:", $"[0]: {mc.Reserved0}, [1]: {mc.Reserved1}, [2]: {mc.Reserved2}");
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                if (obj.MaterialLogics != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Material Logics", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Material Logics: {obj.MaterialLogics.Count}");

                    for (int i = 0; i < obj.MaterialLogics.Count; i++)
                    {
                        var ml = obj.MaterialLogics[i];
                        if (ml == null) continue;

                        if (!m_MaterialLogicFoldouts.ContainsKey(i)) m_MaterialLogicFoldouts[i] = false;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        m_MaterialLogicFoldouts[i] = EditorGUILayout.Foldout(
                            m_MaterialLogicFoldouts[i],
                            $"Material Logic [{i}] - Blend: {ml.Blend} | Alpha: {ml.Alpha} | ZComp: {ml.ZComparison}",
                            true
                        );

                        if (m_MaterialLogicFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.LabelField("Blend Enabled:", ml.Blend.ToString());
                            EditorGUILayout.LabelField("SRC Blend:", ml.SRCBlend.ToString());
                            EditorGUILayout.LabelField("DST Blend:", ml.DSTBlend.ToString());
                            EditorGUILayout.LabelField("Blend Factor:", ml.BlendFactor.ToString());
                            EditorGUILayout.LabelField("Blend Op:", ml.BlendOperation.ToString());
                            EditorGUILayout.LabelField("Logic Op:", ml.LogicOperation.ToString());
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Alpha Enabled:", ml.Alpha.ToString());
                            EditorGUILayout.LabelField("Alpha Function:", ml.AlphaFunction.ToString());
                            EditorGUILayout.LabelField("Alpha Ref:", ml.AlphaRef.ToString());
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("ZComparison Enabled:", ml.ZComparison.ToString());
                            EditorGUILayout.LabelField("ZComparison Function:", ml.ZComparisonFunction.ToString());
                            EditorGUILayout.LabelField("ZUpdate:", ml.ZUpdate.ToString());
                            EditorGUILayout.LabelField("Reserved:", $"[0]: {ml.Reserved0}, [1]: {ml.Reserved1}, [2]: {ml.Reserved2}, [3]: {ml.Reserved3}");
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                if (obj.TextureMaps != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Texture Maps", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Texture Maps: {obj.TextureMaps.Count}");

                    for (int i = 0; i < obj.TextureMaps.Count; i++)
                    {
                        var tm = obj.TextureMaps[i];
                        if (tm == null) continue;

                        if (!m_TextureMapFoldouts.ContainsKey(i)) m_TextureMapFoldouts[i] = false;

                        int descCount = tm.NinjaTextureMapDescriptions != null ? tm.NinjaTextureMapDescriptions.Count : 0;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        m_TextureMapFoldouts[i] = EditorGUILayout.Foldout(
                            m_TextureMapFoldouts[i],
                            $"Texture Map [{i}] - Descriptions: {descCount} (Offset: 0x{tm.Offset:X8})",
                            true
                        );

                        if (m_TextureMapFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            if (tm.NinjaTextureMapDescriptions != null)
                            {
                                for (int j = 0; j < tm.NinjaTextureMapDescriptions.Count; j++)
                                {
                                    var desc = tm.NinjaTextureMapDescriptions[j];
                                    if (desc == null) continue;

                                    EditorGUILayout.LabelField($"Description [{j}]:", EditorStyles.miniBoldLabel);
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.LabelField("Type:", desc.Type.ToString());
                                    EditorGUILayout.LabelField("Texture Index:", desc.Index.ToString());
                                    EditorGUILayout.Vector2Field("Offset", desc.Offset);
                                    EditorGUILayout.FloatField("Blend", desc.Blend);
                                    EditorGUILayout.LabelField("Texture Info:", desc.TextureInfo.ToString());
                                    EditorGUILayout.LabelField("Min Filter:", desc.MinFilter.ToString());
                                    EditorGUILayout.LabelField("Mag Filter:", desc.MagFilter.ToString());
                                    EditorGUILayout.FloatField("MipMap Bias", desc.MipMapBias);
                                    EditorGUILayout.LabelField("Max Mip Level:", desc.MaxMipLevel.ToString());
                                    EditorGUILayout.LabelField("Reserved:", $"[0]: {desc.Reserved0}, [1]: {desc.Reserved1}, [2]: {desc.Reserved2}");
                                    EditorGUI.indentLevel--;
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
            }

            if (data.TextureList != null && data.TextureList.NinjaTextureFiles != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Texture List (NXTL)", EditorStyles.boldLabel);
                var texList = data.TextureList.NinjaTextureFiles;
                EditorGUILayout.LabelField($"Total Textures in List: {texList.Count}");

                for (int i = 0; i < texList.Count; i++)
                {
                    var tf = texList[i];
                    if (tf == null) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"[{i}] {tf.FileName ?? ""}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Type: {tf.Type} | Bank: {tf.Bank} | Global Index: {tf.GlobalIndex}");
                    EditorGUILayout.LabelField($"Min Filter: {tf.MinFilter} | Mag Filter: {tf.MagFilter}");
                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion

        #region Motion Tab
        private void DrawMotionTab()
        {
            var data = m_LoadedNinjaData.Data;
            if (data.Motion == null && data.MaterialMotion == null)
            {
                EditorGUILayout.HelpBox("No Node or Material Motion data present in this file.", MessageType.Info);
                return;
            }

            if (data.Motion != null)
            {
                DrawSingleMotionSection("Node Motion Information", data.Motion, m_SubMotionFoldouts, m_SubMotionPages);
            }

            if (data.MaterialMotion != null)
            {
                EditorGUILayout.Space();
                DrawSingleMotionSection("Material Motion Information", data.MaterialMotion, m_MatSubMotionFoldouts, m_MatSubMotionPages);
            }
        }

        private void DrawSingleMotionSection(
            string headerTitle,
            NinjaMotion mot,
            Dictionary<int, bool> foldouts,
            Dictionary<int, int> pages)
        {
            EditorGUILayout.LabelField(headerTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Chunk ID: {mot.ChunkID ?? "N/A"} | Motion Type: {mot.Type}");
            EditorGUILayout.LabelField($"Frame Range: {mot.StartFrame:F2} - {mot.EndFrame:F2} | Framerate: {mot.Framerate:F2} FPS");
            EditorGUILayout.LabelField($"Reserved Data: [0]: {mot.Reserved0}, [1]: {mot.Reserved1}");

            if (mot.SubMotions != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"SubMotions / Animation Tracks ({mot.SubMotions.Count})", EditorStyles.boldLabel);

                var nodes = m_LoadedNinjaData.Data.Object?.Nodes;
                var nodeNames = m_LoadedNinjaData.Data.NodeNameList?.NinjaNodeNames;

                for (int i = 0; i < mot.SubMotions.Count; i++)
                {
                    var sm = mot.SubMotions[i];
                    if (sm == null) continue;

                    if (!foldouts.ContainsKey(i)) foldouts[i] = false;
                    if (!pages.ContainsKey(i)) pages[i] = 0;

                    string targetNodeName = $"Node_{sm.NodeIndex:0000}";
                    if (nodes != null && sm.NodeIndex >= 0 && sm.NodeIndex < nodes.Count && !string.IsNullOrEmpty(nodes[sm.NodeIndex].Name))
                        targetNodeName = nodes[sm.NodeIndex].Name;
                    else if (nodeNames != null && sm.NodeIndex >= 0 && sm.NodeIndex < nodeNames.Count)
                        targetNodeName = nodeNames[sm.NodeIndex];

                    int kfCount = sm.Keyframes != null ? sm.Keyframes.Count : 0;
                    string formattedRawType = NinjaSubMotionTypeFormatter.FormatSubMotionType(sm.Type, mot.Type);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foldouts[i] = EditorGUILayout.Foldout(
                        foldouts[i],
                        $"SubMotion [{i}] - Target: [{sm.NodeIndex}] {targetNodeName} | Raw Type: 0x{(uint)sm.Type:X8} ({formattedRawType}) | Keyframes: {kfCount}",
                        true
                    );

                    if (foldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Raw SubMotion Type:", formattedRawType);
                        EditorGUILayout.LabelField("Raw Flags Hex:", $"0x{(uint)sm.Type:X8}");
                        EditorGUILayout.LabelField("Interpolation Type:", sm.InterpolationType.ToString());
                        EditorGUILayout.LabelField("Target Node Index:", sm.NodeIndex.ToString());
                        EditorGUILayout.LabelField("Frame Range:", $"{sm.StartFrame:F2} to {sm.EndFrame:F2}");
                        EditorGUILayout.LabelField("Keyframe Range:", $"{sm.StartKeyframe:F2} to {sm.EndKeyframe:F2}");

                        if (sm.Keyframes != null && sm.Keyframes.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Raw Keyframe Data:", EditorStyles.boldLabel);

                            int totalKeyframes = sm.Keyframes.Count;
                            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalKeyframes / ITEMS_PER_PAGE));
                            int currentPage = pages[i];

                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("Prev Page", GUILayout.Width(80)) && currentPage > 0) currentPage--;
                            EditorGUILayout.LabelField($"Page {currentPage + 1} / {totalPages} (Keyframes {currentPage * ITEMS_PER_PAGE} - {Mathf.Min(totalKeyframes, (currentPage + 1) * ITEMS_PER_PAGE) - 1})");
                            if (GUILayout.Button("Next Page", GUILayout.Width(80)) && currentPage < totalPages - 1) currentPage++;
                            pages[i] = currentPage;
                            EditorGUILayout.EndHorizontal();

                            int startIdx = currentPage * ITEMS_PER_PAGE;
                            int endIdx = Mathf.Min(totalKeyframes, (currentPage + 1) * ITEMS_PER_PAGE);

                            for (int kIdx = startIdx; kIdx < endIdx; kIdx++)
                            {
                                var kfObj = sm.Keyframes[kIdx];
                                if (kfObj == null) continue;

                                if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_VECTOR vKf)
                                {
                                    EditorGUILayout.LabelField($"Frame {vKf.Frame:F2}: Vector ({vKf.Value.x}, {vKf.Value.y}, {vKf.Value.z})");
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16 rKf)
                                {
                                    EditorGUILayout.LabelField($"Frame {rKf.Frame}: BAMS Short3 ({rKf.Value1}, {rKf.Value2}, {rKf.Value3})");
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_FLOAT fKf)
                                {
                                    EditorGUILayout.LabelField($"Frame {fKf.Frame:F2}: Float ({fKf.Value})");
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16Kf)
                                {
                                    EditorGUILayout.LabelField($"Frame {s16Kf.Frame}: Short ({s16Kf.Value})");
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32Kf)
                                {
                                    EditorGUILayout.LabelField($"Frame {s32Kf.Frame:F2}: Int ({s32Kf.Value})");
                                }
                                else
                                {
                                    EditorGUILayout.LabelField($"Keyframe [{kIdx}]: Unknown Type ({kfObj.GetType().Name})");
                                }
                            }
                        }

                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion

        #region Camera, Light & Effects Tab
        private void DrawMiscTab()
        {
            var data = m_LoadedNinjaData.Data;

            if (data.Camera == null && data.Light == null && data.EffectList == null && data.NodeNameList == null)
            {
                EditorGUILayout.HelpBox("No Camera, Light, Effect or Node Name data present in this file.", MessageType.Info);
                return;
            }

            if (data.Camera != null)
            {
                EditorGUILayout.LabelField("Camera Data (NXCA)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Camera Type:", data.Camera.Type.ToString());
                EditorGUILayout.LabelField("Unknown UInt32 [1]:", data.Camera.UnknownUInt32_1.ToString());
                EditorGUILayout.LabelField("Unknown UInt32 [2]:", data.Camera.UnknownUInt32_2.ToString());
                EditorGUILayout.Vector3Field("Unknown Vector3 [1]", data.Camera.UnknownVector3_1);
                EditorGUILayout.Vector3Field("Unknown Vector3 [2]", data.Camera.UnknownVector3_2);
                EditorGUILayout.FloatField("Unknown Float [1]", data.Camera.UnknownFloat_1);
                EditorGUILayout.FloatField("Unknown Float [2]", data.Camera.UnknownFloat_2);
                EditorGUILayout.FloatField("Unknown Float [3]", data.Camera.UnknownFloat_3);
                EditorGUILayout.FloatField("Unknown Float [4]", data.Camera.UnknownFloat_4);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.Light != null)
            {
                EditorGUILayout.LabelField("Light Data (NXLI)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Light Type:", data.Light.Type.ToString());
                EditorGUILayout.LabelField("Unknown UInt32 [1]:", data.Light.UnknownUInt32_1.ToString());
                EditorGUILayout.Vector3Field("Unknown Vector3 [1]", data.Light.UnknownVector3_1);
                EditorGUILayout.Vector3Field("Unknown Vector3 [2]", data.Light.UnknownVector3_2);
                EditorGUILayout.Vector3Field("Unknown Vector3 [3]", data.Light.UnknownVector3_3);
                EditorGUILayout.FloatField("Unknown Float [1]", data.Light.UnknownFloat_1);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.EffectList != null)
            {
                EditorGUILayout.LabelField("Effect List (NXEF)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Effect List Type:", data.EffectList.Type.ToString());

                if (data.EffectList.NinjaEffectFiles != null && data.EffectList.NinjaEffectFiles.Count > 0)
                {
                    EditorGUILayout.LabelField("Effect Files:", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < data.EffectList.NinjaEffectFiles.Count; i++)
                    {
                        var ef = data.EffectList.NinjaEffectFiles[i];
                        if (ef == null) continue;
                        EditorGUILayout.LabelField($"[{i}] Type: {ef.Type} | File: {ef.FileName ?? ""}");
                    }
                }

                if (data.EffectList.NinjaTechniqueNames != null && data.EffectList.NinjaTechniqueNames.Count > 0)
                {
                    EditorGUILayout.LabelField("Technique Names:", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < data.EffectList.NinjaTechniqueNames.Count; i++)
                    {
                        var tn = data.EffectList.NinjaTechniqueNames[i];
                        if (tn == null) continue;
                        EditorGUILayout.LabelField($"[{i}] Type: {tn.Type} | Name: {tn.Name ?? ""}");
                    }
                }

                if (data.EffectList.NinjaTechniqueIndices != null && data.EffectList.NinjaTechniqueIndices.Count > 0)
                {
                    EditorGUILayout.LabelField("Technique Indices:", string.Join(", ", data.EffectList.NinjaTechniqueIndices));
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.NodeNameList != null && data.NodeNameList.NinjaNodeNames != null)
            {
                EditorGUILayout.LabelField("Node Name List (NXNN)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Sort Type:", data.NodeNameList.Type.ToString());
                EditorGUILayout.LabelField("Total Names:", data.NodeNameList.NinjaNodeNames.Count.ToString());

                for (int i = 0; i < data.NodeNameList.NinjaNodeNames.Count; i++)
                {
                    EditorGUILayout.LabelField($"[{i}]: {data.NodeNameList.NinjaNodeNames[i] ?? ""}");
                }
                EditorGUI.indentLevel--;
            }
        }
        #endregion
    }
}