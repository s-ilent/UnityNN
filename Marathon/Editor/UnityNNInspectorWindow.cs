// File: Marathon/Editor/UnityNNInspectorWindow.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        private UnityEngine.Object m_SelectedAsset;
        private NinjaNext m_LoadedNinjaData;
        private Vector2 m_ScrollPosition;
        private int m_SelectedTab = 0;
        private string[] m_Tabs = new string[] {
            "Overview",
            "Node Tree",
            "Meshes & Geometry",
            "Materials & Textures",
            "Motion & Animation",
            "Camera, Light & Effects"
        };

        private string m_NodeSearchFilter = "";
        private bool m_ExpandAllNodes = false;
        private string m_DumpedJsonText = "";
        private bool m_ShowJsonOutput = false;

        private Dictionary<int, bool> m_NodeFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_SubObjectFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_VertexListFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, int> m_VertexListPages = new Dictionary<int, int>();
        private Dictionary<int, bool> m_PrimitiveListFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_MaterialFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_MaterialColourFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_MaterialLogicFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_TextureMapFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_SubMotionFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, int> m_SubMotionPages = new Dictionary<int, int>();
        private Dictionary<int, bool> m_MatSubMotionFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, int> m_MatSubMotionPages = new Dictionary<int, int>();

        private const int ITEMS_PER_PAGE = 50;

        [MenuItem("Window/UnityNN/Data Inspector")]
        public static void OpenWindow()
        {
            var window = GetWindow<UnityNNInspectorWindow>("UnityNN Data Inspector");
            window.minSize = new Vector2(650, 500);
            window.Show();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject != m_SelectedAsset)
            {
                m_SelectedAsset = Selection.activeObject;
                LoadSelectedAsset();
                Repaint();
            }
        }

        private void LoadSelectedAsset()
        {
            m_LoadedNinjaData = null;
            m_DumpedJsonText = "";
            m_ShowJsonOutput = false;
            ClearState();

            if (m_SelectedAsset == null) return;

            string path = AssetDatabase.GetAssetPath(m_SelectedAsset);
            if (string.IsNullOrEmpty(path)) return;

            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".xno" || ext == ".xnm" || ext == ".xna" || ext == ".xnc" || ext == ".xnl" ||
                ext == ".xnj" || ext == ".xnd" || ext == ".xng" || ext == ".xne" || ext == ".xni" ||
                ext == ".xnf" || ext == ".xnt" || ext == ".xnv" || ext == ".xnr")
            {
                try
                {
                    m_LoadedNinjaData = new NinjaNext();
                    m_LoadedNinjaData.Load(path);

                    if (m_LoadedNinjaData.Data != null)
                    {
                        var data = m_LoadedNinjaData.Data;

                        if (data.Object != null)
                        {
                            string nameSource;
                            data.NodeNameList = NinjaNodeNameResolver.ResolveNodeNames(data.Object, data.NodeNameList, path, null, out nameSource);
                        }

                        if (data.TextureList == null)
                        {
                            data.TextureList = NinjaMaterialResolver.ResolveTextureList(null, path, null);
                        }

                        if (data.Motion == null || data.MaterialMotion == null)
                        {
                            NinjaMotion extraNodeMot, extraMatMot;
                            string nodeSrc, matSrc;
                            NinjaMotionResolver.ResolveLinkedMotions(path, null, out extraNodeMot, out extraMatMot, out nodeSrc, out matSrc);

                            if (data.Motion == null && extraNodeMot != null)
                            {
                                data.Motion = extraNodeMot;
                            }

                            if (data.MaterialMotion == null && extraMatMot != null)
                            {
                                data.MaterialMotion = extraMatMot;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Could not inspect asset {path}:\n{ex}");
                    m_LoadedNinjaData = null;
                }
            }
        }

        private void ClearState()
        {
            m_NodeFoldouts.Clear();
            m_SubObjectFoldouts.Clear();
            m_VertexListFoldouts.Clear();
            m_VertexListPages.Clear();
            m_PrimitiveListFoldouts.Clear();
            m_MaterialFoldouts.Clear();
            m_MaterialColourFoldouts.Clear();
            m_MaterialLogicFoldouts.Clear();
            m_TextureMapFoldouts.Clear();
            m_SubMotionFoldouts.Clear();
            m_SubMotionPages.Clear();
            m_MatSubMotionFoldouts.Clear();
            m_MatSubMotionPages.Clear();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UnityNN Asset Data Inspector", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            m_SelectedAsset = EditorGUILayout.ObjectField("Target Asset", m_SelectedAsset, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadSelectedAsset();
            }

            if (m_LoadedNinjaData == null || m_LoadedNinjaData.Data == null)
            {
                EditorGUILayout.HelpBox("Select or assign a Ninja asset (.xno, .xnm, .xna, .xnj, .xnd, .xng, .xnc, .xnl, .xnv, etc.) to inspect its internal data.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            m_SelectedTab = GUILayout.Toolbar(m_SelectedTab, m_Tabs);
            if (GUILayout.Button("Dump JSON", GUILayout.Width(100)))
            {
                DumpCurrentCategoryJson();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            switch (m_SelectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawNodeTreeTab(); break;
                case 2: DrawMeshesTab(); break;
                case 3: DrawMaterialsTab(); break;
                case 4: DrawMotionTab(); break;
                case 5: DrawMiscTab(); break;
            }

            if (m_ShowJsonOutput && !string.IsNullOrEmpty(m_DumpedJsonText))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Dumped Category JSON Output", EditorStyles.boldLabel);
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(130)))
                {
                    GUIUtility.systemCopyBuffer = m_DumpedJsonText;
                    EditorUtility.DisplayDialog("Copied", "Category JSON copied to system clipboard!", "OK");
                }
                if (GUILayout.Button("Save to File...", GUILayout.Width(110)))
                {
                    string savePath = EditorUtility.SaveFilePanel("Save Category JSON", "", $"{m_Tabs[m_SelectedTab]}_Data.json", "json");
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        File.WriteAllText(savePath, m_DumpedJsonText);
                        EditorUtility.DisplayDialog("Saved", $"Saved JSON to {savePath}", "OK");
                    }
                }
                if (GUILayout.Button("Close", GUILayout.Width(60)))
                {
                    m_ShowJsonOutput = false;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.TextArea(m_DumpedJsonText, GUILayout.MaxHeight(250));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DumpCurrentCategoryJson()
        {
            if (m_LoadedNinjaData == null || m_LoadedNinjaData.Data == null) return;

            var data = m_LoadedNinjaData.Data;
            object targetCategoryData = null;

            switch (m_SelectedTab)
            {
                case 0:
                    targetCategoryData = data;
                    break;
                case 1:
                    targetCategoryData = data.Object?.Nodes;
                    break;
                case 2:
                    targetCategoryData = new {
                        SubObjects = data.Object?.SubObjects,
                        VertexLists = data.Object?.VertexLists,
                        PrimitiveLists = data.Object?.PrimitiveLists
                    };
                    break;
                case 3:
                    targetCategoryData = new {
                        Materials = data.Object?.Materials,
                        MaterialColours = data.Object?.MaterialColours,
                        MaterialLogics = data.Object?.MaterialLogics,
                        TextureMaps = data.Object?.TextureMaps,
                        TextureList = data.TextureList
                    };
                    break;
                case 4:
                    targetCategoryData = new {
                        NodeMotion = data.Motion,
                        MaterialMotion = data.MaterialMotion
                    };
                    break;
                case 5:
                    targetCategoryData = new {
                        Camera = data.Camera,
                        Light = data.Light,
                        EffectList = data.EffectList,
                        NodeNameList = data.NodeNameList
                    };
                    break;
            }

            m_DumpedJsonText = NinjaJsonSerializer.Serialize(targetCategoryData);
            GUIUtility.systemCopyBuffer = m_DumpedJsonText;
            m_ShowJsonOutput = true;

            EditorUtility.DisplayDialog("JSON Dumped", $"Successfully dumped '{m_Tabs[m_SelectedTab]}' data as JSON!\nCopied to system clipboard.", "OK");
        }
    }
}