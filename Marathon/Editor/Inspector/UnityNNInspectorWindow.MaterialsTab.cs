using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Materials Tab
        private void DrawMaterialsTab()
        {
            if (!m_Context.IsNinjaAsset) return;

            EnsureStyles();
            var data = m_Context.NinjaData.Data;
            var obj = data.Object;

            if (obj != null && obj.Materials != null)
            {
                EditorGUILayout.LabelField("Materials Definitions", EditorStyles.boldLabel);
                for (int i = 0; i < obj.Materials.Count; i++)
                {
                    var mat = obj.Materials[i];
                    if (mat == null) continue;

                    if (!m_MaterialFoldouts.ContainsKey(i)) m_MaterialFoldouts[i] = false;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_MaterialFoldouts[i] = EditorGUILayout.Foldout(m_MaterialFoldouts[i], $"Material [{i}]", true);
                    if (m_MaterialFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        DrawCleanFlagsLabel(mat.Type, "Material Type:");
                        DrawCleanFlagsLabel(mat.Flag, "Material Flags:");
                        EditorGUILayout.LabelField("Colour Offset:", $"0x{mat.MaterialColourOffset:X8}");
                        EditorGUILayout.LabelField("Logic Offset:", $"0x{mat.MaterialLogicOffset:X8}");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            // Structured 5-Column Table List View for NXTL
            if (data.TextureList != null && data.TextureList.NinjaTextureFiles != null)
            {
                EditorGUILayout.Space();
                var texList = data.TextureList.NinjaTextureFiles;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Texture List (NXTL) - {texList.Count} Textures", EditorStyles.boldLabel);

                // Table Header
                Rect headerRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label(" Index", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label("Texture Filename", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label("Global Index", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                GUILayout.Label("Bank", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label("Min / Mag Filter", EditorStyles.miniBoldLabel, GUILayout.Width(140));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < texList.Count; i++)
                {
                    var tf = texList[i];
                    if (tf == null) continue;

                    GUIStyle rowBg = (i % 2 == 0) ? evenStyle : oddStyle;
                    EditorGUILayout.BeginHorizontal(rowBg, GUILayout.Height(20));

                    GUILayout.Label($"[{i:00}]", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                    GUILayout.Label(tf.FileName ?? "<null>", EditorStyles.label, GUILayout.ExpandWidth(true));
                    GUILayout.Label($"{tf.GlobalIndex}", EditorStyles.miniLabel, GUILayout.Width(80));
                    GUILayout.Label($"{tf.Bank}", EditorStyles.miniLabel, GUILayout.Width(50));
                    GUILayout.Label($"{CleanEnumString(tf.MinFilter)} / {CleanEnumString(tf.MagFilter)}", EditorStyles.miniLabel, GUILayout.Width(140));

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }
        #endregion
    }
}