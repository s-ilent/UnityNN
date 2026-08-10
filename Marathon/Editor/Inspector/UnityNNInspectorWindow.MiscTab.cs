using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region Camera, Light & Effects Tab
        private void DrawMiscTab()
        {
            if (!m_Context.IsNinjaAsset)
            {
                EditorGUILayout.HelpBox("Select a Ninja asset to view Camera, Light & Effects.", MessageType.Info);
                return;
            }

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
                EditorGUILayout.Vector3Field("Vector [1]", data.Camera.UnknownVector3_1);
                EditorGUILayout.Vector3Field("Vector [2]", data.Camera.UnknownVector3_2);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.Light != null)
            {
                EditorGUILayout.LabelField("Light Data (NXLI)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Light Type:", data.Light.Type.ToString());
                EditorGUILayout.Vector3Field("Vector [1]", data.Light.UnknownVector3_1);
                EditorGUILayout.Vector3Field("Vector [2]", data.Light.UnknownVector3_2);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.EffectList != null)
            {
                EditorGUILayout.LabelField("Effect List (NXEF)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                if (data.EffectList.NinjaEffectFiles != null)
                {
                    for (int i = 0; i < data.EffectList.NinjaEffectFiles.Count; i++)
                    {
                        var ef = data.EffectList.NinjaEffectFiles[i];
                        if (ef != null) EditorGUILayout.LabelField($"[{i}] Type: {ef.Type} | File: {ef.FileName ?? ""}");
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            if (data.NodeNameList != null && data.NodeNameList.NinjaNodeNames != null)
            {
                EditorGUILayout.LabelField("Node Name List (NXNN)", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
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