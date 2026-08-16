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
            m_NodeHierarchyTargetProp = serializedObject.FindProperty("m_NodeHierarchyTarget");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            string assetPath = ((ScriptedImporter)target).assetPath;
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();

            EditorGUILayout.LabelField("Mesh Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ScaleProp, new GUIContent("Scale Factor"));
            if (m_MeshImportModeProp != null)
            {
                EditorGUILayout.PropertyField(m_MeshImportModeProp, new GUIContent("Mesh Hierarchy Mode"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_ImportAnimationProp, new GUIContent("Import Animation"));
            if (m_NodeHierarchyTargetProp != null)
            {
                EditorGUILayout.PropertyField(m_NodeHierarchyTargetProp, new GUIContent("Node Hierarchy Targets"), true);
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