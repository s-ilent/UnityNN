using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public class UnityNNInspectorWindow : EditorWindow
    {
        private Object m_SelectedAsset;
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

        // Foldout and UI state storage
        private string m_NodeSearchFilter = "";
        private bool m_ExpandAllNodes = false;
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
                    
                    if (m_LoadedNinjaData.Data != null && m_LoadedNinjaData.Data.TextureList == null)
                    {
                        m_LoadedNinjaData.Data.TextureList = NinjaMaterialResolver.ResolveTextureList(null, path);
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
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UnityNN Asset Data Inspector", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            m_SelectedAsset = EditorGUILayout.ObjectField("Target Asset", m_SelectedAsset, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadSelectedAsset();
            }

            if (m_LoadedNinjaData == null || m_LoadedNinjaData.Data == null)
            {
                EditorGUILayout.HelpBox("Select or assign a Ninja asset (.xn*) to inspect its internal data.", MessageType.Info);
                return;
            }

            m_SelectedTab = GUILayout.Toolbar(m_SelectedTab, m_Tabs);
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

            EditorGUILayout.EndScrollView();
        }

        #region Overview Tab
        private void DrawOverviewTab()
        {
            var data = m_LoadedNinjaData.Data;

            EditorGUILayout.LabelField("Chunk Summary", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Object Chunk (NXOB):", data.Object != null ? $"Present ({data.Object.Nodes.Count} nodes, {data.Object.SubObjects.Count} sub-objects)" : "Absent");
            EditorGUILayout.LabelField("Motion Chunk (NXMA/NXMO):", data.Motion != null ? $"Present ({data.Motion.ChunkID}, {data.Motion.SubMotions.Count} tracks, {data.Motion.Framerate} FPS)" : "Absent");
            EditorGUILayout.LabelField("Texture List (NXTL):", data.TextureList != null ? $"Present ({data.TextureList.NinjaTextureFiles.Count} textures)" : "Absent");
            EditorGUILayout.LabelField("Node Name List (NXNN):", data.NodeNameList != null ? $"Present ({data.NodeNameList.NinjaNodeNames.Count} names, Sort: {data.NodeNameList.Type})" : "Absent");
            EditorGUILayout.LabelField("Effect List (NXEF):", data.EffectList != null ? $"Present ({data.EffectList.NinjaEffectFiles.Count} effects, {data.EffectList.NinjaTechniqueNames.Count} techniques)" : "Absent");
            EditorGUILayout.LabelField("Camera Chunk (NXCA):", data.Camera != null ? $"Present ({data.Camera.Type})" : "Absent");
            EditorGUILayout.LabelField("Light Chunk (NXLI):", data.Light != null ? $"Present ({data.Light.Type})" : "Absent");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            if (data.Object != null)
            {
                EditorGUILayout.LabelField("Object Metadata", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Bounding Center:", data.Object.Center.ToString("F4"));
                EditorGUILayout.LabelField("Bounding Radius:", data.Object.Radius.ToString("F4"));
                EditorGUILayout.LabelField("Max Node Depth:", data.Object.MaxNodeDepth.ToString());
                EditorGUILayout.LabelField("Matrix Index Count:", data.Object.MatrixIndexCount.ToString());
                EditorGUILayout.LabelField("Texture Count:", data.Object.TextureCount.ToString());
                EditorGUILayout.LabelField("Node Count:", data.Object.Nodes != null ? data.Object.Nodes.Count.ToString() : "0");
                EditorGUILayout.LabelField("SubObject Count:", data.Object.SubObjects != null ? data.Object.SubObjects.Count.ToString() : "0");
                EditorGUILayout.LabelField("Material Count:", data.Object.Materials != null ? data.Object.Materials.Count.ToString() : "0");
                EditorGUILayout.LabelField("Material Colours Count:", data.Object.MaterialColours != null ? data.Object.MaterialColours.Count.ToString() : "0");
                EditorGUILayout.LabelField("Material Logics Count:", data.Object.MaterialLogics != null ? data.Object.MaterialLogics.Count.ToString() : "0");
                EditorGUILayout.LabelField("Texture Map Count:", data.Object.TextureMaps != null ? data.Object.TextureMaps.Count.ToString() : "0");
                EditorGUILayout.LabelField("Vertex List Count:", data.Object.VertexLists != null ? data.Object.VertexLists.Count.ToString() : "0");
                EditorGUILayout.LabelField("Primitive List Count:", data.Object.PrimitiveLists != null ? data.Object.PrimitiveLists.Count.ToString() : "0");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.Motion != null)
            {
                EditorGUILayout.LabelField("Motion Metadata", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Chunk ID:", data.Motion.ChunkID ?? "N/A");
                EditorGUILayout.LabelField("Motion Type:", data.Motion.Type.ToString());
                EditorGUILayout.LabelField("Frame Range:", $"{data.Motion.StartFrame:F2} to {data.Motion.EndFrame:F2}");
                EditorGUILayout.LabelField("Framerate:", $"{data.Motion.Framerate:F2} FPS");
                EditorGUILayout.LabelField("SubMotions Count:", data.Motion.SubMotions != null ? data.Motion.SubMotions.Count.ToString() : "0");
                EditorGUILayout.LabelField("Reserved Values:", $"[0]: {data.Motion.Reserved0}, [1]: {data.Motion.Reserved1}");
                EditorGUI.indentLevel--;
            }
        }
        #endregion

        #region Node Tree Tab
        private void DrawNodeTreeTab()
        {
            if (m_LoadedNinjaData.Data.Object == null || m_LoadedNinjaData.Data.Object.Nodes == null)
            {
                EditorGUILayout.HelpBox("No Object/Node tree data present in this file.", MessageType.Info);
                return;
            }

            var nodes = m_LoadedNinjaData.Data.Object.Nodes;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Nodes: {nodes.Count}", EditorStyles.boldLabel);
            if (GUILayout.Button(m_ExpandAllNodes ? "Collapse All" : "Expand All", GUILayout.Width(100)))
            {
                m_ExpandAllNodes = !m_ExpandAllNodes;
                for (int i = 0; i < nodes.Count; i++)
                    m_NodeFoldouts[i] = m_ExpandAllNodes;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
            m_NodeSearchFilter = EditorGUILayout.TextField(m_NodeSearchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(50))) m_NodeSearchFilter = "";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                string displayName = string.IsNullOrEmpty(n.Name) ? $"Node_{i:0000}" : n.Name;

                if (!string.IsNullOrEmpty(m_NodeSearchFilter) &&
                    !displayName.ToLower().Contains(m_NodeSearchFilter.ToLower()) &&
                    !i.ToString().Contains(m_NodeSearchFilter))
                {
                    continue;
                }

                if (!m_NodeFoldouts.ContainsKey(i))
                    m_NodeFoldouts[i] = m_ExpandAllNodes;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                m_NodeFoldouts[i] = EditorGUILayout.Foldout(
                    m_NodeFoldouts[i],
                    $"[{i}] {displayName} (Type: {n.Type})",
                    true
                );

                if (m_NodeFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Name:", n.Name ?? "");
                    EditorGUILayout.LabelField("Node Type:", n.Type.ToString());
                    EditorGUILayout.LabelField("Matrix Index:", n.MatrixIndex.ToString());
                    EditorGUILayout.LabelField("User Defined:", n.UserDefined.ToString("X8"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Hierarchy Indices:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Parent: {n.ParentIndex} | Child: {n.ChildIndex} | Sibling: {n.SiblingIndex}");

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Transform:", EditorStyles.boldLabel);
                    EditorGUILayout.Vector3Field("Translation", n.Translation);
                    EditorGUILayout.Vector3Field("Rotation", n.Rotation);
                    EditorGUILayout.Vector3Field("Scaling", n.Scaling);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Bounding Data:", EditorStyles.boldLabel);
                    EditorGUILayout.Vector3Field("Center", n.Center);
                    EditorGUILayout.FloatField("Radius", n.Radius);
                    EditorGUILayout.Vector3Field("Bounding Box", n.BoundingBox);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Inverse Initial Matrix:", EditorStyles.boldLabel);
                    Matrix4x4 mat = n.InvInitMatrix;
                    EditorGUILayout.LabelField($"R0: {mat.m00:F4}, {mat.m01:F4}, {mat.m02:F4}, {mat.m03:F4}");
                    EditorGUILayout.LabelField($"R1: {mat.m10:F4}, {mat.m11:F4}, {mat.m12:F4}, {mat.m13:F4}");
                    EditorGUILayout.LabelField($"R2: {mat.m20:F4}, {mat.m21:F4}, {mat.m22:F4}, {mat.m23:F4}");
                    EditorGUILayout.LabelField($"R3: {mat.m30:F4}, {mat.m31:F4}, {mat.m32:F4}, {mat.m33:F4}");

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
        }
        #endregion

        #region Meshes Tab
        private void DrawMeshesTab()
        {
            if (m_LoadedNinjaData.Data.Object == null)
            {
                EditorGUILayout.HelpBox("No Object/Mesh data present in this file.", MessageType.Info);
                return;
            }

            var obj = m_LoadedNinjaData.Data.Object;

            if (obj.SubObjects != null)
            {
                EditorGUILayout.LabelField("SubObjects & Mesh Sets", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total SubObjects: {obj.SubObjects.Count}");

                for (int i = 0; i < obj.SubObjects.Count; i++)
                {
                    var sub = obj.SubObjects[i];
                    if (sub == null) continue;

                    if (!m_SubObjectFoldouts.ContainsKey(i)) m_SubObjectFoldouts[i] = false;

                    int meshSetCount = sub.MeshSets != null ? sub.MeshSets.Count : 0;
                    int texCount = sub.TextureIndices != null ? sub.TextureIndices.Count : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_SubObjectFoldouts[i] = EditorGUILayout.Foldout(
                        m_SubObjectFoldouts[i],
                        $"SubObject [{i}] - Type: {sub.Type} | MeshSets: {meshSetCount} | Textures: {texCount}",
                        true
                    );

                    if (m_SubObjectFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("SubObject Type:", sub.Type.ToString());

                        if (sub.TextureIndices != null && sub.TextureIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField("Texture Indices:", string.Join(", ", sub.TextureIndices));
                        }

                        if (sub.MeshSets != null && sub.MeshSets.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Mesh Sets:", EditorStyles.boldLabel);

                            for (int j = 0; j < sub.MeshSets.Count; j++)
                            {
                                var ms = sub.MeshSets[j];
                                if (ms == null) continue;

                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                EditorGUILayout.LabelField($"MeshSet [{j}]", EditorStyles.boldLabel);
                                EditorGUILayout.LabelField($"Node Index: {ms.NodeIndex} | Matrix Index: {ms.MatrixIndex}");
                                EditorGUILayout.LabelField($"Material Index: {ms.MaterialIndex} | Shader Index: {ms.ShaderIndex}");
                                EditorGUILayout.LabelField($"Vertex List Index: {ms.VertexListIndex} | Primitive List Index: {ms.PrimitiveListIndex}");
                                EditorGUILayout.Vector3Field("Center", ms.Center);
                                EditorGUILayout.FloatField("Radius", ms.Radius);
                                EditorGUILayout.EndVertical();
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (obj.VertexLists != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Vertex Lists", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Vertex Lists: {obj.VertexLists.Count}");

                for (int i = 0; i < obj.VertexLists.Count; i++)
                {
                    var vl = obj.VertexLists[i];
                    if (vl == null) continue;

                    if (!m_VertexListFoldouts.ContainsKey(i)) m_VertexListFoldouts[i] = false;
                    if (!m_VertexListPages.ContainsKey(i)) m_VertexListPages[i] = 0;

                    int vCount = vl.Vertices != null ? vl.Vertices.Count : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_VertexListFoldouts[i] = EditorGUILayout.Foldout(
                        m_VertexListFoldouts[i],
                        $"Vertex List [{i}] - Count: {vCount} | Format: {vl.Format} | FVF: {vl.FlexibleVertexFormat}",
                        true
                    );

                    if (m_VertexListFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Type:", vl.Type.ToString());
                        EditorGUILayout.LabelField("Format:", vl.Format.ToString());
                        EditorGUILayout.LabelField("Flexible Vertex Format:", vl.FlexibleVertexFormat.ToString());
                        EditorGUILayout.LabelField("HDR Flags:", $"Common: {vl.HDRCommon}, Data: {vl.HDRData}, Lock: {vl.HDRLock}");

                        if (vl.BoneMatrixIndices != null && vl.BoneMatrixIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField("Bone Matrix Indices:", string.Join(", ", vl.BoneMatrixIndices));
                        }

                        if (vl.Vertices != null && vl.Vertices.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Vertices Inspection:", EditorStyles.boldLabel);

                            int totalVertices = vl.Vertices.Count;
                            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalVertices / ITEMS_PER_PAGE));
                            int currentPage = m_VertexListPages[i];

                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("Prev Page", GUILayout.Width(80)) && currentPage > 0)
                            {
                                currentPage--;
                            }
                            EditorGUILayout.LabelField($"Page {currentPage + 1} / {totalPages} (Vertices {currentPage * ITEMS_PER_PAGE} - {Mathf.Min(totalVertices, (currentPage + 1) * ITEMS_PER_PAGE) - 1})");
                            if (GUILayout.Button("Next Page", GUILayout.Width(80)) && currentPage < totalPages - 1)
                            {
                                currentPage++;
                            }
                            m_VertexListPages[i] = currentPage;
                            EditorGUILayout.EndHorizontal();

                            int startIdx = currentPage * ITEMS_PER_PAGE;
                            int endIdx = Mathf.Min(totalVertices, (currentPage + 1) * ITEMS_PER_PAGE);

                            for (int vIdx = startIdx; vIdx < endIdx; vIdx++)
                            {
                                var v = vl.Vertices[vIdx];
                                if (v == null) continue;

                                EditorGUILayout.LabelField($"Vertex [{vIdx}]:", EditorStyles.miniBoldLabel);
                                EditorGUI.indentLevel++;

                                if (v.Position.HasValue) EditorGUILayout.Vector3Field("Pos", v.Position.Value);
                                if (v.Normals.HasValue) EditorGUILayout.Vector3Field("Normal", v.Normals.Value);
                                if (v.Tangent.HasValue) EditorGUILayout.Vector3Field("Tangent", v.Tangent.Value);
                                if (v.Binormals.HasValue) EditorGUILayout.Vector3Field("Binormal", v.Binormals.Value);
                                if (v.Weight.HasValue) EditorGUILayout.Vector3Field("Weight", v.Weight.Value);

                                if (v.MatrixIndices != null && v.MatrixIndices.Length > 0)
                                    EditorGUILayout.LabelField("Matrix Indices:", string.Join(", ", v.MatrixIndices));

                                if (v.VertexColours != null && v.VertexColours.Length >= 4)
                                    EditorGUILayout.ColorField("Color", new Color32(v.VertexColours[0], v.VertexColours[1], v.VertexColours[2], v.VertexColours[3]));

                                if (v.VertexColours2 != null && v.VertexColours2.Length >= 4)
                                    EditorGUILayout.ColorField("Color2", new Color32(v.VertexColours2[0], v.VertexColours2[1], v.VertexColours2[2], v.VertexColours2[3]));

                                if (v.TextureCoordinates != null && v.TextureCoordinates.Count > 0)
                                {
                                    for (int uvIdx = 0; uvIdx < v.TextureCoordinates.Count; uvIdx++)
                                        EditorGUILayout.Vector2Field($"UV[{uvIdx}]", v.TextureCoordinates[uvIdx]);
                                }

                                EditorGUI.indentLevel--;
                            }
                        }

                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            if (obj.PrimitiveLists != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Primitive Lists", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Primitive Lists: {obj.PrimitiveLists.Count}");

                for (int i = 0; i < obj.PrimitiveLists.Count; i++)
                {
                    var pl = obj.PrimitiveLists[i];
                    if (pl == null) continue;

                    if (!m_PrimitiveListFoldouts.ContainsKey(i)) m_PrimitiveListFoldouts[i] = false;

                    int stripCount = pl.StripIndices != null ? pl.StripIndices.Count : 0;
                    int indexCount = pl.IndexIndices != null ? pl.IndexIndices.Count : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_PrimitiveListFoldouts[i] = EditorGUILayout.Foldout(
                        m_PrimitiveListFoldouts[i],
                        $"Primitive List [{i}] - Type: {pl.Type} | Format: {pl.Format} | Strip Count: {stripCount} | Index Count: {indexCount}",
                        true
                    );

                    if (m_PrimitiveListFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Primitive Type:", pl.Type.ToString());
                        EditorGUILayout.LabelField("Format:", pl.Format.ToString());
                        EditorGUILayout.LabelField("Index Buffer:", pl.IndexBuffer.ToString());
                        EditorGUILayout.LabelField("Reserved:", $"[0]: {pl.Reserved0}, [1]: {pl.Reserved1}");

                        if (pl.StripIndices != null && pl.StripIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField($"Strip Indices ({pl.StripIndices.Count}):", EditorStyles.boldLabel);
                            string stripStr = string.Join(", ", pl.StripIndices.GetRange(0, Mathf.Min(pl.StripIndices.Count, 100)));
                            if (pl.StripIndices.Count > 100) stripStr += "...";
                            EditorGUILayout.HelpBox(stripStr, MessageType.None);
                        }

                        if (pl.IndexIndices != null && pl.IndexIndices.Count > 0)
                        {
                            EditorGUILayout.LabelField($"Index Indices ({pl.IndexIndices.Count}):", EditorStyles.boldLabel);
                            string indexStr = string.Join(", ", pl.IndexIndices.GetRange(0, Mathf.Min(pl.IndexIndices.Count, 100)));
                            if (pl.IndexIndices.Count > 100) indexStr += "...";
                            EditorGUILayout.HelpBox(indexStr, MessageType.None);
                        }

                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion

        #region Materials Tab
        private void DrawMaterialsTab()
        {
            if (m_LoadedNinjaData.Data.Object == null && m_LoadedNinjaData.Data.TextureList == null)
            {
                EditorGUILayout.HelpBox("No Material or Texture data present in this file.", MessageType.Info);
                return;
            }

            var obj = m_LoadedNinjaData.Data.Object;

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
                            EditorGUILayout.LabelField("Reserved Data:", $"[0]: {mat.Reserved0}, [1]: {mat.Reserved1}, [2]: {mat.Reserved2}");
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
                            EditorGUILayout.LabelField("Reserved:", $"[0]: {mc.Reserved0}, [1]: {mc.Reserved1}, [2]: {mc.Reserved2}");
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
                            EditorGUILayout.LabelField("Blend Factor:", ml.BlendFactor.ToString());
                            EditorGUILayout.LabelField("Blend Op:", ml.BlendOperation.ToString());
                            EditorGUILayout.LabelField("Logic Op:", ml.LogicOperation.ToString());
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Alpha Enabled:", ml.Alpha.ToString());
                            EditorGUILayout.LabelField("Alpha Function:", ml.AlphaFunction.ToString());
                            EditorGUILayout.LabelField("Alpha Ref:", ml.AlphaRef.ToString());
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("ZComparison Enabled:", ml.ZComparison.ToString());
                            EditorGUILayout.LabelField("ZComparison Function:", ml.ZComparisonFunction.ToString());
                            EditorGUILayout.LabelField("ZUpdate:", ml.ZUpdate.ToString());
                            EditorGUILayout.LabelField("Reserved:", $"[0]: {ml.Reserved0}, [1]: {ml.Reserved1}, [2]: {ml.Reserved2}, [3]: {ml.Reserved3}");
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                if (obj.TextureMaps != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Texture Maps", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Texture Maps: {obj.TextureMaps.Count}");

                    for (int i = 0; i < obj.TextureMaps.Count; i++)
                    {
                        var tm = obj.TextureMaps[i];
                        if (tm == null) continue;

                        if (!m_TextureMapFoldouts.ContainsKey(i)) m_TextureMapFoldouts[i] = false;

                        int descCount = tm.NinjaTextureMapDescriptions != null ? tm.NinjaTextureMapDescriptions.Count : 0;

                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        m_TextureMapFoldouts[i] = EditorGUILayout.Foldout(
                            m_TextureMapFoldouts[i],
                            $"Texture Map [{i}] - Descriptions: {descCount} (Offset: 0x{tm.Offset:X8})",
                            true
                        );

                        if (m_TextureMapFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            if (tm.NinjaTextureMapDescriptions != null)
                            {
                                for (int j = 0; j < tm.NinjaTextureMapDescriptions.Count; j++)
                                {
                                    var desc = tm.NinjaTextureMapDescriptions[j];
                                    if (desc == null) continue;

                                    EditorGUILayout.LabelField($"Description [{j}]:", EditorStyles.miniBoldLabel);
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.LabelField("Type:", desc.Type.ToString());
                                    EditorGUILayout.LabelField("Texture Index:", desc.Index.ToString());
                                    EditorGUILayout.Vector2Field("Offset", desc.Offset);
                                    EditorGUILayout.FloatField("Blend", desc.Blend);
                                    EditorGUILayout.LabelField("Texture Info:", desc.TextureInfo.ToString());
                                    EditorGUILayout.LabelField("Min Filter:", desc.MinFilter.ToString());
                                    EditorGUILayout.LabelField("Mag Filter:", desc.MagFilter.ToString());
                                    EditorGUILayout.FloatField("MipMap Bias", desc.MipMapBias);
                                    EditorGUILayout.LabelField("Max Mip Level:", desc.MaxMipLevel.ToString());
                                    EditorGUILayout.LabelField("Reserved:", $"[0]: {desc.Reserved0}, [1]: {desc.Reserved1}, [2]: {desc.Reserved2}");
                                    EditorGUI.indentLevel--;
                                }
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
            }

            if (m_LoadedNinjaData.Data.TextureList != null && m_LoadedNinjaData.Data.TextureList.NinjaTextureFiles != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Texture List (NXTL)", EditorStyles.boldLabel);
                var texList = m_LoadedNinjaData.Data.TextureList.NinjaTextureFiles;
                EditorGUILayout.LabelField($"Total Textures in List: {texList.Count}");

                for (int i = 0; i < texList.Count; i++)
                {
                    var tf = texList[i];
                    if (tf == null) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"[{i}] {tf.FileName ?? ""}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Type: {tf.Type} | Bank: {tf.Bank} | Global Index: {tf.GlobalIndex}");
                    EditorGUILayout.LabelField($"Min Filter: {tf.MinFilter} | Mag Filter: {tf.MagFilter}");
                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion

        #region Motion Tab
        private void DrawMotionTab()
        {
            if (m_LoadedNinjaData.Data.Motion == null)
            {
                EditorGUILayout.HelpBox("No Motion data present in this file.", MessageType.Info);
                return;
            }

            var mot = m_LoadedNinjaData.Data.Motion;
            EditorGUILayout.LabelField("Motion Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Chunk ID: {mot.ChunkID ?? "N/A"} | Motion Type: {mot.Type}");
            EditorGUILayout.LabelField($"Frame Range: {mot.StartFrame:F2} - {mot.EndFrame:F2} | Framerate: {mot.Framerate:F2} FPS");
            EditorGUILayout.LabelField($"Reserved Data: [0]: {mot.Reserved0}, [1]: {mot.Reserved1}");

            if (mot.SubMotions != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"SubMotions / Animation Tracks ({mot.SubMotions.Count})", EditorStyles.boldLabel);

                var nodes = m_LoadedNinjaData.Data.Object?.Nodes;
                var nodeNames = m_LoadedNinjaData.Data.NodeNameList?.NinjaNodeNames;

                for (int i = 0; i < mot.SubMotions.Count; i++)
                {
                    var sm = mot.SubMotions[i];
                    if (sm == null) continue;

                    if (!m_SubMotionFoldouts.ContainsKey(i)) m_SubMotionFoldouts[i] = false;
                    if (!m_SubMotionPages.ContainsKey(i)) m_SubMotionPages[i] = 0;

                    string targetNodeName = $"Node_{sm.NodeIndex:0000}";
                    if (nodes != null && sm.NodeIndex >= 0 && sm.NodeIndex < nodes.Count && !string.IsNullOrEmpty(nodes[sm.NodeIndex].Name))
                        targetNodeName = nodes[sm.NodeIndex].Name;
                    else if (nodeNames != null && sm.NodeIndex >= 0 && sm.NodeIndex < nodeNames.Count)
                        targetNodeName = nodeNames[sm.NodeIndex];

                    int kfCount = sm.Keyframes != null ? sm.Keyframes.Count : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    m_SubMotionFoldouts[i] = EditorGUILayout.Foldout(
                        m_SubMotionFoldouts[i],
                        $"SubMotion [{i}] - Target: [{sm.NodeIndex}] {targetNodeName} | Type: {sm.Type} | Keyframes: {kfCount}",
                        true
                    );

                    if (m_SubMotionFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Type:", sm.Type.ToString());
                        EditorGUILayout.LabelField("Interpolation Type:", sm.InterpolationType.ToString());
                        EditorGUILayout.LabelField("Target Node Index:", sm.NodeIndex.ToString());
                        EditorGUILayout.LabelField("Frame Range:", $"{sm.StartFrame:F2} to {sm.EndFrame:F2}");
                        EditorGUILayout.LabelField("Keyframe Range:", $"{sm.StartKeyframe:F2} to {sm.EndKeyframe:F2}");

                        if (sm.Keyframes != null && sm.Keyframes.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Keyframe Data:", EditorStyles.boldLabel);

                            int totalKeyframes = sm.Keyframes.Count;
                            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalKeyframes / ITEMS_PER_PAGE));
                            int currentPage = m_SubMotionPages[i];

                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("Prev Page", GUILayout.Width(80)) && currentPage > 0)
                            {
                                currentPage--;
                            }
                            EditorGUILayout.LabelField($"Page {currentPage + 1} / {totalPages} (Keyframes {currentPage * ITEMS_PER_PAGE} - {Mathf.Min(totalKeyframes, (currentPage + 1) * ITEMS_PER_PAGE) - 1})");
                            if (GUILayout.Button("Next Page", GUILayout.Width(80)) && currentPage < totalPages - 1)
                            {
                                currentPage++;
                            }
                            m_SubMotionPages[i] = currentPage;
                            EditorGUILayout.EndHorizontal();

                            int startIdx = currentPage * ITEMS_PER_PAGE;
                            int endIdx = Mathf.Min(totalKeyframes, (currentPage + 1) * ITEMS_PER_PAGE);

                            for (int kIdx = startIdx; kIdx < endIdx; kIdx++)
                            {
                                var kfObj = sm.Keyframes[kIdx];
                                if (kfObj == null) continue;

                                if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_VECTOR vKf)
                                {
                                    EditorGUILayout.Vector3Field($"Frame {vKf.Frame:F2}", vKf.Value);
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16 rKf)
                                {
                                    float degX = (float)((double)rKf.Value1 * (180.0 / 32768.0));
                                    float degY = (float)((double)rKf.Value2 * (180.0 / 32768.0));
                                    float degZ = (float)((double)rKf.Value3 * (180.0 / 32768.0));
                                    EditorGUILayout.LabelField($"Frame {rKf.Frame}: BAMS ({rKf.Value1}, {rKf.Value2}, {rKf.Value3}) -> Euler ({degX:F2}°, {degY:F2}°, {degZ:F2}°)");
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_FLOAT fKf)
                                {
                                    EditorGUILayout.FloatField($"Frame {fKf.Frame:F2}", fKf.Value);
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16Kf)
                                {
                                    EditorGUILayout.LabelField($"Frame {s16Kf.Frame}: {s16Kf.Value}");
                                }
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32Kf)
                                {
                                    EditorGUILayout.LabelField($"Frame {s32Kf.Frame:F2}: {s32Kf.Value}");
                                }
                                else
                                {
                                    EditorGUILayout.LabelField($"Keyframe [{kIdx}]: Unknown Keyframe Type ({kfObj.GetType().Name})");
                                }
                            }
                        }

                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion

        #region Camera, Light & Effects Tab
        private void DrawMiscTab()
        {
            var data = m_LoadedNinjaData.Data;

            if (data.Camera == null && data.Light == null && data.EffectList == null && data.NodeNameList == null)
            {
                EditorGUILayout.HelpBox("No Camera, Light, Effect or Node Name data present in this file.", MessageType.Info);
                return;
            }

            if (data.Camera != null)
            {
                EditorGUILayout.LabelField("Camera Data (NXCA)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Camera Type:", data.Camera.Type.ToString());
                EditorGUILayout.LabelField("Unknown UInt32 [1]:", data.Camera.UnknownUInt32_1.ToString());
                EditorGUILayout.LabelField("Unknown UInt32 [2]:", data.Camera.UnknownUInt32_2.ToString());
                EditorGUILayout.Vector3Field("Unknown Vector3 [1]", data.Camera.UnknownVector3_1);
                EditorGUILayout.Vector3Field("Unknown Vector3 [2]", data.Camera.UnknownVector3_2);
                EditorGUILayout.FloatField("Unknown Float [1]", data.Camera.UnknownFloat_1);
                EditorGUILayout.FloatField("Unknown Float [2]", data.Camera.UnknownFloat_2);
                EditorGUILayout.FloatField("Unknown Float [3]", data.Camera.UnknownFloat_3);
                EditorGUILayout.FloatField("Unknown Float [4]", data.Camera.UnknownFloat_4);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.Light != null)
            {
                EditorGUILayout.LabelField("Light Data (NXLI)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Light Type:", data.Light.Type.ToString());
                EditorGUILayout.LabelField("Unknown UInt32 [1]:", data.Light.UnknownUInt32_1.ToString());
                EditorGUILayout.Vector3Field("Unknown Vector3 [1]", data.Light.UnknownVector3_1);
                EditorGUILayout.Vector3Field("Unknown Vector3 [2]", data.Light.UnknownVector3_2);
                EditorGUILayout.Vector3Field("Unknown Vector3 [3]", data.Light.UnknownVector3_3);
                EditorGUILayout.FloatField("Unknown Float [1]", data.Light.UnknownFloat_1);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.EffectList != null)
            {
                EditorGUILayout.LabelField("Effect List (NXEF)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Effect List Type:", data.EffectList.Type.ToString());

                if (data.EffectList.NinjaEffectFiles != null && data.EffectList.NinjaEffectFiles.Count > 0)
                {
                    EditorGUILayout.LabelField("Effect Files:", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < data.EffectList.NinjaEffectFiles.Count; i++)
                    {
                        var ef = data.EffectList.NinjaEffectFiles[i];
                        if (ef == null) continue;
                        EditorGUILayout.LabelField($"[{i}] Type: {ef.Type} | File: {ef.FileName ?? ""}");
                    }
                }

                if (data.EffectList.NinjaTechniqueNames != null && data.EffectList.NinjaTechniqueNames.Count > 0)
                {
                    EditorGUILayout.LabelField("Technique Names:", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < data.EffectList.NinjaTechniqueNames.Count; i++)
                    {
                        var tn = data.EffectList.NinjaTechniqueNames[i];
                        if (tn == null) continue;
                        EditorGUILayout.LabelField($"[{i}] Type: {tn.Type} | Name: {tn.Name ?? ""}");
                    }
                }

                if (data.EffectList.NinjaTechniqueIndices != null && data.EffectList.NinjaTechniqueIndices.Count > 0)
                {
                    EditorGUILayout.LabelField("Technique Indices:", string.Join(", ", data.EffectList.NinjaTechniqueIndices));
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.NodeNameList != null && data.NodeNameList.NinjaNodeNames != null)
            {
                EditorGUILayout.LabelField("Node Name List (NXNN)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Sort Type:", data.NodeNameList.Type.ToString());
                EditorGUILayout.LabelField("Total Names:", data.NodeNameList.NinjaNodeNames.Count.ToString());

                for (int i = 0; i < data.NodeNameList.NinjaNodeNames.Count; i++)
                {
                    EditorGUILayout.LabelField($"[{i}]: {data.NodeNameList.NinjaNodeNames[i] ?? ""}");
                }
                EditorGUI.indentLevel--;
            }
        }
        #endregion
    }
}