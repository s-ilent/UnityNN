using UnityEngine;
using UnityEditor;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Node Tree Tab
        private void DrawNodeTreeTab()
        {
            if (!m_Context.IsNinjaAsset)
            {
                EditorGUILayout.HelpBox("Select a Ninja asset to view the Node Tree.", MessageType.Info);
                return;
            }

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
                    DrawMatrix4x4ReadOnly(n.InvInitMatrix);

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
        }
        #endregion
    }
}