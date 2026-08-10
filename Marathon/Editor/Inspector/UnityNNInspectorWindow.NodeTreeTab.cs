using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Node Tree Tab
        private void DrawNodeTreeTab()
        {
            if (!m_Context.IsNinjaAsset || m_Context.NinjaData.Data.Object == null || m_Context.NinjaData.Data.Object.Nodes == null)
            {
                return;
            }

            var nodes = m_Context.NinjaData.Data.Object.Nodes;

            EditorGUILayout.LabelField("Node Tree", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Nodes: {nodes.Count}", EditorStyles.boldLabel);
            if (GUILayout.Button(m_ExpandAllNodes ? "Collapse All" : "Expand All", GUILayout.Width(100)))
            {
                m_ExpandAllNodes = !m_ExpandAllNodes;
                for (int i = 0; i < nodes.Count; i++) m_NodeFoldouts[i] = m_ExpandAllNodes;
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

                if (!m_NodeFoldouts.ContainsKey(i)) m_NodeFoldouts[i] = m_ExpandAllNodes;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                m_NodeFoldouts[i] = EditorGUILayout.Foldout(m_NodeFoldouts[i], $"[{i}] {displayName}", true);

                if (m_NodeFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Name:", n.Name ?? "");
                    
                    DrawCleanFlagsLabel(n.Type, "Node Flags:");

                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Hierarchy:", $"Parent: {n.ParentIndex} | Child: {n.ChildIndex} | Sibling: {n.SiblingIndex}");

                    EditorGUILayout.Vector3Field("Translation", n.Translation);
                    EditorGUILayout.Vector3Field("Rotation", n.Rotation);
                    EditorGUILayout.Vector3Field("Scaling", n.Scaling);

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