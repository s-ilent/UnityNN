// File: Marathon/Editor/NinjaNextImporterEditor.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System;
using System.IO;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;
using Marathon.Formats.Archive;

namespace SilentTools
{
    [CustomEditor(typeof(NinjaNextImporter))]
    [CanEditMultipleObjects]
    public class NinjaNextImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty m_ScaleProp;
        private SerializedProperty m_MeshImportModeProp;
        private SerializedProperty m_GenerateMeshCollidersProp;
        private SerializedProperty m_ImportMaterialsProp;
        private SerializedProperty m_MaterialLocationProp;
        private SerializedProperty m_MaterialSearchProp;
        private SerializedProperty m_MaterialNamingProp;
        private SerializedProperty m_MaterialSearchPathProp;
        private SerializedProperty m_TextureSearchPathsProp;
        private SerializedProperty m_MaterialRemapsProp;
        private SerializedProperty m_TextureRemapsProp;
        private SerializedProperty m_ImportAnimationProp;
        private SerializedProperty m_GenerateAnimatorControllerProp;
        private SerializedProperty m_NodeHierarchyTargetProp;

        private NinjaNext m_PreviewData;
        private string m_LastLoadedPath = "";
        private int m_SelectedTab = 0;
        private bool m_CanGenerateController = true;
        private int m_DistinctBoneCount = 0;
        private int m_DistinctTexCount = 0;

        public override void OnEnable()
        {
            base.OnEnable();
            m_ScaleProp = serializedObject.FindProperty("m_Scale");
            m_MeshImportModeProp = serializedObject.FindProperty("m_MeshImportMode");
            m_GenerateMeshCollidersProp = serializedObject.FindProperty("m_GenerateMeshColliders");
            m_ImportMaterialsProp = serializedObject.FindProperty("m_ImportMaterials");
            m_MaterialLocationProp = serializedObject.FindProperty("m_MaterialLocation");
            m_MaterialSearchProp = serializedObject.FindProperty("m_MaterialSearch");
            m_MaterialNamingProp = serializedObject.FindProperty("m_MaterialNaming");
            m_MaterialSearchPathProp = serializedObject.FindProperty("m_MaterialSearchPath");
            m_TextureSearchPathsProp = serializedObject.FindProperty("m_TextureSearchPaths");
            m_MaterialRemapsProp = serializedObject.FindProperty("m_MaterialRemaps");
            m_TextureRemapsProp = serializedObject.FindProperty("m_TextureRemaps");
            m_ImportAnimationProp = serializedObject.FindProperty("m_ImportAnimation");
            m_GenerateAnimatorControllerProp = serializedObject.FindProperty("m_GenerateAnimatorController");
            m_NodeHierarchyTargetProp = serializedObject.FindProperty("m_NodeHierarchyTarget");

            m_LastLoadedPath = "";
            LoadPreviewData();
        }

