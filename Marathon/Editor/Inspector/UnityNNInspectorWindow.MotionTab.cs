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
            EditorGUILayout.LabelField($"Frame Range: {mot.StartFrame:F2} - {mot.EndFrame:F2} | Framerate: {mot.Framerate:F2} FPS | Category: {CleanEnumString(mot.Type)}");

            if (mot.SubMotions != null)
            {
                for (int i = 0; i < mot.SubMotions.Count; i++)
                {
                    var sm = mot.SubMotions[i];
                    if (sm == null) continue;

                    if (!foldouts.ContainsKey(i)) foldouts[i] = false;
                    if (!pages.ContainsKey(i)) pages[i] = 0;

                    string formattedType = NinjaSubMotionTypeFormatter.FormatSubMotionType(sm.Type, mot.Type);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foldouts[i] = EditorGUILayout.Foldout(foldouts[i], $"SubMotion [{i}] Node [{sm.NodeIndex}] - {formattedType} (Keyframes: {sm.Keyframes?.Count ?? 0})", true);

                    if (foldouts[i] && sm.Keyframes != null)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Track Type Flags:", formattedType);
                        EditorGUILayout.LabelField("Raw Type Hex:", $"0x{(uint)sm.Type:X8}");
                        EditorGUILayout.LabelField("Interpolation Mode:", CleanEnumString(sm.InterpolationType));
                        EditorGUILayout.LabelField("Frame Range:", $"{sm.StartFrame:F2} - {sm.EndFrame:F2} | Keyframes Range: {sm.StartKeyframe:F2} - {sm.EndKeyframe:F2}");

                        int currentPage = pages[i];
                        DrawPaginationControls(ref currentPage, sm.Keyframes.Count, ITEMS_PER_PAGE);
                        pages[i] = currentPage;

                        int startIdx = currentPage * ITEMS_PER_PAGE;
                        int endIdx = Mathf.Min(sm.Keyframes.Count, (currentPage + 1) * ITEMS_PER_PAGE);

                        for (int kIdx = startIdx; kIdx < endIdx; kIdx++)
                        {
                            var kf = sm.Keyframes[kIdx];
                            if (kf is NinjaKeyframe.NNS_MOTION_KEY_VECTOR v) EditorGUILayout.LabelField($"Frame {v.Frame:F2}: Vector ({v.Value.x:F4}, {v.Value.y:F4}, {v.Value.z:F4})");
                            else if (kf is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16 r) EditorGUILayout.LabelField($"Frame {r.Frame}: BAMS ({r.Value1}, {r.Value2}, {r.Value3}) -> Deg ({BamsToDegrees(r.Value1):F2}°, {BamsToDegrees(r.Value2):F2}°, {BamsToDegrees(r.Value3):F2}°)");
                            else if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT f) EditorGUILayout.LabelField($"Frame {f.Frame:F2}: Float ({f.Value:F4})");
                            else if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32) EditorGUILayout.LabelField($"Frame {s32.Frame:F2}: Sint32 ({s32.Value}) -> Deg ({Bams32ToDegrees(s32.Value):F2}°)");
                            else if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16) EditorGUILayout.LabelField($"Frame {s16.Frame}: Sint16 ({s16.Value}) -> Deg ({BamsToDegrees(s16.Value):F2}°)");
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }

        private static float BamsToDegrees(int bamAngle) => (float)((double)bamAngle * (180.0 / 32768.0));
        private static float Bams32ToDegrees(int bam32Angle) => (float)((double)bam32Angle * (360.0 / 65536.0));
        #endregion
    }
}