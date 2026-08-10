using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Misc Tab
        private void DrawMiscTab()
        {
            if (!m_Context.IsNinjaAsset) return;

            EnsureStyles();
            var data = m_Context.NinjaData.Data;

            if (data.Camera != null) EditorGUILayout.LabelField("Camera Type:", CleanEnumString(data.Camera.Type));
            if (data.Light != null) EditorGUILayout.LabelField("Light Type:", CleanEnumString(data.Light.Type));

            // Structured 2-Column Table List View for NXNN
            if (data.NodeNameList != null && data.NodeNameList.NinjaNodeNames != null)
            {
                EditorGUILayout.Space();
                var nodeNames = data.NodeNameList.NinjaNodeNames;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Node Name List (NXNN) - {nodeNames.Count} Names", EditorStyles.boldLabel);

                // Table Header
                Rect headerRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label(" Index", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                GUILayout.Label("Node Name String", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < nodeNames.Count; i++)
                {
                    GUIStyle rowBg = (i % 2 == 0) ? evenStyle : oddStyle;
                    EditorGUILayout.BeginHorizontal(rowBg, GUILayout.Height(18));

                    GUILayout.Label($"[{i:0000}]", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                    GUILayout.Label(nodeNames[i] ?? "", EditorStyles.label, GUILayout.ExpandWidth(true));

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }
        #endregion
    }
}