using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Meshes Tab
        private void DrawMeshesTab()
        {
            if (!m_Context.IsNinjaAsset || m_Context.NinjaData.Data.Object == null) return;

            var obj = m_Context.NinjaData.Data.Object;

            if (m_UseGenericReflectionView)
            {
                if (obj.SubObjects != null && obj.SubObjects.Count > 0)
                    NinjaReflectionDrawer.DrawObjectReflectively(obj.SubObjects, "SubObjects & MeshSets");
                if (obj.VertexLists != null && obj.VertexLists.Count > 0)
                    NinjaReflectionDrawer.DrawObjectReflectively(obj.VertexLists, "Vertex Lists");
                if (obj.PrimitiveLists != null && obj.PrimitiveLists.Count > 0)
                    NinjaReflectionDrawer.DrawObjectReflectively(obj.PrimitiveLists, "Primitive Lists");
                return;
            }

            // Tailored View
            if (obj.SubObjects != null && obj.SubObjects.Count > 0)
            {
                EditorGUILayout.LabelField($"SubObjects & Mesh Sets ({obj.SubObjects.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < obj.SubObjects.Count; i++)
                {
                    var sub = obj.SubObjects[i];
                    if (sub == null) continue;

                    if (!m_SubObjectFoldouts.ContainsKey(i)) m_SubObjectFoldouts[i] = false;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_SubObjectFoldouts[i] = EditorGUILayout.Foldout(m_SubObjectFoldouts[i], $"SubObject [{i}] - Type: {sub.Type} | MeshSets: {sub.MeshSets?.Count ?? 0}", true);

                    if (m_SubObjectFoldouts[i] && sub.MeshSets != null)
                    {
                        EditorGUI.indentLevel++;
                        if (sub.TextureIndices != null && sub.TextureIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField("Texture Indices:", string.Join(", ", sub.TextureIndices));
                        }

                        for (int j = 0; j < sub.MeshSets.Count; j++)
                        {
                            var ms = sub.MeshSets[j];
                            EditorGUILayout.LabelField($"MeshSet [{j}] -> Center: ({ms.Center.x:F2}, {ms.Center.y:F2}, {ms.Center.z:F2}) | Radius: {ms.Radius:F2}");
                            EditorGUILayout.LabelField($"         Node: {ms.NodeIndex} | Mat: {ms.MaterialIndex} | VertList: {ms.VertexListIndex} | PrimList: {ms.PrimitiveListIndex} | Matrix: {ms.MatrixIndex} | Shader: {ms.ShaderIndex}");
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (obj.VertexLists != null && obj.VertexLists.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Vertex Lists ({obj.VertexLists.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < obj.VertexLists.Count; i++)
                {
                    var vl = obj.VertexLists[i];
                    if (vl == null) continue;

                    if (!m_VertexListFoldouts.ContainsKey(i)) m_VertexListFoldouts[i] = false;
                    if (!m_VertexListPages.ContainsKey(i)) m_VertexListPages[i] = 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_VertexListFoldouts[i] = EditorGUILayout.Foldout(m_VertexListFoldouts[i], $"Vertex List [{i}] - Count: {vl.Vertices?.Count ?? 0}", true);

                    if (m_VertexListFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Vertex Type:", CleanEnumString(vl.Type));
                        EditorGUILayout.LabelField("Xbox Format:", CleanEnumString(vl.Format));
                        EditorGUILayout.LabelField("Flexible Vertex Format:", CleanEnumString(vl.FlexibleVertexFormat));
                        if (vl.BoneMatrixIndices != null && vl.BoneMatrixIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField("Bone Matrix Indices:", string.Join(", ", vl.BoneMatrixIndices));
                        }

                        if (vl.Vertices != null && vl.Vertices.Count > 0)
                        {
                            int currentPage = m_VertexListPages[i];
                            DrawPaginationControls(ref currentPage, vl.Vertices.Count, ITEMS_PER_PAGE);
                            m_VertexListPages[i] = currentPage;

                            int startIdx = currentPage * ITEMS_PER_PAGE;
                            int endIdx = Mathf.Min(vl.Vertices.Count, (currentPage + 1) * ITEMS_PER_PAGE);

                            for (int vIdx = startIdx; vIdx < endIdx; vIdx++)
                            {
                                var v = vl.Vertices[vIdx];
                                if (v == null) continue;

                                string posStr = v.Position.HasValue ? $"Pos: ({v.Position.Value.x:F3}, {v.Position.Value.y:F3}, {v.Position.Value.z:F3})" : "";
                                string normStr = v.Normals.HasValue ? $"Norm: ({v.Normals.Value.x:F2}, {v.Normals.Value.y:F2}, {v.Normals.Value.z:F2})" : "";
                                string uvStr = (v.TextureCoordinates != null && v.TextureCoordinates.Count > 0) ? $"UV0: ({v.TextureCoordinates[0].x:F3}, {v.TextureCoordinates[0].y:F3})" : "";

                                EditorGUILayout.LabelField($"Vertex [{vIdx}] {posStr} | {normStr} | {uvStr}");
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (obj.PrimitiveLists != null && obj.PrimitiveLists.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Primitive Lists ({obj.PrimitiveLists.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < obj.PrimitiveLists.Count; i++)
                {
                    var pl = obj.PrimitiveLists[i];
                    if (pl == null) continue;

                    if (!m_PrimitiveListFoldouts.ContainsKey(i)) m_PrimitiveListFoldouts[i] = false;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_PrimitiveListFoldouts[i] = EditorGUILayout.Foldout(m_PrimitiveListFoldouts[i], $"Primitive List [{i}] - Type: {CleanEnumString(pl.Type)} | IndexIndices: {pl.IndexIndices?.Count ?? 0} | StripIndices: {pl.StripIndices?.Count ?? 0}", true);

                    if (m_PrimitiveListFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Format:", $"{pl.Format}");
                        EditorGUILayout.LabelField("Index Buffer:", $"0x{pl.IndexBuffer:X8}");
                        if (pl.StripIndices != null && pl.StripIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField("Strip Lengths:", string.Join(", ", pl.StripIndices));
                        }
                        if (pl.IndexIndices != null && pl.IndexIndices.Count > 0)
                        {
                            int sampleCount = Mathf.Min(pl.IndexIndices.Count, 30);
                            List<string> sampleList = new List<string>();
                            for (int s = 0; s < sampleCount; s++) sampleList.Add(pl.IndexIndices[s].ToString());
                            EditorGUILayout.LabelField($"Index Array ({pl.IndexIndices.Count} total):", string.Join(", ", sampleList) + (pl.IndexIndices.Count > 30 ? "..." : ""));
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion
    }
}