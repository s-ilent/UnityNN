using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Motion Tab
        private void DrawMotionTab()
        {
            if (!m_Context.IsNinjaAsset)
            {
                EditorGUILayout.HelpBox("Select a Ninja asset to view Motion & Animation.", MessageType.Info);
                return;
            }

            var data = m_LoadedNinjaData.Data;
            if (data.Motion == null && data.MaterialMotion == null)
            {
                EditorGUILayout.HelpBox("No Node or Material Motion data present in this file.", MessageType.Info);
                return;
            }

            if (data.Motion != null)
            {
                DrawSingleMotionSection("Node Motion Information", data.Motion, m_SubMotionFoldouts, m_SubMotionPages);
            }

            if (data.MaterialMotion != null)
            {
                EditorGUILayout.Space();
                DrawSingleMotionSection("Material Motion Information", data.MaterialMotion, m_MatSubMotionFoldouts, m_MatSubMotionPages);
            }
        }

        private void DrawSingleMotionSection(
            string headerTitle,
            NinjaMotion mot,
            Dictionary<int, bool> foldouts,
            Dictionary<int, int> pages)
        {
            EditorGUILayout.LabelField(headerTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Chunk ID: {mot.ChunkID ?? "N/A"} | Motion Type: {mot.Type}");
            EditorGUILayout.LabelField($"Frame Range: {mot.StartFrame:F2} - {mot.EndFrame:F2} | Framerate: {mot.Framerate:F2} FPS");

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

                    if (!foldouts.ContainsKey(i)) foldouts[i] = false;
                    if (!pages.ContainsKey(i)) pages[i] = 0;

                    string targetNodeName = $"Node_{sm.NodeIndex:0000}";
                    if (nodes != null && sm.NodeIndex >= 0 && sm.NodeIndex < nodes.Count && !string.IsNullOrEmpty(nodes[sm.NodeIndex].Name))
                        targetNodeName = nodes[sm.NodeIndex].Name;
                    else if (nodeNames != null && sm.NodeIndex >= 0 && sm.NodeIndex < nodeNames.Count)
                        targetNodeName = nodeNames[sm.NodeIndex];

                    int kfCount = sm.Keyframes != null ? sm.Keyframes.Count : 0;
                    string formattedRawType = NinjaSubMotionTypeFormatter.FormatSubMotionType(sm.Type, mot.Type);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foldouts[i] = EditorGUILayout.Foldout(
                        foldouts[i],
                        $"SubMotion [{i}] - Target: [{sm.NodeIndex}] {targetNodeName} | Raw Type: 0x{(uint)sm.Type:X8} ({formattedRawType}) | Keyframes: {kfCount}",
                        true
                    );

                    if (foldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Raw SubMotion Type:", formattedRawType);
                        EditorGUILayout.LabelField("Target Node Index:", sm.NodeIndex.ToString());
                        EditorGUILayout.LabelField("Frame Range:", $"{sm.StartFrame:F2} to {sm.EndFrame:F2}");

                        if (sm.Keyframes != null && sm.Keyframes.Count > 0)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Raw Keyframe Data:", EditorStyles.boldLabel);

                            int currentPage = pages[i];
                            DrawPaginationControls(ref currentPage, sm.Keyframes.Count, ITEMS_PER_PAGE);
                            pages[i] = currentPage;

                            int startIdx = currentPage * ITEMS_PER_PAGE;
                            int endIdx = Mathf.Min(sm.Keyframes.Count, (currentPage + 1) * ITEMS_PER_PAGE);

                            for (int kIdx = startIdx; kIdx < endIdx; kIdx++)
                            {
                                var kfObj = sm.Keyframes[kIdx];
                                if (kfObj == null) continue;

                                if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_VECTOR vKf)
                                    EditorGUILayout.LabelField($"Frame {vKf.Frame:F2}: Vector ({vKf.Value.x}, {vKf.Value.y}, {vKf.Value.z})");
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16 rKf)
                                    EditorGUILayout.LabelField($"Frame {rKf.Frame}: BAMS Short3 ({rKf.Value1}, {rKf.Value2}, {rKf.Value3})");
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_FLOAT fKf)
                                    EditorGUILayout.LabelField($"Frame {fKf.Frame:F2}: Float ({fKf.Value})");
                                else if (kfObj is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16Kf)
                                    EditorGUILayout.LabelField($"Frame {s16Kf.Frame}: Short ({s16Kf.Value})");
                            }
                        }

                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion
    }
}