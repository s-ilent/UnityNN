using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

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

            if (m_UseGenericReflectionView)
            {
                if (obj != null)
                {
                    if (obj.Materials != null && obj.Materials.Count > 0)
                        NinjaReflectionDrawer.DrawObjectReflectively(obj.Materials, "Materials");
                    if (obj.MaterialColours != null && obj.MaterialColours.Count > 0)
                        NinjaReflectionDrawer.DrawObjectReflectively(obj.MaterialColours, "Material Colours");
                    if (obj.MaterialLogics != null && obj.MaterialLogics.Count > 0)
                        NinjaReflectionDrawer.DrawObjectReflectively(obj.MaterialLogics, "Material Logics");
                    if (obj.TextureMaps != null && obj.TextureMaps.Count > 0)
                        NinjaReflectionDrawer.DrawObjectReflectively(obj.TextureMaps, "Texture Maps");
                }
                if (data.TextureList != null)
                {
                    NinjaReflectionDrawer.DrawObjectReflectively(data.TextureList, "Texture List (NXTL)");
                }
                return;
            }

            // Tailored View
            if (obj != null)
            {
                if (obj.Materials != null && obj.Materials.Count > 0)
                {
                    EditorGUILayout.LabelField($"Material Definitions ({obj.Materials.Count})", EditorStyles.boldLabel);
                    for (int i = 0; i < obj.Materials.Count; i++)
                    {
                        var mat = obj.Materials[i];
                        if (mat == null) continue;

                        if (!m_MaterialFoldouts.ContainsKey(i)) m_MaterialFoldouts[i] = false;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        m_MaterialFoldouts[i] = EditorGUILayout.Foldout(m_MaterialFoldouts[i], $"Material [{i}] - Type: {CleanEnumString(mat.Type)}", true);
                        if (m_MaterialFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            DrawCleanFlagsLabel(mat.Type, "Material Type:");
                            DrawCleanFlagsLabel(mat.Flag, "Material Flags:");
                            EditorGUILayout.LabelField("User Defined:", $"{mat.UserDefined}");

                            var col = FindMaterialColourByOffset(obj, mat.MaterialColourOffset);
                            if (col != null)
                            {
                                EditorGUILayout.Space(2);
                                EditorGUILayout.LabelField("Colour Definition:", EditorStyles.boldLabel);
                                EditorGUILayout.ColorField("Diffuse", new Color(col.Diffuse.x, col.Diffuse.y, col.Diffuse.z, col.Diffuse.w));
                                EditorGUILayout.ColorField("Ambient", new Color(col.Ambient.x, col.Ambient.y, col.Ambient.z, col.Ambient.w));
                                EditorGUILayout.ColorField("Specular", new Color(col.Specular.x, col.Specular.y, col.Specular.z, col.Specular.w));
                                EditorGUILayout.ColorField("Emissive", new Color(col.Emissive.x, col.Emissive.y, col.Emissive.z, col.Emissive.w));
                                EditorGUILayout.LabelField("Power:", $"{col.Power:F2}");
                            }

                            var logic = FindMaterialLogicByOffset(obj, mat.MaterialLogicOffset);
                            if (logic != null)
                            {
                                EditorGUILayout.Space(2);
                                EditorGUILayout.LabelField("Logic Definition:", EditorStyles.boldLabel);
                                EditorGUILayout.LabelField($"Blend: {logic.Blend} | SRC: {CleanEnumString(logic.SRCBlend)} | DST: {CleanEnumString(logic.DSTBlend)}");
                                EditorGUILayout.LabelField($"BlendOp: {CleanEnumString(logic.BlendOperation)} | LogicOp: {CleanEnumString(logic.LogicOperation)}");
                                EditorGUILayout.LabelField($"Alpha Test: {logic.Alpha} | Function: {CleanEnumString(logic.AlphaFunction)} | Ref: {logic.AlphaRef}");
                                EditorGUILayout.LabelField($"ZCompare: {logic.ZComparison} | Function: {CleanEnumString(logic.ZComparisonFunction)} | ZUpdate: {logic.ZUpdate}");
                            }

                            var texMap = FindTextureMapByOffset(obj, mat.MaterialTexMapDescriptionOffset);
                            if (texMap != null && texMap.NinjaTextureMapDescriptions != null)
                            {
                                EditorGUILayout.Space(2);
                                EditorGUILayout.LabelField($"Texture Map ({texMap.NinjaTextureMapDescriptions.Count} Layers):", EditorStyles.boldLabel);
                                for (int t = 0; t < texMap.NinjaTextureMapDescriptions.Count; t++)
                                {
                                    var desc = texMap.NinjaTextureMapDescriptions[t];
                                    string texName = (data.TextureList != null && data.TextureList.NinjaTextureFiles != null && desc.Index >= 0 && desc.Index < data.TextureList.NinjaTextureFiles.Count)
                                        ? data.TextureList.NinjaTextureFiles[desc.Index].FileName : $"Index_{desc.Index}";

                                    EditorGUILayout.LabelField($"  Layer [{t}] Tex: {texName} | Offset: ({desc.Offset.x:F2}, {desc.Offset.y:F2}) | Blend: {desc.Blend:F2}");
                                    EditorGUILayout.LabelField($"          Filters: {CleanEnumString(desc.MinFilter)} / {CleanEnumString(desc.MagFilter)} | MipBias: {desc.MipMapBias:F2}");
                                }
                            }

                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                if (obj.MaterialColours != null && obj.MaterialColours.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Global Material Colours Array ({obj.MaterialColours.Count})", EditorStyles.boldLabel);
                    for (int c = 0; c < obj.MaterialColours.Count; c++)
                    {
                        var col = obj.MaterialColours[c];
                        if (col == null) continue;
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"Colour Entry [{c}] - Offset: 0x{col.Offset:X8}", EditorStyles.miniBoldLabel);
                        EditorGUILayout.ColorField("Diffuse", new Color(col.Diffuse.x, col.Diffuse.y, col.Diffuse.z, col.Diffuse.w));
                        EditorGUILayout.ColorField("Ambient", new Color(col.Ambient.x, col.Ambient.y, col.Ambient.z, col.Ambient.w));
                        EditorGUILayout.ColorField("Specular", new Color(col.Specular.x, col.Specular.y, col.Specular.z, col.Specular.w));
                        EditorGUILayout.ColorField("Emissive", new Color(col.Emissive.x, col.Emissive.y, col.Emissive.z, col.Emissive.w));
                        EditorGUILayout.LabelField($"Power: {col.Power:F2}");
                        EditorGUILayout.EndVertical();
                    }
                }
            }

            if (data.TextureList != null && data.TextureList.NinjaTextureFiles != null)
            {
                EditorGUILayout.Space();
                var texList = data.TextureList.NinjaTextureFiles;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Texture List (NXTL) - {texList.Count} Textures", EditorStyles.boldLabel);

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

        private NinjaMaterialColours FindMaterialColourByOffset(NinjaObject obj, uint offset)
        {
            if (offset == 0 || obj == null || obj.MaterialColours == null) return null;
            return obj.MaterialColours.Find(c => c.Offset == offset);
        }

        private NinjaMaterialLogic FindMaterialLogicByOffset(NinjaObject obj, uint offset)
        {
            if (offset == 0 || obj == null || obj.MaterialLogics == null) return null;
            return obj.MaterialLogics.Find(l => l.Offset == offset);
        }

        private NinjaTextureMap FindTextureMapByOffset(NinjaObject obj, uint offset)
        {
            if (offset == 0 || obj == null || obj.TextureMaps == null) return null;
            return obj.TextureMaps.Find(t => t.Offset == offset);
        }
        #endregion
    }
}