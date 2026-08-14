using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Persistent Overview Pane (Right Side)
        private void DrawPersistentOverviewTableRightPane()
        {
            EditorGUILayout.LabelField("Asset Metrics & Summary", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(m_Context.SourceSceneObjectName))
            {
                EditorGUILayout.LabelField($"Scene Target: {m_Context.SourceSceneObjectName}", EditorStyles.boldLabel);
            }
            if (!string.IsNullOrEmpty(m_Context.AssetPath))
            {
                EditorGUILayout.LabelField(Path.GetFileName(m_Context.AssetPath), EditorStyles.label);
            }

            EditorGUILayout.Space(4);

            if (m_Context.IsNinjaAsset)
            {
                var data = m_Context.NinjaData.Data;

                if (m_Context.ObjectSource.IsPresent)
                {
                    DrawMetricRow("Model Nodes", $"{data.Object.Nodes.Count}");
                    DrawMetricRow("SubObjects", $"{data.Object.SubObjects.Count}");
                    DrawMetricRow("Materials", $"{data.Object.Materials.Count}");
                    DrawMetricRow("Bounding Radius", $"{data.Object.Radius:F2}m");
                }

                if (m_Context.NodeMotionSource.IsPresent)
                {
                    DrawMetricRow("Node Motion FPS", $"{data.Motion.Framerate:F0} FPS");
                    DrawMetricRow("Motion Tracks", $"{data.Motion.SubMotions.Count}");
                    DrawMetricRow("Frame Range", $"{data.Motion.StartFrame:F0} - {data.Motion.EndFrame:F0}");
                }

                if (m_Context.MaterialMotionSource.IsPresent)
                {
                    DrawMetricRow("Mat Motion FPS", $"{data.MaterialMotion.Framerate:F0} FPS");
                    DrawMetricRow("Mat Tracks", $"{data.MaterialMotion.SubMotions.Count}");
                }

                if (m_Context.TextureListSource.IsPresent)
                {
                    DrawMetricRow("Textures Defined", $"{data.TextureList.NinjaTextureFiles.Count}");
                }

                if (m_Context.NodeNameListSource.IsPresent)
                {
                    DrawMetricRow("Node Name Strings", $"{data.NodeNameList.NinjaNodeNames.Count}");
                }

                if (data.EffectList != null)
                {
                    DrawMetricRow("Effect Files", $"{data.EffectList.NinjaEffectFiles.Count}");
                }

                if (data.Camera != null)
                {
                    DrawMetricRow("Camera Type", $"{data.Camera.Type}");
                }

                if (data.Light != null)
                {
                    DrawMetricRow("Light Type", $"{data.Light.Type}");
                }
            }

            if (m_Context.IsRelAsset)
            {
                if (m_Context.RelData is SetFileData setFile)
                {
                    DrawMetricRow("REL Type", "Stage Layout");
                    DrawMetricRow("Area ID", $"{setFile.AreaID}");
                    DrawMetricRow("Stage Maps", $"{setFile.MapData.Count}");
                }
                else if (m_Context.RelData is CollisionMeshData colData)
                {
                    DrawMetricRow("REL Type", "Collision Geometry");
                    DrawMetricRow("Vertices", $"{colData.Vertices.Count}");
                    DrawMetricRow("Polygons", $"{colData.Polygons.Count}");
                }
                else if (m_Context.RelData is LndEffectData effect)
                {
                    DrawMetricRow("REL Type", "Lighting & Fog");
                    DrawMetricRow("Fog Near", $"{effect.Fog.NearPlane:F1}m");
                    DrawMetricRow("Fog Far", $"{effect.Fog.FarPlane:F1}m");
                }
                else if (m_Context.RelData is EnemyLayoutData enemyData)
                {
                    DrawMetricRow("REL Type", "Enemy Spawns");
                    DrawMetricRow("Spawn Waves", $"{enemyData.Spawns.Count}");
                }
                else if (m_Context.RelData is StageBlockRouteData routeData)
                {
                    DrawMetricRow("REL Type", "Stage Route/Block");
                    DrawMetricRow("Route Entries", $"{routeData.Offsets.Count}");
                }
                else if (m_Context.RelData is List<LndFogData> fogs)
                {
                    DrawMetricRow("REL Type", "Fog Bank");
                    DrawMetricRow("Fog Presets", $"{fogs.Count}");
                }
                else if (m_Context.RelData is LndCommonData common)
                {
                    DrawMetricRow("REL Type", "Map Scene Link");
                    DrawMetricRow("NBL Fragment", $"{common.NblFilenameFragment}");
                }
                else if (m_Context.RelData is List<QuestListingData> qList)
                {
                    DrawMetricRow("REL Type", "Quest List");
                    DrawMetricRow("Quests", $"{qList.Count}");
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Dump Category JSON", GUILayout.Height(26)))
            {
                DumpCurrentCategoryJson();
            }
        }

        private void DrawMetricRow(string metricLabel, string metricValue)
        {
            EditorGUILayout.BeginHorizontal();

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
            EditorGUILayout.LabelField(metricLabel, labelStyle, GUILayout.Width(110));

            GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            EditorGUILayout.LabelField(metricValue, valueStyle, GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();
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
        #endregion
    }
}