// File: Marathon/Editor/NinjaNextImporterEditor.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;

namespace SilentTools
{
    [CustomEditor(typeof(NinjaNextImporter))]
    [CanEditMultipleObjects]
    public class NinjaNextImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty m_ScaleProp;
        private SerializedProperty m_MeshImportModeProp;
        private SerializedProperty m_ImportMaterialsProp;
        private SerializedProperty m_MaterialLocationProp;
        private SerializedProperty m_MaterialSearchProp;
        private SerializedProperty m_MaterialNamingProp;
        private SerializedProperty m_MaterialSearchPathProp;
        private SerializedProperty m_ImportAnimationProp;
        private SerializedProperty m_GenerateAnimatorControllerProp;
        private SerializedProperty m_NodeHierarchyTargetProp;

        public override void OnEnable()
        {
            base.OnEnable();
            m_ScaleProp = serializedObject.FindProperty("m_Scale");
            m_MeshImportModeProp = serializedObject.FindProperty("m_MeshImportMode");
            m_ImportMaterialsProp = serializedObject.FindProperty("m_ImportMaterials");
            m_MaterialLocationProp = serializedObject.FindProperty("m_MaterialLocation");
            m_MaterialSearchProp = serializedObject.FindProperty("m_MaterialSearch");
            m_MaterialNamingProp = serializedObject.FindProperty("m_MaterialNaming");
            m_MaterialSearchPathProp = serializedObject.FindProperty("m_MaterialSearchPath");
            m_ImportAnimationProp = serializedObject.FindProperty("m_ImportAnimation");
            m_GenerateAnimatorControllerProp = serializedObject.FindProperty("m_GenerateAnimatorController");
            m_NodeHierarchyTargetProp = serializedObject.FindProperty("m_NodeHierarchyTarget");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            string assetPath = ((ScriptedImporter)target).assetPath;
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();

            if (ext == ".nbl" || ext == ".gbl" || ext == ".zbl")
            {
                EditorGUILayout.LabelField("NBL Archive Tools", EditorStyles.boldLabel);
                if (GUILayout.Button("Extract NBL Contents to Folder...", GUILayout.Height(28)))
                {
                    NblExporter.ExtractNblToDirectory(assetPath);
                }
                EditorGUILayout.Space();
            }

            EditorGUILayout.LabelField("Mesh Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ScaleProp, new GUIContent("Scale Factor"));
            if (m_MeshImportModeProp != null)
            {
                EditorGUILayout.PropertyField(m_MeshImportModeProp, new GUIContent("Mesh Hierarchy Mode"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ImportAnimationProp, new GUIContent("Import Animation"));

            if (m_ImportAnimationProp.boolValue)
            {
                EditorGUI.indentLevel++;

                int distinctBoneCount, distinctTexCount;
                bool canGenerateController = NinjaAnimatorResolver.CanGenerateAnimatorController(assetPath, out distinctBoneCount, out distinctTexCount);

                EditorGUI.BeginDisabledGroup(!canGenerateController);
                if (m_GenerateAnimatorControllerProp != null)
                {
                    EditorGUILayout.PropertyField(m_GenerateAnimatorControllerProp, new GUIContent("Generate Animator Controller", "Creates a 2-layer Animator Controller for single-animation assets (Layer 0: Transform, Layer 1: Material)."));
                }
                EditorGUI.EndDisabledGroup();

                if (!canGenerateController)
                {
                    EditorGUILayout.HelpBox($"Animator Controller auto-generation is disabled because this object has multiple distinct state animations in obj_param ({distinctBoneCount} bone tracks, {distinctTexCount} material tracks). Use RelObjectAnimationComponent on the instance to access individual clips.", MessageType.None);
                }

                if (m_NodeHierarchyTargetProp != null)
                {
                    EditorGUILayout.PropertyField(m_NodeHierarchyTargetProp, new GUIContent("Node Hierarchy Targets"), true);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Material Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ImportMaterialsProp, new GUIContent("Import Materials"));

            if (m_ImportMaterialsProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_MaterialLocationProp, new GUIContent("Location"));
                EditorGUILayout.PropertyField(m_MaterialNamingProp, new GUIContent("Naming Format"));
                EditorGUILayout.PropertyField(m_MaterialSearchProp, new GUIContent("Search Mode"));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(m_MaterialSearchPathProp, new GUIContent("Load Directory"));
                if (GUILayout.Button("Browse", GUILayout.Width(65)))
                {
                    string folder = EditorUtility.OpenFolderPanel("Select Material Load Directory", "Assets", "");
                    if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath))
                    {
                        m_MaterialSearchPathProp.stringValue = "Assets" + folder.Substring(Application.dataPath.Length);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if ((MaterialLocation)m_MaterialLocationProp.enumValueIndex == MaterialLocation.EmbedInPrefab)
                {
                    if (GUILayout.Button("Extract Materials...", GUILayout.Height(25)))
                    {
                        NinjaMaterialResolver.ExtractMaterials(assetPath, m_MaterialLocationProp, m_MaterialSearchPathProp);
                    }
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}