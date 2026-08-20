using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Marathon.Formats.Particle;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        private Dictionary<int, bool> m_TypdFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_EmitterFoldouts = new Dictionary<int, bool>();
        private int m_TypdSubKeyframePage = 0;

        private void DrawParticleTab()
        {
            if (!m_Context.IsParticleAsset) return;

            EnsureStyles();
            var pData = m_Context.ParticleEffectData;

            switch (m_SelectedTab)
            {
                case 0:
                    DrawParticleEmittersSection(pData);
                    break;
                case 1:
                    DrawParticleTypdBehaviorsSection(pData);
                    break;
                case 2:
                    DrawParticleSequenceCuesSection(pData);
                    break;
            }
        }

        #region Section 1: Emitters & Generators
        private void DrawParticleEmittersSection(ParticleEffectFile pData)
        {
            EditorGUILayout.LabelField($"Particle Generators & Emitters ({pData.Emitters.Count})", EditorStyles.boldLabel);

            if (pData.ResourceFiles.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Referenced Resources ({pData.ResourceFiles.Count}):", EditorStyles.boldLabel);
                for (int r = 0; r < pData.ResourceFiles.Count; r++)
                {
                    EditorGUILayout.LabelField($"  [{r:00}] {pData.ResourceFiles[r]}");
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            for (int i = 0; i < pData.Emitters.Count; i++)
            {
                var em = pData.Emitters[i];
                if (!m_EmitterFoldouts.ContainsKey(i)) m_EmitterFoldouts[i] = true;

                string resName = (em.ResourceIndex >= 0 && em.ResourceIndex < pData.ResourceFiles.Count)
                    ? pData.ResourceFiles[em.ResourceIndex] : "None";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                m_EmitterFoldouts[i] = EditorGUILayout.Foldout(m_EmitterFoldouts[i], $"Emitter [{i:00}] - {em.Type} ({resName})", true);

                if (m_EmitterFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Emitter Type:", $"{em.Type}");
                    EditorGUILayout.LabelField("Resource File:", resName);
                    EditorGUILayout.LabelField("Flags:", $"0x{em.Flags:X2}");

                    if (em.Type == EmitterType.Sprite && em.SpriteSubRecords.Count > 0)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField($"Sprite Sub-Emitters ({em.SpriteSubRecords.Count}):", EditorStyles.boldLabel);
                        for (int s = 0; s < em.SpriteSubRecords.Count; s++)
                        {
                            var sub = em.SpriteSubRecords[s];
                            EditorGUILayout.LabelField($"  [{s}] ID: {sub.SubEmitterId} | Blend: {sub.BlendMode} | Size: {sub.Size:F2} | Flags: 0x{sub.Flags:X2}");
                        }
                    }
                    else if (em.Type == EmitterType.Mesh)
                    {
                        EditorGUILayout.Space(2);
                        if (em.CoreFileIndices.Count > 0)
                        {
                            List<string> coreNames = new List<string>();
                            foreach (int c in em.CoreFileIndices)
                                coreNames.Add(c >= 0 && c < pData.ResourceFiles.Count ? pData.ResourceFiles[c] : $"Res_{c}");
                            EditorGUILayout.LabelField("Core Files:", string.Join(", ", coreNames));
                        }
                        if (em.AnimationIndices.Count > 0)
                        {
                            List<string> animNames = new List<string>();
                            foreach (int a in em.AnimationIndices)
                                animNames.Add(a >= 0 && a < pData.ResourceFiles.Count ? pData.ResourceFiles[a] : $"Res_{a}");
                            EditorGUILayout.LabelField("Animations:", string.Join(", ", animNames));
                        }
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }
        }
        #endregion

        #region Section 2: TYPD Simulation Parameters
        private void DrawParticleTypdBehaviorsSection(ParticleEffectFile pData)
        {
            EditorGUILayout.LabelField($"Simulation Parameters ({pData.Behaviors.Count} TYPD Blocks)", EditorStyles.boldLabel);

            for (int i = 0; i < pData.Behaviors.Count; i++)
            {
                var block = pData.Behaviors[i];
                if (!m_TypdFoldouts.ContainsKey(i)) m_TypdFoldouts[i] = false;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                m_TypdFoldouts[i] = EditorGUILayout.Foldout(m_TypdFoldouts[i], $"[{i:00}] {block.TypeName} (Code: {block.TypeId}, Words: {block.Parameters.Count})", true);

                if (m_TypdFoldouts[i])
                {
                    EditorGUI.indentLevel++;

                    // 1. GenerateParticle (Type 0)
                    if (block.BehaviorType == TypdBehaviorType.GenerateParticle && block.GeneratorHeader != null)
                    {
                        var hdr = block.GeneratorHeader;
                        EditorGUILayout.LabelField("Generator Header:", EditorStyles.boldLabel);
                        EditorGUILayout.Vector3Field("Spawn Area Box", hdr.SpawnArea);
                        EditorGUILayout.LabelField("Particle Lifetime:", $"{hdr.ParticleLife:F2}s");
                        EditorGUILayout.LabelField("Initial Speed:", $"{hdr.InitialSpeed:F2}");
                        EditorGUILayout.LabelField("Gravity / Drag:", $"Gravity: {hdr.Gravity:F2}, Drag: {hdr.Drag:F2}");
                        EditorGUILayout.LabelField("Blend Mode:", $"{hdr.BlendMode} | Draw Flags: 0x{hdr.DrawFlags:X2}");

                        if (block.ParticleSubKeyframes.Count > 0)
                        {
                            EditorGUILayout.Space(2);
                            EditorGUILayout.LabelField($"Particle Keyframe Curves ({block.ParticleSubKeyframes.Count} Sub-Records):", EditorStyles.boldLabel);

                            DrawPaginationControls(ref m_TypdSubKeyframePage, block.ParticleSubKeyframes.Count, 10);

                            int start = m_TypdSubKeyframePage * 10;
                            int end = Mathf.Min(block.ParticleSubKeyframes.Count, (m_TypdSubKeyframePage + 1) * 10);

                            for (int k = start; k < end; k++)
                            {
                                var kf = block.ParticleSubKeyframes[k];
                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                EditorGUILayout.LabelField($"Sub-Keyframe [{k:00}] (ID: {kf.KeyIndex}) - Lifetime: {kf.Lifetime:F2}s, Speed: {kf.Velocity:F2}", EditorStyles.miniBoldLabel);
                                EditorGUILayout.ColorField("Start Color", kf.StartColor);
                                EditorGUILayout.ColorField("End Color", kf.EndColor);
                                EditorGUILayout.LabelField($"Scale Curve: Start {kf.StartSize:F2} -> End {kf.EndSize:F2}");
                                EditorGUILayout.EndVertical();
                            }
                        }
                    }

                    // 2. Ambient Light (Type -20)
                    else if (block.BehaviorType == TypdBehaviorType.AmbientLight && block.Parameters.Count >= 3)
                    {
                        Color amb = new Color(block.GetFloat(0), block.GetFloat(1), block.GetFloat(2), 1f);
                        EditorGUILayout.ColorField("Ambient Light Color", amb);
                    }

                    // 3. Blur (Type -13)
                    else if (block.BehaviorType == TypdBehaviorType.Blur && block.Parameters.Count >= 1)
                    {
                        EditorGUILayout.LabelField("Blur Intensity:", $"{block.GetFloat(0):F3}");
                    }

                    // 4. SpotLight (Type -9)
                    else if (block.BehaviorType == TypdBehaviorType.SpotLight && block.Parameters.Count >= 34)
                    {
                        EditorGUILayout.LabelField("Spot Light Properties:", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Flags:", $"0x{block.Parameters[0]:X8}");
                        EditorGUILayout.LabelField("Inner / Outer Angle:", $"{block.GetFloat(2):F1}° / {block.GetFloat(3):F1}°");
                        EditorGUILayout.LabelField("Range / Attenuation:", $"{block.GetFloat(4):F1}m / {block.GetFloat(5):F2}");
                    }

                    // 5. Point Light (Type -3)
                    else if (block.BehaviorType == TypdBehaviorType.Light && block.Parameters.Count >= 13)
                    {
                        Color lightCol = new Color(block.GetFloat(5), block.GetFloat(6), block.GetFloat(7), block.GetFloat(8));
                        EditorGUILayout.ColorField("Light Color", lightCol);
                        EditorGUILayout.LabelField("Radius / Falloff:", $"{block.GetFloat(9):F1}m / {block.GetFloat(10):F2}");
                    }

                    // 6. Text / Bone Binding (Type -4)
                    else if (block.BehaviorType == TypdBehaviorType.Text && block.Parameters.Count >= 4)
                    {
                        int boneIdx = block.Parameters[1];
                        string boneName = (boneIdx >= 0 && boneIdx < pData.ExternalBones.Count)
                            ? pData.ExternalBones[boneIdx] : $"Bone_{boneIdx}";
                        EditorGUILayout.LabelField("Text Target Bone:", boneName);
                        EditorGUILayout.LabelField("Text Identifier:", $"{block.Parameters[0]}");
                    }

                    // 7. PlayAnimation (Type -2)
                    else if (block.BehaviorType == TypdBehaviorType.PlayAnimation && block.Parameters.Count >= 1)
                    {
                        int fIdx = block.Parameters[0];
                        string animFile = (fIdx >= 0 && fIdx < pData.ResourceFiles.Count)
                            ? pData.ResourceFiles[fIdx] : $"File_{fIdx}";
                        EditorGUILayout.LabelField("Target Animation File:", animFile);
                    }

                    // Raw Word Viewer for generic / custom blocks
                    if (m_UseGenericReflectionView || block.Parameters.Count < 46)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField($"Raw Parameter Words ({block.Parameters.Count}):", EditorStyles.miniBoldLabel);
                        int sampleCount = Mathf.Min(block.Parameters.Count, 32);
                        List<string> words = new List<string>();
                        for (int w = 0; w < sampleCount; w++)
                        {
                            words.Add($"[{w}] {block.Parameters[w]} (0x{block.Parameters[w]:X4}) / {block.GetFloat(w):F2}");
                        }
                        EditorGUILayout.LabelField(string.Join("\n", words), EditorStyles.miniLabel);
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }
        }
        #endregion

        #region Section 3: Sequence Timeline Cues
        private void DrawParticleSequenceCuesSection(ParticleEffectFile pData)
        {
            EditorGUILayout.LabelField($"Sequence Timeline Cues ({pData.SequenceCues.Count} Total)", EditorStyles.boldLabel);

            for (int i = 0; i < pData.SequenceCues.Count; i++)
            {
                var cue = pData.SequenceCues[i];
                GUIStyle rowBg = (i % 2 == 0) ? evenStyle : oddStyle;

                EditorGUILayout.BeginVertical(rowBg);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"[Cue_{i:00}]", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                GUILayout.Label($"Effect ID: {cue.EffectId}", EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label($"Target ID: {cue.TargetId}", EditorStyles.label, GUILayout.Width(90));
                GUILayout.Label($"Frames: {cue.StartTime} - {cue.EndTime}", EditorStyles.label, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Translation: ({cue.Translation.x:F2}, {cue.Translation.y:F2}, {cue.Translation.z:F2}) | Rotation: ({cue.Rotation.x:F1}°, {cue.Rotation.y:F1}°, {cue.Rotation.z:F1}°)");
                EditorGUILayout.LabelField($"Links: Top 0x{cue.NextEntryTop:X4}, Bottom 0x{cue.NextEntryBottom:X4} | Flags: [{cue.UserData1}, {cue.UserData2}, {cue.UserData3}, {cue.UserData4}]");
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }
        #endregion
    }
}