using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        #region REL Tab
        private void DrawRelTab()
        {
            if (!m_Context.IsRelAsset) return;

            object parsedData = m_Context.RelData;
            RelFileType relType = m_Context.RelType;

            if (parsedData is FileListData fileListData)
            {
                int totalFiles = 0;
                foreach (var c in fileListData.Categories) totalFiles += c.FileNames.Count;
                EditorGUILayout.LabelField($"File List ({fileListData.Categories.Count} Categories, {totalFiles} Files)", EditorStyles.boldLabel);

                foreach (var cat in fileListData.Categories)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Category [{cat.CategoryIndex:02d}] ({cat.FileNames.Count} Files)", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < cat.FileNames.Count; i++)
                    {
                        EditorGUILayout.LabelField($"  [{i:000}] {cat.FileNames[i]}");
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
            }
            else if (parsedData is SetFileData setFile)
            {
                EditorGUILayout.LabelField($"Stage Objects Layout (Area ID: {setFile.AreaID}, Maps: {setFile.MapData.Count})", EditorStyles.boldLabel);
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
            else if (parsedData is CollisionMeshData colData)
            {
                EditorGUILayout.LabelField($"Collision Mesh Geometry (Vertices: {colData.Vertices.Count}, Polygons: {colData.Polygons.Count})", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                for (int i = 0; i < Mathf.Min(colData.Polygons.Count, 15); i++)
                {
                    var poly = colData.Polygons[i];
                    EditorGUILayout.LabelField($"  Polygon [{i}]: Indices ({poly.VertexIndices[0]}, {poly.VertexIndices[1]}, {poly.VertexIndices[2]}, {poly.VertexIndices[3]}) | Flags: 0x{poly.Flags:X8}");
                }
                if (colData.Polygons.Count > 15)
                {
                    EditorGUILayout.LabelField($"  ... and {colData.Polygons.Count - 15} more polygons.");
                }
            }
            else if (parsedData is LndEffectData effect)
            {
                EditorGUILayout.LabelField("Environment & Lighting", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Fog Range: {effect.Fog.NearPlane:F1}m - {effect.Fog.FarPlane:F1}m");
                EditorGUILayout.ColorField("Fog Color", effect.Fog.FogColor);
                EditorGUILayout.ColorField("Ambient Light", effect.PlayerLightAmbient.LightColor);
                EditorGUILayout.ColorField("Player Light 1", effect.PlayerLight1.LightColor);
            }
            else if (parsedData is List<LndFogData> fogs)
            {
                EditorGUILayout.LabelField($"Fog Bank ({fogs.Count} Presets)", EditorStyles.boldLabel);
                for (int i = 0; i < fogs.Count; i++)
                {
                    var fog = fogs[i];
                    EditorGUILayout.LabelField($"  Fog [{i}]: Range {fog.NearPlane:F1}m - {fog.FarPlane:F1}m | Color: {fog.FogColor}");
                }
            }
            else if (parsedData is LndCommonData common)
            {
                EditorGUILayout.LabelField("Map Scene Links (LndCommon)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Linked NBL Filename Fragment: {common.NblFilenameFragment}");
                EditorGUILayout.LabelField($"  Linked XNT Fragment 1: {common.XntFilenameFragment1}");
                EditorGUILayout.LabelField($"  Linked XNT Fragment 2: {common.XntFilenameFragment2}");
                EditorGUILayout.LabelField($"  Unknown Float: {common.UnknownFloat}");
            }
            else if (parsedData is EnemyLayoutData enemyLayout)
            {
                EditorGUILayout.LabelField($"Enemy Spawns ({enemyLayout.Spawns.Count} Waves)", EditorStyles.boldLabel);
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
            else if (parsedData is List<QuestListingData> questList)
            {
                EditorGUILayout.LabelField($"Quest Listing ({questList.Count} Quests)", EditorStyles.boldLabel);
                foreach (var q in questList)
                {
                    EditorGUILayout.LabelField($"  Quest [{q.QuestNumber:000}]: {q.FileName}");
                }
            }
            else if (parsedData is StageBlockRouteData routeData)
            {
                EditorGUILayout.LabelField($"Stage Route & Block Data ({routeData.Offsets.Count} Entries)", EditorStyles.boldLabel);
                for (int i = 0; i < Mathf.Min(routeData.Offsets.Count, 20); i++)
                {
                    EditorGUILayout.LabelField($"  Route [{i}]: Offset 0x{routeData.Offsets[i]:X8}");
                }
            }
        }
        #endregion
    }
}