// File: Marathon/Editor/Inspector/UnityNNInspectorWindow.RelTab.cs
using UnityNN;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace UnityNN.Editor
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
                int triCount = colData.Triangles?.Count ?? 0;
                EditorGUILayout.LabelField($"Collision Mesh Geometry (Vertices: {colData.Vertices.Count}, Triangles: {triCount})", EditorStyles.boldLabel);

                if (colData.BoundingBoxMin.HasValue && colData.BoundingBoxMax.HasValue)
                {
                    EditorGUILayout.LabelField($"Bounds Extents: Min ({colData.BoundingBoxMin.Value.x:F2}, {colData.BoundingBoxMin.Value.y:F2}, {colData.BoundingBoxMin.Value.z:F2}) to Max ({colData.BoundingBoxMax.Value.x:F2}, {colData.BoundingBoxMax.Value.y:F2}, {colData.BoundingBoxMax.Value.z:F2})");
                }
                EditorGUILayout.Space();

                for (int i = 0; i < Mathf.Min(triCount, 25); i++)
                {
                    var tri = colData.Triangles[i];
                    string adjStr = $"[{tri.Adjacency0:X4}, {tri.Adjacency1:X4}, {tri.Adjacency2:X4}]";
                    EditorGUILayout.LabelField($"  Triangle [{i:000}]: ({tri.VertexIndex0}, {tri.VertexIndex1}, {tri.VertexIndex2}) | Mat ID: {tri.MaterialID} | Adjacency: {adjStr}");
                }
                if (triCount > 25)
                {
                    EditorGUILayout.LabelField($"  ... and {triCount - 25} more triangles.");
                }
            }
            else if (parsedData is LndEffectData effect)
            {
                EditorGUILayout.LabelField("Environment, Fog & Lighting (LndEffect)", EditorStyles.boldLabel);

                // 1. Fog
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Fog Parameters:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Near Plane: {effect.Fog.NearPlane:F4}");
                EditorGUILayout.LabelField($"  Far Plane: {effect.Fog.FarPlane:F4}");
                EditorGUILayout.LabelField($"  Initial Intensity: {effect.Fog.InitialIntensity:F4}");
                EditorGUILayout.LabelField($"  Ramp Up: {effect.Fog.RampUp:F4}");
                EditorGUILayout.ColorField("  Fog Color", effect.Fog.FogColor);
                EditorGUILayout.EndVertical();

                // 2. Lights
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Light Definitions:", EditorStyles.boldLabel);
                if (effect.PlayerLight1 != null)
                {
                    EditorGUILayout.LabelField("  Player Light 1:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Vector3Field("    Direction", effect.PlayerLight1.Direction);
                    EditorGUILayout.ColorField("    Light Color", effect.PlayerLight1.LightColor);
                }
                if (effect.PlayerLight2 != null)
                {
                    EditorGUILayout.LabelField("  Player Light 2:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Vector3Field("    Direction", effect.PlayerLight2.Direction);
                    EditorGUILayout.ColorField("    Light Color", effect.PlayerLight2.LightColor);
                }
                if (effect.PlayerLightAmbient != null)
                {
                    EditorGUILayout.LabelField("  Player Light Ambient:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Vector3Field("    Direction", effect.PlayerLightAmbient.Direction);
                    EditorGUILayout.ColorField("    Light Color", effect.PlayerLightAmbient.LightColor);
                }
                EditorGUILayout.EndVertical();

                // 3. Sun
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Sun Parameters:", EditorStyles.boldLabel);
                EditorGUILayout.Vector3Field("  Sun Position", effect.SunPosition);
                EditorGUILayout.LabelField($"  Sun Unknown: {effect.SunUnknown:F4}");
                EditorGUILayout.EndVertical();

                // 4. Gradients
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Gradient Definitions:", EditorStyles.boldLabel);
                if (effect.TopGradient != null)
                {
                    EditorGUILayout.LabelField("  Top Gradient:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"    Start Height: {effect.TopGradient.StartHeight:F4}");
                    EditorGUILayout.LabelField($"    End Height: {effect.TopGradient.EndHeight:F4}");
                    EditorGUILayout.ColorField("    Start Color", effect.TopGradient.StartColor);
                    EditorGUILayout.ColorField("    End Color", effect.TopGradient.EndColor);
                    EditorGUILayout.LabelField($"    Gradient Multiplier: {effect.TopGradient.GradientMultiplier:F4}");
                    EditorGUILayout.LabelField($"    Destination Multiplier: {effect.TopGradient.DestinationMultiplier:F4}");
                }
                if (effect.BottomGradient != null)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("  Bottom Gradient:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"    Start Height: {effect.BottomGradient.StartHeight:F4}");
                    EditorGUILayout.LabelField($"    End Height: {effect.BottomGradient.EndHeight:F4}");
                    EditorGUILayout.ColorField("    Start Color", effect.BottomGradient.StartColor);
                    EditorGUILayout.ColorField("    End Color", effect.BottomGradient.EndColor);
                    EditorGUILayout.LabelField($"    Gradient Multiplier: {effect.BottomGradient.GradientMultiplier:F4}");
                    EditorGUILayout.LabelField($"    Destination Multiplier: {effect.BottomGradient.DestinationMultiplier:F4}");
                }
                EditorGUILayout.EndVertical();

                // 5. Blur
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Blur Parameters:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Blur Start Distance: {effect.BlurStartDistance:F4}");
                EditorGUILayout.LabelField($"  Blur Unknown: {effect.BlurUnknown:F4}");
                EditorGUILayout.LabelField($"  Blur Pixel Count: {effect.BlurPixelCount}");
                EditorGUILayout.LabelField($"  Blur Distance: {effect.BlurDistance:F4}");
                EditorGUILayout.LabelField($"  Blur Opacity: {effect.BlurOpacity:F4}");
                EditorGUILayout.EndVertical();
            }
            else if (parsedData is LndEnemyLightData enemyLight)
            {
                EditorGUILayout.LabelField("Enemy Light Definitions (LndEnemyLight)", EditorStyles.boldLabel);
                if (enemyLight.Light1 != null)
                {
                    EditorGUILayout.LabelField("  Light 1:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Vector3Field("    Direction", enemyLight.Light1.Direction);
                    EditorGUILayout.ColorField("    Light Color", enemyLight.Light1.LightColor);
                }
                if (enemyLight.Light2 != null)
                {
                    EditorGUILayout.LabelField("  Light 2:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Vector3Field("    Direction", enemyLight.Light2.Direction);
                    EditorGUILayout.ColorField("    Light Color", enemyLight.Light2.LightColor);
                }
                if (enemyLight.LightAmbient != null)
                {
                    EditorGUILayout.LabelField("  Light Ambient:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Vector3Field("    Direction", enemyLight.LightAmbient.Direction);
                    EditorGUILayout.ColorField("    Light Color", enemyLight.LightAmbient.LightColor);
                }
            }
            else if (parsedData is List<LndFogData> fogs)
            {
                EditorGUILayout.LabelField($"Fog Bank ({fogs.Count} Presets)", EditorStyles.boldLabel);
                for (int i = 0; i < fogs.Count; i++)
                {
                    var fog = fogs[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"  Fog Preset [{i:00}]", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"    Near Plane: {fog.NearPlane:F4}");
                    EditorGUILayout.LabelField($"    Far Plane: {fog.FarPlane:F4}");
                    EditorGUILayout.LabelField($"    Initial Intensity: {fog.InitialIntensity:F4}");
                    EditorGUILayout.LabelField($"    Ramp Up: {fog.RampUp:F4}");
                    EditorGUILayout.ColorField("    Fog Color", fog.FogColor);
                    EditorGUILayout.EndVertical();
                }
            }
            else if (parsedData is LndCommonData common)
            {
                EditorGUILayout.LabelField("Map Scene Links (LndCommon)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  NBL Filename Fragment: {common.NblFilenameFragment}");
                EditorGUILayout.LabelField($"  XNT Filename Fragment 1: {common.XntFilenameFragment1}");
                EditorGUILayout.LabelField($"  XNT Filename Fragment 2: {common.XntFilenameFragment2}");
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
            else if (parsedData is ObjectParamData paramData)
            {
                EditorGUILayout.LabelField($"Object Definitions ({paramData.ObjectDefinitions.Count} Objects)", EditorStyles.boldLabel);
                foreach (var kvp in paramData.ObjectDefinitions)
                {
                    int objId = kvp.Key;
                    var obj = kvp.Value;
                    string defName = SetObjectDefinitions.GetDefinitionName(objId);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"[{objId:000}] {defName}", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    if (obj.Hitbox != null)
                    {
                        EditorGUILayout.LabelField("Hitbox:", $"Shape: {obj.Hitbox.HitboxShape} | Size: ({obj.Hitbox.UnknownFloat2:F1}, {obj.Hitbox.UnknownFloat3:F1}, {obj.Hitbox.UnknownFloat4:F1}) | Radius: {obj.Hitbox.UnknownFloat6:F1}");
                    }

                    if (obj.Models.Count > 0)
                    {
                        EditorGUILayout.LabelField("Models:", string.Join(", ", obj.Models.ConvertAll(m => $"{m.FileName} (ID:{m.Id})")));
                    }

                    if (obj.Animations.Count > 0)
                    {
                        EditorGUILayout.LabelField("Animations:", $"{obj.Animations.Count} Tracks");
                        for (int a = 0; a < obj.Animations.Count; a++)
                        {
                            var anim = obj.Animations[a];
                            EditorGUILayout.LabelField($"  [{a:00}] Bone: {anim.BoneAnimName} | Tex: {anim.TexAnimName} (ID1: {anim.UnknownIdentifier1}, ID2: {anim.UnknownIdentifier2})");
                        }
                    }

                    if (obj.ParticleSoundReferences != null)
                    {
                        foreach (var pb in obj.ParticleSoundReferences.ParticleBindings)
                            EditorGUILayout.LabelField($"  [Particle Event] {pb.ParticleName} -> {pb.EventName}");
                        foreach (var sb in obj.ParticleSoundReferences.SoundBindings)
                            EditorGUILayout.LabelField($"  [Sound Event] ID {sb.SoundId} -> {sb.EventName}");
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
            }
            else if (parsedData is ObjectParticleInfoData particleData)
            {
                EnsureStyles();
                EditorGUILayout.LabelField($"Particle Effects Table ({particleData.Entries.Count} Presets)", EditorStyles.boldLabel);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("Index", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label("Particle Name", EditorStyles.miniBoldLabel, GUILayout.Width(200));
                GUILayout.Label("Payload File (.dat)", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label("Param Float", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < particleData.Entries.Count; i++)
                {
                    var entry = particleData.Entries[i];
                    GUIStyle rowBg = (i % 2 == 0) ? evenStyle : oddStyle;
                    EditorGUILayout.BeginHorizontal(rowBg, GUILayout.Height(18));
                    GUILayout.Label($"[{entry.ParticleIndex:000}]", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                    GUILayout.Label(entry.ParticleName, EditorStyles.label, GUILayout.Width(200));
                    GUILayout.Label(entry.ParticleFileName, EditorStyles.label, GUILayout.ExpandWidth(true));
                    GUILayout.Label($"{entry.MysteryFloat:F1}", EditorStyles.miniLabel, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }
        #endregion
    }
}