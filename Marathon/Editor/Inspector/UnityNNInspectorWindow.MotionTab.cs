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
            if (!m_Context.IsNinjaAsset) return;

            var data = m_Context.NinjaData.Data;
            if (data.Motion != null) DrawSingleMotionSection("Node Motion Information", data.Motion, m_SubMotionFoldouts, m_SubMotionPages);
            if (data.MaterialMotion != null) DrawSingleMotionSection("Material Motion Information", data.MaterialMotion, m_MatSubMotionFoldouts, m_MatSubMotionPages);
        }

        private void DrawSingleMotionSection(string title, NinjaMotion mot, Dictionary<int, bool> foldouts, Dictionary<int, int> pages)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Frame Range: {mot.StartFrame:F2} - {mot.EndFrame:F2} | Framerate: {mot.Framerate:F2} FPS");

            if (mot.SubMotions != null)
            {
                for (int i = 0; i < mot.SubMotions.Count; i++)
                {
                    var sm = mot.SubMotions[i];
                    if (sm == null) continue;

                    if (!foldouts.ContainsKey(i)) foldouts[i] = false;
                    if (!pages.ContainsKey(i)) pages[i] = 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foldouts[i] = EditorGUILayout.Foldout(foldouts[i], $"SubMotion [{i}] Target Node [{sm.NodeIndex}] - Keyframes: {sm.Keyframes?.Count ?? 0}", true);

                    if (foldouts[i] && sm.Keyframes != null)
                    {
                        EditorGUI.indentLevel++;
                        DrawCleanFlagsLabel(sm.Type, "SubMotion Track Flags:");

                        int currentPage = pages[i];
                        DrawPaginationControls(ref currentPage, sm.Keyframes.Count, ITEMS_PER_PAGE);
                        pages[i] = currentPage;

                        int startIdx = currentPage * ITEMS_PER_PAGE;
                        int endIdx = Mathf.Min(sm.Keyframes.Count, (currentPage + 1) * ITEMS_PER_PAGE);

                        for (int kIdx = startIdx; kIdx < endIdx; kIdx++)
                        {
                            var kf = sm.Keyframes[kIdx];
                            if (kf is NinjaKeyframe.NNS_MOTION_KEY_VECTOR v) EditorGUILayout.LabelField($"Frame {v.Frame:F2}: Vector ({v.Value.x}, {v.Value.y}, {v.Value.z})");
                            else if (kf is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16 r) EditorGUILayout.LabelField($"Frame {r.Frame}: BAMS ({r.Value1}, {r.Value2}, {r.Value3})");
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