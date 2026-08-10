using UnityEngine;
using UnityEditor;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public class InspectedAssetContext
    {
        public string AssetPath { get; set; } = "";
        public NinjaNext NinjaData { get; set; }
        public object RelData { get; set; }
        public RelFileType RelType { get; set; } = RelFileType.Unknown;

        public bool IsNinjaAsset => NinjaData != null && NinjaData.Data != null;
        public bool IsRelAsset => RelData != null;
    }

    public partial class UnityNNInspectorWindow : EditorWindow
    {
        private UnityEngine.Object m_SelectedAsset;
        private InspectedAssetContext m_Context = new InspectedAssetContext();

        // Property delegate for backward compatibility across tabs
        private NinjaNext m_LoadedNinjaData => m_Context.NinjaData;

        private Vector2 m_MainScrollPosition;
        private Vector2 m_SidePaneScrollPosition;
        private int m_SelectedTab = 0;
        private string[] m_Tabs = new string[] {
            "Node Tree",
            "Meshes & Geometry",
            "Materials & Textures",
            "Motion & Animation",
            "Camera, Light & Effects",
            "REL Stage & Lighting"
        };

        private string m_DumpedJsonText = "";
        private bool m_ShowJsonOutput = false;

        [MenuItem("Window/UnityNN/Data Inspector")]
        public static void OpenWindow()
        {
            var window = GetWindow<UnityNNInspectorWindow>("UnityNN Data Inspector");
            window.minSize = new Vector2(850, 550);
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
            m_Context = new InspectedAssetContext();
            m_DumpedJsonText = "";
            m_ShowJsonOutput = false;
            ClearState();

            if (m_SelectedAsset == null) return;

            string path = AssetDatabase.GetAssetPath(m_SelectedAsset);
            if (string.IsNullOrEmpty(path)) return;

            m_Context.AssetPath = path;
            string ext = Path.GetExtension(path).ToLower();

            if (ext == ".xno" || ext == ".xnm" || ext == ".xna" || ext == ".xnc" || ext == ".xnl" ||
                ext == ".xnj" || ext == ".xnd" || ext == ".xng" || ext == ".xne" || ext == ".xni" || ext == ".xnf" || ext == ".xnv")
            {
                try
                {
                    NinjaNext ninjaLoader = new NinjaNext();
                    ninjaLoader.Load(path);

                    if (ninjaLoader.Data != null)
                    {
                        var data = ninjaLoader.Data;
                        if (data.Object != null)
                        {
                            string src;
                            data.NodeNameList = NinjaNodeNameResolver.ResolveNodeNames(data.Object, data.NodeNameList, path, null, out src);
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
                            if (data.Motion == null && extraNodeMot != null) data.Motion = extraNodeMot;
                            if (data.MaterialMotion == null && extraMatMot != null) data.MaterialMotion = extraMatMot;
                        }
                        m_Context.NinjaData = ninjaLoader;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Could not load Ninja asset {path}:\n{ex}");
                }
            }
            else if (ext == ".rel")
            {
                try
                {
                    byte[] rawData = File.ReadAllBytes(path);
                    RelFileType rType;
                    object parsedRel = RelResolver.ParseRelBytes(rawData, Path.GetFileName(path), out rType);
                    m_Context.RelData = parsedRel;
                    m_Context.RelType = rType;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Could not load REL asset {path}:\n{ex}");
                }
            }
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

            if (!m_Context.IsNinjaAsset && !m_Context.IsRelAsset)
            {
                EditorGUILayout.HelpBox("Select or assign a Ninja (.xno, .xna, .xnj, .xnm, etc.) or REL (.rel) asset to inspect.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();

            // Horizontal Split: Left Side Pane (Overview & File Stats) | Right Main Content Pane
            EditorGUILayout.BeginHorizontal();

            // 1. Left Side Pane (Fixed Width 260px)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(260));
            m_SidePaneScrollPosition = EditorGUILayout.BeginScrollView(m_SidePaneScrollPosition);
            DrawOverviewSidePane();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 2. Right Main Content Area (Category Tabs)
            EditorGUILayout.BeginVertical();
            m_SelectedTab = GUILayout.Toolbar(m_SelectedTab, m_Tabs);

            EditorGUILayout.Space();
            m_MainScrollPosition = EditorGUILayout.BeginScrollView(m_MainScrollPosition);

            switch (m_SelectedTab)
            {
                case 0: DrawNodeTreeTab(); break;
                case 1: DrawMeshesTab(); break;
                case 2: DrawMaterialsTab(); break;
                case 3: DrawMotionTab(); break;
                case 4: DrawMiscTab(); break;
                case 5: DrawRelTab(); break;
            }

            if (m_ShowJsonOutput && !string.IsNullOrEmpty(m_DumpedJsonText))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Category JSON Output", EditorStyles.boldLabel);
                if (GUILayout.Button("Copy", GUILayout.Width(60))) GUIUtility.systemCopyBuffer = m_DumpedJsonText;
                if (GUILayout.Button("Close", GUILayout.Width(60))) m_ShowJsonOutput = false;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.TextArea(m_DumpedJsonText, GUILayout.MaxHeight(200));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }
    }
}