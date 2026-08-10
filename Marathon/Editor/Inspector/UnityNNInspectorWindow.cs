using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public class ChunkSourceInfo
    {
        public bool IsPresent { get; set; }
        public bool IsLocal { get; set; }
        public string SourceDescription { get; set; } = "Absent";
        public string Details { get; set; } = "";
    }

    public class InspectedAssetContext
    {
        public string AssetPath { get; set; } = "";
        public string SourceSceneObjectName { get; set; } = "";
        public NinjaNext NinjaData { get; set; }
        public object RelData { get; set; }
        public RelFileType RelType { get; set; } = RelFileType.Unknown;

        public ChunkSourceInfo ObjectSource { get; set; } = new ChunkSourceInfo();
        public ChunkSourceInfo NodeMotionSource { get; set; } = new ChunkSourceInfo();
        public ChunkSourceInfo MaterialMotionSource { get; set; } = new ChunkSourceInfo();
        public ChunkSourceInfo TextureListSource { get; set; } = new ChunkSourceInfo();
        public ChunkSourceInfo NodeNameListSource { get; set; } = new ChunkSourceInfo();
        public ChunkSourceInfo RelSource { get; set; } = new ChunkSourceInfo();

        public bool IsNinjaAsset => NinjaData != null && NinjaData.Data != null;
        public bool IsRelAsset => RelData != null;
    }

    public partial class UnityNNInspectorWindow : EditorWindow
    {
        private UnityEngine.Object m_SelectedAsset;
        private InspectedAssetContext m_Context = new InspectedAssetContext();

        private NinjaNext m_LoadedNinjaData => m_Context.NinjaData;

        private Vector2 m_MainScrollPosition;
        private Vector2 m_RightPaneScrollPosition;
        private int m_SelectedTab = 0;

        // Default: Local Data Only (linked files disabled by default)
        private bool m_IncludeLinkedFiles = false;

        private readonly string[] m_NinjaTabNames = new string[] {
            "Node Tree",
            "Meshes & Geometry",
            "Materials & Textures",
            "Motion & Animation",
            "Camera, Light & Effects"
        };

        private readonly string[] m_RelTabNames = new string[] {
            "Stage Objects Layout",
            "Environment & Fog",
            "Enemy Spawns",
            "Quest Listing"
        };

        private string m_DumpedJsonText = "";
        private bool m_ShowJsonOutput = false;

        [MenuItem("Window/UnityNN/Data Inspector")]
        public static void OpenWindow()
        {
            var window = GetWindow<UnityNNInspectorWindow>("UnityNN Data Inspector");
            window.minSize = new Vector2(880, 550);
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

            Object targetAsset = m_SelectedAsset;
            if (m_SelectedAsset is GameObject go)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(go))
                {
                    Object prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (prefabSource != null)
                    {
                        targetAsset = prefabSource;
                        m_Context.SourceSceneObjectName = go.name;
                    }
                    else
                    {
                        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                        if (!string.IsNullOrEmpty(prefabPath))
                        {
                            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
                            if (mainAsset != null)
                            {
                                targetAsset = mainAsset;
                                m_Context.SourceSceneObjectName = go.name;
                            }
                        }
                    }
                }
            }

            string path = AssetDatabase.GetAssetPath(targetAsset);
            if (string.IsNullOrEmpty(path)) return;

            m_Context.AssetPath = path;
            string ext = Path.GetExtension(path).ToLower();

            if (ext == ".xno" || ext == ".xnm" || ext == ".xna" || ext == ".xnc" || ext == ".xnl" ||
                ext == ".xnj" || ext == ".xnd" || ext == ".xng" || ext == ".xne" || ext == ".xni" || ext == ".xnf" || ext == ".xnv" || ext == ".xnt" || ext == ".xnn")
            {
                try
                {
                    NinjaNext ninjaLoader = new NinjaNext();
                    ninjaLoader.Load(path);

                    if (ninjaLoader.Data != null)
                    {
                        m_Context.NinjaData = ninjaLoader;
                        var data = ninjaLoader.Data;

                        if (data.Object != null)
                        {
                            m_Context.ObjectSource.IsPresent = true;
                            m_Context.ObjectSource.IsLocal = true;
                            m_Context.ObjectSource.SourceDescription = "Local";
                            m_Context.ObjectSource.Details = $"{data.Object.Nodes.Count} nodes, {data.Object.SubObjects.Count} meshes, {data.Object.Materials.Count} mats";
                        }

                        if (data.Motion != null)
                        {
                            m_Context.NodeMotionSource.IsPresent = true;
                            m_Context.NodeMotionSource.IsLocal = true;
                            m_Context.NodeMotionSource.SourceDescription = "Local";
                            m_Context.NodeMotionSource.Details = $"{data.Motion.Framerate:F0} FPS, {data.Motion.SubMotions.Count} tracks";
                        }

                        if (data.MaterialMotion != null)
                        {
                            m_Context.MaterialMotionSource.IsPresent = true;
                            m_Context.MaterialMotionSource.IsLocal = true;
                            m_Context.MaterialMotionSource.SourceDescription = "Local";
                            m_Context.MaterialMotionSource.Details = $"{data.MaterialMotion.Framerate:F0} FPS, {data.MaterialMotion.SubMotions.Count} tracks";
                        }

                        if (data.TextureList != null)
                        {
                            m_Context.TextureListSource.IsPresent = true;
                            m_Context.TextureListSource.IsLocal = true;
                            m_Context.TextureListSource.SourceDescription = "Local";
                            m_Context.TextureListSource.Details = $"{data.TextureList.NinjaTextureFiles.Count} textures";
                        }

                        if (data.NodeNameList != null)
                        {
                            m_Context.NodeNameListSource.IsPresent = true;
                            m_Context.NodeNameListSource.IsLocal = true;
                            m_Context.NodeNameListSource.SourceDescription = "Local";
                            m_Context.NodeNameListSource.Details = $"{data.NodeNameList.NinjaNodeNames.Count} names";
                        }

                        if (m_IncludeLinkedFiles)
                        {
                            if (data.Object != null && !m_Context.NodeNameListSource.IsPresent)
                            {
                                string nameSrc;
                                var resolvedNames = NinjaNodeNameResolver.ResolveNodeNames(data.Object, data.NodeNameList, path, null, out nameSrc);
                                if (resolvedNames != null && resolvedNames.NinjaNodeNames.Count > 0)
                                {
                                    data.NodeNameList = resolvedNames;
                                    m_Context.NodeNameListSource.IsPresent = true;
                                    m_Context.NodeNameListSource.IsLocal = false;
                                    m_Context.NodeNameListSource.SourceDescription = "Linked";
                                    m_Context.NodeNameListSource.Details = $"{resolvedNames.NinjaNodeNames.Count} names";
                                }
                            }

                            if (!m_Context.TextureListSource.IsPresent)
                            {
                                var resolvedTexList = NinjaMaterialResolver.ResolveTextureList(null, path, null);
                                if (resolvedTexList != null && resolvedTexList.NinjaTextureFiles.Count > 0)
                                {
                                    data.TextureList = resolvedTexList;
                                    m_Context.TextureListSource.IsPresent = true;
                                    m_Context.TextureListSource.IsLocal = false;
                                    m_Context.TextureListSource.SourceDescription = "Linked (.xnt)";
                                    m_Context.TextureListSource.Details = $"{resolvedTexList.NinjaTextureFiles.Count} tex";
                                }
                            }

                            if (!m_Context.NodeMotionSource.IsPresent || !m_Context.MaterialMotionSource.IsPresent)
                            {
                                NinjaMotion extraNodeMot, extraMatMot;
                                string nodeSrc, matSrc;
                                NinjaMotionResolver.ResolveLinkedMotions(path, null, out extraNodeMot, out extraMatMot, out nodeSrc, out matSrc);

                                if (!m_Context.NodeMotionSource.IsPresent && extraNodeMot != null)
                                {
                                    data.Motion = extraNodeMot;
                                    m_Context.NodeMotionSource.IsPresent = true;
                                    m_Context.NodeMotionSource.IsLocal = false;
                                    m_Context.NodeMotionSource.SourceDescription = "Linked (.xnm)";
                                    m_Context.NodeMotionSource.Details = $"{extraNodeMot.Framerate:F0} FPS";
                                }

                                if (!m_Context.MaterialMotionSource.IsPresent && extraMatMot != null)
                                {
                                    data.MaterialMotion = extraMatMot;
                                    m_Context.MaterialMotionSource.IsPresent = true;
                                    m_Context.MaterialMotionSource.IsLocal = false;
                                    m_Context.MaterialMotionSource.SourceDescription = "Linked (.xnv)";
                                    m_Context.MaterialMotionSource.Details = $"{extraMatMot.Framerate:F0} FPS";
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Could not load Ninja asset {path}:\n{ex}");
                }
            }
            else if (ext == ".rel" || ext == ".xnr")
            {
                try
                {
                    byte[] rawData = File.ReadAllBytes(path);
                    RelFileType rType;
                    object parsedRel = RelResolver.ParseRelBytes(rawData, Path.GetFileName(path), out rType);
                    m_Context.RelData = parsedRel;
                    m_Context.RelType = rType;

                    m_Context.RelSource.IsPresent = true;
                    m_Context.RelSource.IsLocal = true;
                    m_Context.RelSource.SourceDescription = $"Local ({ext})";
                    m_Context.RelSource.Details = $"Type: {rType}";
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Could not load REL/XNR asset {path}:\n{ex}");
                }
            }

            EnsureActiveTab();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();

            // Top Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("UnityNN Inspector", EditorStyles.boldLabel, GUILayout.Width(130));

            EditorGUI.BeginChangeCheck();
            m_IncludeLinkedFiles = GUILayout.Toggle(m_IncludeLinkedFiles, "Include Linked Support Files", EditorStyles.toolbarButton, GUILayout.Width(170));
            if (EditorGUI.EndChangeCheck())
            {
                LoadSelectedAsset();
            }

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(m_Context.SourceSceneObjectName))
            {
                GUILayout.Label($"Scene Target: [{m_Context.SourceSceneObjectName}] -> {m_Context.AssetPath}", EditorStyles.miniLabel);
            }
            else if (!string.IsNullOrEmpty(m_Context.AssetPath))
            {
                GUILayout.Label(m_Context.AssetPath, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();
            m_SelectedAsset = EditorGUILayout.ObjectField("Target Asset / Scene Selection", m_SelectedAsset, typeof(UnityEngine.Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                LoadSelectedAsset();
            }

            if (!m_Context.IsNinjaAsset && !m_Context.IsRelAsset)
            {
                EditorGUILayout.HelpBox("Select a Ninja asset (.xno, .xna, .xnj, .xnm, .xnt, etc.), REL file (.rel, .xnr), or a GameObject in the scene hierarchy.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(2);

            // Horizontal Split: Left Main Content Pane | Right Persistent Metrics Pane
            EditorGUILayout.BeginHorizontal();

            // 1. Left Main Content Pane (Category Tabs Toolbar & Tab Content)
            EditorGUILayout.BeginVertical();
            DrawDynamicTabToolbar();

            EditorGUILayout.Space(4);
            m_MainScrollPosition = EditorGUILayout.BeginScrollView(m_MainScrollPosition);

            if (m_Context.IsNinjaAsset)
            {
                switch (m_SelectedTab)
                {
                    case 0: DrawNodeTreeTab(); break;
                    case 1: DrawMeshesTab(); break;
                    case 2: DrawMaterialsTab(); break;
                    case 3: DrawMotionTab(); break;
                    case 4: DrawMiscTab(); break;
                }
            }
            else if (m_Context.IsRelAsset)
            {
                DrawRelTab();
            }

            if (m_ShowJsonOutput && !string.IsNullOrEmpty(m_DumpedJsonText))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Category JSON Output", EditorStyles.boldLabel);
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(120))) GUIUtility.systemCopyBuffer = m_DumpedJsonText;
                if (GUILayout.Button("Close", GUILayout.Width(60))) m_ShowJsonOutput = false;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.TextArea(m_DumpedJsonText, GUILayout.MaxHeight(180));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 2. Right Persistent Metrics Pane (Width 280px) - Disables horizontal scrollbars
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(280));
            m_RightPaneScrollPosition = EditorGUILayout.BeginScrollView(m_RightPaneScrollPosition, false, false);
            DrawPersistentOverviewTableRightPane();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private bool[] GetCurrentTabAvailabilityFlags()
        {
            if (m_Context.IsNinjaAsset)
            {
                bool[] flags = new bool[m_NinjaTabNames.Length];
                var data = m_Context.NinjaData.Data;

                flags[0] = data.Object != null && data.Object.Nodes != null && data.Object.Nodes.Count > 0;
                flags[1] = data.Object != null && ((data.Object.SubObjects != null && data.Object.SubObjects.Count > 0) || (data.Object.VertexLists != null && data.Object.VertexLists.Count > 0));
                flags[2] = (data.Object != null && data.Object.Materials != null && data.Object.Materials.Count > 0) || (data.TextureList != null && data.TextureList.NinjaTextureFiles != null && data.TextureList.NinjaTextureFiles.Count > 0);
                flags[3] = data.Motion != null || data.MaterialMotion != null;
                flags[4] = data.Camera != null || data.Light != null || data.EffectList != null || data.NodeNameList != null;

                return flags;
            }

            if (m_Context.IsRelAsset)
            {
                bool[] flags = new bool[m_RelTabNames.Length];
                flags[0] = m_Context.RelData is SetFileData;
                flags[1] = m_Context.RelData is LndEffectData || m_Context.RelData is List<LndFogData>;
                flags[2] = m_Context.RelData is EnemyLayoutData;
                flags[3] = m_Context.RelData is List<QuestListingData>;
                return flags;
            }

            return new bool[0];
        }

        private void DrawDynamicTabToolbar()
        {
            string[] tabNames = m_Context.IsNinjaAsset ? m_NinjaTabNames : (m_Context.IsRelAsset ? m_RelTabNames : new string[0]);
            bool[] activeFlags = GetCurrentTabAvailabilityFlags();

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool isEnabled = i < activeFlags.Length && activeFlags[i];
                EditorGUI.BeginDisabledGroup(!isEnabled);

                GUIStyle btnStyle = (i == m_SelectedTab) ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold } : GUI.skin.button;
                if (GUILayout.Button(tabNames[i], btnStyle) && isEnabled)
                {
                    m_SelectedTab = i;
                }

                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void EnsureActiveTab()
        {
            bool[] activeFlags = GetCurrentTabAvailabilityFlags();
            if (m_SelectedTab >= activeFlags.Length || !activeFlags[m_SelectedTab])
            {
                m_SelectedTab = 0;
                for (int i = 0; i < activeFlags.Length; i++)
                {
                    if (activeFlags[i])
                    {
                        m_SelectedTab = i;
                        break;
                    }
                }
            }
        }
    }
}