        private void LoadPreviewData()
        {
            string assetPath = ((ScriptedImporter)target).assetPath;
            if (string.IsNullOrEmpty(assetPath) || assetPath == m_LastLoadedPath) return;

            m_LastLoadedPath = assetPath;
            m_PreviewData = new NinjaNext();

            try
            {
                string ext = Path.GetExtension(assetPath).ToLowerInvariant();
                if (ext is ".nbl" or ".gbl" or ".zbl")
                {
                    using (FileStream fs = File.OpenRead(assetPath))
                    {
                        NblArchive nbl = NblArchive.Load(fs);
                        m_PreviewData.Data = nbl.ToFormatData();
                    }
                }
                else if (ext is not (".rel" or ".xnr" or ".gnr" or ".znr"))
                {
                    m_PreviewData.Load(assetPath);
                }

                // Cache animator controller generation capability on asset load
                m_CanGenerateController = NinjaAnimatorResolver.CanGenerateAnimatorController(
                    assetPath,
                    out m_DistinctBoneCount,
                    out m_DistinctTexCount
                );
            }
            catch
            {
                m_PreviewData = null;
                m_CanGenerateController = true;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            string assetPath = ((ScriptedImporter)target).assetPath;
            if (assetPath != m_LastLoadedPath)
            {
                LoadPreviewData();
            }

            string ext = Path.GetExtension(assetPath).ToLowerInvariant();

            bool isModelAsset = ext is ".xno" or ".xna" or ".xnj" or ".gno" or ".zno" || (m_PreviewData?.Data?.Object != null);
            bool isMotionAsset = ext is ".xnm" or ".xnv" or ".gnm" or ".znm" || (m_PreviewData?.Data?.Motion != null || m_PreviewData?.Data?.MaterialMotion != null);
            bool isTexturePackage = ext is ".xnt" or ".gnt" or ".znt" || (m_PreviewData?.Data?.TextureList != null && !isModelAsset);
            bool isRelAsset = ext is ".rel" or ".xnr" or ".gnr" or ".znr";
            bool isArchive = ext is ".nbl" or ".gbl" or ".zbl";

            // Archive Extraction Utility Banner
            if (isArchive)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("NBL Archive Utilities", EditorStyles.boldLabel);
                if (GUILayout.Button("Extract NBL Contents to Folder...", GUILayout.Height(26)))
                {
                    NblExporter.ExtractNblToDirectory(assetPath);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // Top File Contents Overview Card
            DrawFileContentsOverview(ext, isModelAsset, isMotionAsset, isTexturePackage, isRelAsset);
            EditorGUILayout.Space(4);

            // Context-Aware Tab Toolbar
            List<string> tabs = new List<string>();
            if (isModelAsset) { tabs.Add("Model"); tabs.Add("Materials"); }
            if (isModelAsset || isMotionAsset) { tabs.Add("Animation"); }
            if (isTexturePackage || (isModelAsset && m_PreviewData?.Data?.TextureList != null)) { tabs.Add("Textures"); }
            if (isRelAsset) { tabs.Add("Stage / Layout"); }

            if (tabs.Count > 1)
            {
                m_SelectedTab = GUILayout.Toolbar(Mathf.Clamp(m_SelectedTab, 0, tabs.Count - 1), tabs.ToArray(), EditorStyles.toolbarButton);
                EditorGUILayout.Space(4);
            }

            string currentTab = tabs.Count > 0 ? tabs[Mathf.Clamp(m_SelectedTab, 0, tabs.Count - 1)] : "General";

            switch (currentTab)
            {
                case "Model":
                    DrawModelTab();
                    break;
                case "Materials":
                    DrawMaterialsTab(assetPath);
                    break;
                case "Animation":
                    DrawAnimationTab();
                    break;
                case "Textures":
                    DrawTexturesTab();
                    break;
                case "Stage / Layout":
                default:
                    DrawGeneralScaleSettings();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        #region Tab Drawers
        private void DrawModelTab()
        {
            EditorGUILayout.LabelField("Mesh & Hierarchy Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ScaleProp, new GUIContent("Scale Factor"));
            if (m_MeshImportModeProp != null)
            {
                EditorGUILayout.PropertyField(m_MeshImportModeProp, new GUIContent("Mesh Import Mode"));
            }
            if (m_GenerateMeshCollidersProp != null)
            {
                EditorGUILayout.PropertyField(m_GenerateMeshCollidersProp, new GUIContent("Generate Mesh Colliders"));
            }
        }

        private void DrawMaterialsTab(string assetPath)
        {
            EditorGUILayout.LabelField("Material Resolution Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ImportMaterialsProp, new GUIContent("Import Materials"));

            if (m_ImportMaterialsProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_MaterialLocationProp, new GUIContent("Location"));
                EditorGUILayout.PropertyField(m_MaterialNamingProp, new GUIContent("Naming Format"));
                EditorGUILayout.PropertyField(m_MaterialSearchProp, new GUIContent("Search Mode"));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(m_MaterialSearchPathProp, new GUIContent("Material Search Path"));
                if (GUILayout.Button("Browse", GUILayout.Width(65)))
                {
                    string folder = EditorUtility.OpenFolderPanel("Select Material Search Directory", "Assets", "");
                    if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath))
                    {
                        m_MaterialSearchPathProp.stringValue = "Assets" + folder.Substring(Application.dataPath.Length);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                DrawTextureSearchPathsList();

                EditorGUILayout.Space(4);

                if ((MaterialLocation)m_MaterialLocationProp.enumValueIndex == MaterialLocation.EmbedInPrefab)
                {
                    if (GUILayout.Button("Extract Materials...", GUILayout.Height(24)))
                    {
                        NinjaMaterialResolver.ExtractMaterials(assetPath, m_MaterialLocationProp, m_MaterialSearchPathProp);
                    }
                    EditorGUILayout.Space(2);
                }

                // FBX-Style Per-Material Remap Table (Non-mutating)
                DrawMaterialRemapTable();

                EditorGUI.indentLevel--;
            }
        }

        private void DrawTextureSearchPathsList()
        {
            EditorGUILayout.LabelField("Texture Search Paths (Ordered by Priority)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Folders are searched in order from top to bottom. Textures found in these paths override textures in the local asset folder.", MessageType.None);

            if (m_TextureSearchPathsProp == null) return;

            for (int i = 0; i < m_TextureSearchPathsProp.arraySize; i++)
            {
                var element = m_TextureSearchPathsProp.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(24));
                EditorGUILayout.PropertyField(element, GUIContent.none);

                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string folder = EditorUtility.OpenFolderPanel("Select Texture Search Directory", "Assets", "");
                    if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath))
                    {
                        element.stringValue = "Assets" + folder.Substring(Application.dataPath.Length);
                    }
                }

                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    m_TextureSearchPathsProp.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Texture Search Path", GUILayout.Width(190)))
            {
                int newIdx = m_TextureSearchPathsProp.arraySize;
                m_TextureSearchPathsProp.InsertArrayElementAtIndex(newIdx);
                m_TextureSearchPathsProp.GetArrayElementAtIndex(newIdx).stringValue = "Assets";
            }
            EditorGUILayout.EndHorizontal();
        }

        private int FindMaterialRemapIndex(int slotIndex)
        {
            if (m_MaterialRemapsProp == null) return -1;
            for (int j = 0; j < m_MaterialRemapsProp.arraySize; j++)
            {
                var elem = m_MaterialRemapsProp.GetArrayElementAtIndex(j);
                if (elem.FindPropertyRelative("slotIndex").intValue == slotIndex)
                {
                    return j;
                }
            }
            return -1;
        }

        private void DrawMaterialRemapTable()
        {
            if (m_PreviewData?.Data?.Object?.Materials == null || m_PreviewData.Data.Object.Materials.Count == 0) return;

            var mats = m_PreviewData.Data.Object.Materials;
            EditorGUILayout.LabelField($"Material Remap Overrides ({mats.Count} slots)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag and drop external Material assets to override individual imported materials.", MessageType.None);

            for (int i = 0; i < mats.Count; i++)
            {
                int remapIdx = FindMaterialRemapIndex(i);
                Material currentOverride = null;
                if (remapIdx >= 0)
                {
                    currentOverride = (Material)m_MaterialRemapsProp.GetArrayElementAtIndex(remapIdx).FindPropertyRelative("overrideMaterial").objectReferenceValue;
                }

                string typeStr = CleanEnumString(mats[i].Type);
                string label = $"Slot [{i:00}] ({typeStr})";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label, GUILayout.Width(140));

                EditorGUI.BeginChangeCheck();
                Material newOverride = (Material)EditorGUILayout.ObjectField(currentOverride, typeof(Material), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newOverride != null)
                    {
                        if (remapIdx >= 0)
                        {
                            var elem = m_MaterialRemapsProp.GetArrayElementAtIndex(remapIdx);
                            elem.FindPropertyRelative("overrideMaterial").objectReferenceValue = newOverride;
                        }
                        else
                        {
                            int newIndex = m_MaterialRemapsProp.arraySize;
                            m_MaterialRemapsProp.InsertArrayElementAtIndex(newIndex);
                            var elem = m_MaterialRemapsProp.GetArrayElementAtIndex(newIndex);
                            elem.FindPropertyRelative("slotIndex").intValue = i;
                            elem.FindPropertyRelative("originalName").stringValue = $"Material_{i}";
                            elem.FindPropertyRelative("overrideMaterial").objectReferenceValue = newOverride;
                        }
                    }
                    else
                    {
                        // User cleared override -> remove entry
                        if (remapIdx >= 0)
                        {
                            m_MaterialRemapsProp.DeleteArrayElementAtIndex(remapIdx);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private int FindTextureRemapIndex(int textureIndex)
        {
            if (m_TextureRemapsProp == null) return -1;
            for (int j = 0; j < m_TextureRemapsProp.arraySize; j++)
            {
                var elem = m_TextureRemapsProp.GetArrayElementAtIndex(j);
                if (elem.FindPropertyRelative("textureIndex").intValue == textureIndex)
                {
                    return j;
                }
            }
            return -1;
        }

        private void DrawTexturesTab()
        {
            DrawTextureSearchPathsList();
            EditorGUILayout.Space(6);

            var texList = m_PreviewData?.Data?.TextureList?.NinjaTextureFiles;
            if (texList == null || texList.Count == 0)
            {
                EditorGUILayout.HelpBox("No texture package entries found in this asset.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Texture Package Overrides ({texList.Count} Textures)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Override individual textures defined in the XNT package with custom Unity Texture assets.", MessageType.None);

            for (int i = 0; i < texList.Count; i++)
            {
                int remapIdx = FindTextureRemapIndex(i);
                Texture2D currentOverride = null;
                if (remapIdx >= 0)
                {
                    currentOverride = (Texture2D)m_TextureRemapsProp.GetArrayElementAtIndex(remapIdx).FindPropertyRelative("overrideTexture").objectReferenceValue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i:00}] {texList[i].FileName}", GUILayout.Width(180));

                EditorGUI.BeginChangeCheck();
                Texture2D newOverride = (Texture2D)EditorGUILayout.ObjectField(currentOverride, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newOverride != null)
                    {
                        if (remapIdx >= 0)
                        {
                            var elem = m_TextureRemapsProp.GetArrayElementAtIndex(remapIdx);
                            elem.FindPropertyRelative("overrideTexture").objectReferenceValue = newOverride;
                        }
                        else
                        {
                            int newIndex = m_TextureRemapsProp.arraySize;
                            m_TextureRemapsProp.InsertArrayElementAtIndex(newIndex);
                            var elem = m_TextureRemapsProp.GetArrayElementAtIndex(newIndex);
                            elem.FindPropertyRelative("textureIndex").intValue = i;
                            elem.FindPropertyRelative("originalFileName").stringValue = texList[i].FileName;
                            elem.FindPropertyRelative("overrideTexture").objectReferenceValue = newOverride;
                        }
                    }
                    else
                    {
                        // User cleared override -> remove entry
                        if (remapIdx >= 0)
                        {
                            m_TextureRemapsProp.DeleteArrayElementAtIndex(remapIdx);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawAnimationTab()
        {
            EditorGUILayout.LabelField("Animation & Motion Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ImportAnimationProp, new GUIContent("Import Animation"));

            if (m_ImportAnimationProp.boolValue)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginDisabledGroup(!m_CanGenerateController);
                if (m_GenerateAnimatorControllerProp != null)
                {
                    EditorGUILayout.PropertyField(m_GenerateAnimatorControllerProp, new GUIContent("Generate Animator Controller"));
                }
                EditorGUI.EndDisabledGroup();

                if (!m_CanGenerateController)
                {
                    EditorGUILayout.HelpBox($"Animator Controller auto-generation is disabled because this asset contains multiple distinct state tracks ({m_DistinctBoneCount} bones, {m_DistinctTexCount} materials). Access individual clips via RelObjectAnimationComponent.", MessageType.None);
                }

                if (m_NodeHierarchyTargetProp != null)
                {
                    EditorGUILayout.PropertyField(m_NodeHierarchyTargetProp, new GUIContent("Custom Hierarchy Targets"), true);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawGeneralScaleSettings()
        {
            EditorGUILayout.LabelField("Transform & Scale Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ScaleProp, new GUIContent("World Scale Factor"));
        }
        #endregion

        #region Overview Card
        private void DrawFileContentsOverview(string ext, bool isModel, bool isMotion, bool isTex, bool isRel)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("File Contents & Format Summary", EditorStyles.boldLabel);

            if (isModel && m_PreviewData?.Data?.Object != null)
            {
                var obj = m_PreviewData.Data.Object;
                EditorGUILayout.LabelField($"• 3D Nodes: {obj.Nodes.Count} | SubObjects: {obj.SubObjects.Count} | Materials: {obj.Materials.Count} | Bounding Radius: {obj.Radius:F2}m");
            }
            if (m_PreviewData?.Data?.Motion != null)
            {
                var mot = m_PreviewData.Data.Motion;
                EditorGUILayout.LabelField($"• Node Motion: {mot.Framerate:F0} FPS, {mot.SubMotions.Count} tracks ({mot.StartFrame:F0} - {mot.EndFrame:F0} frames)");
            }
            if (m_PreviewData?.Data?.MaterialMotion != null)
            {
                var matMot = m_PreviewData.Data.MaterialMotion;
                EditorGUILayout.LabelField($"• Material Motion: {matMot.Framerate:F0} FPS, {matMot.SubMotions.Count} tracks");
            }
            if (m_PreviewData?.Data?.TextureList != null)
            {
                EditorGUILayout.LabelField($"• Texture Package (XNT): {m_PreviewData.Data.TextureList.NinjaTextureFiles.Count} texture definitions");
            }
            if (isRel)
            {
                EditorGUILayout.LabelField($"• Stage Module ({ext.ToUpperInvariant()}): Contains stage layout, collisions, or lighting presets.");
            }

            EditorGUILayout.EndVertical();
        }
        #endregion

        private static string CleanEnumString(object enumValue)
        {
            if (enumValue == null) return "Standard";
            return enumValue.ToString()
                .Replace("NND_MATTYPE_", "")
                .Replace("NND_NODETYPE_", "");
        }
    }
}