using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Materials Tab
        private void DrawMaterialsTab()
        {
            if (!m_Context.IsNinjaAsset)
            {
                EditorGUILayout.HelpBox("Select a Ninja asset to view Materials & Textures.", MessageType.Info);
                return;
            }

            var data = m_LoadedNinjaData.Data;
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
                            EditorGUILayout.LabelField("Blend Op:", ml.BlendOperation.ToString());
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Alpha Enabled:", ml.Alpha.ToString());
                            EditorGUILayout.LabelField("Alpha Ref:", ml.AlphaRef.ToString());
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("ZUpdate (ZWrite):", ml.ZUpdate.ToString());
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
                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion
    }
}