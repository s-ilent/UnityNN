using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Meshes Tab
        private void DrawMeshesTab()
        {
            if (!m_Context.IsNinjaAsset || m_Context.NinjaData.Data.Object == null)
            {
                return;
            }

            var obj = m_Context.NinjaData.Data.Object;

            if (obj.SubObjects != null)
            {
                EditorGUILayout.LabelField("SubObjects & Mesh Sets", EditorStyles.boldLabel);
                for (int i = 0; i < obj.SubObjects.Count; i++)
                {
                    var sub = obj.SubObjects[i];
                    if (sub == null) continue;

                    if (!m_SubObjectFoldouts.ContainsKey(i)) m_SubObjectFoldouts[i] = false;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_SubObjectFoldouts[i] = EditorGUILayout.Foldout(m_SubObjectFoldouts[i], $"SubObject [{i}] - MeshSets: {sub.MeshSets?.Count ?? 0}", true);

                    if (m_SubObjectFoldouts[i] && sub.MeshSets != null)
                    {
                        EditorGUI.indentLevel++;
                        for (int j = 0; j < sub.MeshSets.Count; j++)
                        {
                            var ms = sub.MeshSets[j];
                            EditorGUILayout.LabelField($"MeshSet [{j}] -> Node: {ms.NodeIndex}, Mat: {ms.MaterialIndex}, VertList: {ms.VertexListIndex}, PrimList: {ms.PrimitiveListIndex}");
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
                        EditorGUILayout.LabelField("Format:", CleanEnumString(vl.Format));
                        EditorGUILayout.LabelField("Flexible Vertex Format:", CleanEnumString(vl.FlexibleVertexFormat));

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
                                if (v != null && v.Position.HasValue)
                                    EditorGUILayout.Vector3Field($"Vertex [{vIdx}] Pos", v.Position.Value);
                            }
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