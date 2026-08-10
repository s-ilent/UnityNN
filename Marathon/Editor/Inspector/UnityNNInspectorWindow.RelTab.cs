using UnityEngine;
using UnityEditor;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region REL Tab
        private void DrawRelTab()
        {
            if (!m_Context.IsRelAsset)
            {
                EditorGUILayout.HelpBox("Select a .rel file to view stage layout, lighting, or spawn data.", MessageType.Info);
                return;
            }

            object parsedData = m_Context.RelData;
            RelFileType relType = m_Context.RelType;

            EditorGUILayout.LabelField($"REL Stage Data ({relType})", EditorStyles.boldLabel);

            if (parsedData is SetFileData setFile)
            {
                EditorGUILayout.LabelField($"Area ID: {setFile.AreaID} | Total Maps: {setFile.MapData.Count}");
                foreach (var map in setFile.MapData)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Map {map.MapNumber} ({map.Headers.Count} Groups)", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    foreach (var header in map.Headers)
                    {
                        EditorGUILayout.LabelField($"Group {header.ListIndex}: {header.Objects.Count} Objects");
                        foreach (var obj in header.Objects)
                        {
                            string defName = SetObjectDefinitions.GetDefinitionName(obj.ObjID);
                            EditorGUILayout.LabelField($"  [{obj.ObjID:000}] {defName} at ({obj.Position.x:F1}, {obj.Position.y:F1}, {obj.Position.z:F1})");
                        }
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
            }
            else if (parsedData is LndEffectData effect)
            {
                EditorGUILayout.LabelField($"Fog Range: {effect.Fog.NearPlane:F1}m - {effect.Fog.FarPlane:F1}m");
                EditorGUILayout.ColorField("Fog Color", effect.Fog.FogColor);
                EditorGUILayout.ColorField("Ambient Light", effect.PlayerLightAmbient.LightColor);
                EditorGUILayout.ColorField("Player Light 1", effect.PlayerLight1.LightColor);
            }
            else if (parsedData is EnemyLayoutData enemyLayout)
            {
                EditorGUILayout.LabelField($"Total Spawn Waves: {enemyLayout.Spawns.Count}");
                for (int i = 0; i < enemyLayout.Spawns.Count; i++)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Spawn Wave [{i}] - {enemyLayout.Spawns[i].Count} Monsters", EditorStyles.boldLabel);
                    foreach (var m in enemyLayout.Spawns[i])
                    {
                        EditorGUILayout.LabelField($"  Monster [{m.MonsterNum:000}] Count: {m.Count} | Level Mod: {m.LevelModifier}");
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }
        #endregion
    }
}