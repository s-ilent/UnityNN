using UnityEngine;
using UnityEditor;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Meshes Tab
        private void DrawMeshesTab()
        {
            if (!m_Context.IsNinjaAsset)
            {
                EditorGUILayout.HelpBox("Select a Ninja asset to view Meshes & Geometry.", MessageType.Info);
                return;
            }

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

                            int currentPage = m_VertexListPages[i];
                            DrawPaginationControls(ref currentPage, vl.Vertices.Count, ITEMS_PER_PAGE);
                            m_VertexListPages[i] = currentPage;

                            int startIdx = currentPage * ITEMS_PER_PAGE;
                            int endIdx = Mathf.Min(vl.Vertices.Count, (currentPage + 1) * ITEMS_PER_PAGE);

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
    }
}