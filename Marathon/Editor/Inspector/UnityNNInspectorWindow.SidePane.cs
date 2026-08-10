using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        private void DrawOverviewSidePane()
        {
            EditorGUILayout.LabelField("Asset Overview", EditorStyles.boldLabel);

            if (m_Context.IsNinjaAsset)
            {
                var data = m_Context.NinjaData.Data;

                EditorGUILayout.LabelField("Chunks Present:", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Model (NXOB):", data.Object != null ? $"Yes ({data.Object.Nodes.Count} nodes)" : "No");
                EditorGUILayout.LabelField("Motion (NXMA):", data.Motion != null ? $"Yes ({data.Motion.Framerate} FPS)" : "No");
                EditorGUILayout.LabelField("MatMotion (NXNV):", data.MaterialMotion != null ? $"Yes" : "No");
                EditorGUILayout.LabelField("TexList (NXTL):", data.TextureList != null ? $"Yes ({data.TextureList.NinjaTextureFiles.Count} tex)" : "No");
                EditorGUILayout.LabelField("NodeNames (NXNN):", data.NodeNameList != null ? $"Yes ({data.NodeNameList.NinjaNodeNames.Count} names)" : "No");

                if (data.Object != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Object Stats:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("SubObjects:", data.Object.SubObjects?.Count.ToString() ?? "0");
                    EditorGUILayout.LabelField("Materials:", data.Object.Materials?.Count.ToString() ?? "0");
                    EditorGUILayout.LabelField("Vertex Lists:", data.Object.VertexLists?.Count.ToString() ?? "0");
                    EditorGUILayout.LabelField("Primitive Lists:", data.Object.PrimitiveLists?.Count.ToString() ?? "0");
                    EditorGUILayout.LabelField("Center:", data.Object.Center.ToString("F2"));
                    EditorGUILayout.LabelField("Radius:", data.Object.Radius.ToString("F2"));
                }
            }
            else if (m_Context.IsRelAsset)
            {
                EditorGUILayout.LabelField("REL File Type:", m_Context.RelType.ToString());

                if (m_Context.RelData is SetFileData setFile)
                {
                    EditorGUILayout.LabelField("Area ID:", setFile.AreaID.ToString());
                    EditorGUILayout.LabelField("Maps:", setFile.MapData.Count.ToString());
                }
                else if (m_Context.RelData is LndEffectData effect)
                {
                    EditorGUILayout.LabelField("Fog Near:", $"{effect.Fog.NearPlane:F1}m");
                    EditorGUILayout.LabelField("Fog Far:", $"{effect.Fog.FarPlane:F1}m");
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Dump Category JSON"))
            {
                DumpCurrentCategoryJson();
            }
        }

        private void DumpCurrentCategoryJson()
        {
            if (m_Context.IsNinjaAsset)
            {
                var data = m_Context.NinjaData.Data;
                switch (m_SelectedTab)
                {
                    case 0: DumpCategoryJson(data.Object?.Nodes); break;
                    case 1: DumpCategoryJson(data.Object?.SubObjects); break;
                    case 2: DumpCategoryJson(data.Object?.Materials); break;
                    case 3: DumpCategoryJson(data.Motion); break;
                    case 4: DumpCategoryJson(data.Camera); break;
                }
            }
            else if (m_Context.IsRelAsset)
            {
                DumpCategoryJson(m_Context.RelData);
            }
        }

        private void DumpCategoryJson(object categoryObj)
        {
            m_DumpedJsonText = NinjaJsonSerializer.Serialize(categoryObj);
            GUIUtility.systemCopyBuffer = m_DumpedJsonText;
            m_ShowJsonOutput = true;
        }
    }
}