// File: Marathon/UnityParsers/NinjaAnimatorResolver.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    public static class NinjaAnimatorResolver
    {
        public static void SetupModelAnimations(
            NinjaNext loader,
            GameObject rootGO,
            List<Transform> nodeTransforms,
            string assetName,
            string assetPath,
            NinjaImportSettings settings,
            AssetImportContext ctx)
        {
            if (rootGO == null || loader?.Data == null) return;
            settings ??= NinjaImportSettings.Default;

            List<AnimationClip> loadedClips = new List<AnimationClip>();
            HashSet<string> loadedClipNames = new HashSet<string>();
            Dictionary<string, AnimationClip> loadedClipCache = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

            AnimationClip mainNodeClip = null;
            AnimationClip mainMatClip = null;

            // 1. Embedded & Adjacent Matching Motions (.xnm / .xnv)
            NinjaMotion nodeMotion = loader.Data.Motion;
            NinjaMotion matMotion = loader.Data.MaterialMotion;

            NinjaMotionResolver.ResolveLinkedMotions(assetPath, ctx, out NinjaMotion extraNodeMot, out NinjaMotion extraMatMot, out _, out _);
            nodeMotion ??= extraNodeMot;
            matMotion ??= extraMatMot;

            if (nodeMotion != null)
            {
                mainNodeClip = NinjaMotionResolver.ResolveMotion(nodeMotion, $"{assetName}_Animation", settings.Scale, rootGO, nodeTransforms, settings.MeshImportMode);
                if (mainNodeClip != null && loadedClipNames.Add(mainNodeClip.name))
                {
                    ctx.AddObjectToAsset("NodeAnimation", mainNodeClip);
                    loadedClips.Add(mainNodeClip);
                    loadedClipCache[assetName] = mainNodeClip;
                }
            }

            if (matMotion != null)
            {
                mainMatClip = NinjaMotionResolver.ResolveMotion(matMotion, $"{assetName}_MaterialAnimation", settings.Scale, rootGO, nodeTransforms, settings.MeshImportMode);
                if (mainMatClip != null && loadedClipNames.Add(mainMatClip.name))
                {
                    ctx.AddObjectToAsset("MaterialAnimation", mainMatClip);
                    loadedClips.Add(mainMatClip);
                    loadedClipCache[$"{assetName}_mat"] = mainMatClip;
                }
            }

            // 2. obj_param Associated Animations Resolution
            ResolvedStageContext stageContext = RelFolderResolver.ResolveAdjacentStageFiles(assetPath, ctx);
            var matchedParam = RelFolderResolver.FindParamEntryForModel(stageContext.ObjectParams, assetName);

            HashSet<string> distinctBoneFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> distinctTexFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (matchedParam.HasValue)
            {
                int objId = matchedParam.Value.Key;
                ObjectParamEntry paramEntry = matchedParam.Value.Value;

                RelObjectAnimationComponent animMeta = rootGO.AddComponent<RelObjectAnimationComponent>();
                animMeta.objID = objId;

                AnimationClip ResolveParamClip(string rawName, bool isMat, string prefix)
                {
                    if (string.IsNullOrEmpty(rawName)) return null;
                    string key = rawName.Trim();
                    if (loadedClipCache.TryGetValue(key, out AnimationClip cached)) return cached;

                    string animPath = RelFolderResolver.FindAnimationFilePath(key, stageContext.BaseDirectory, isMat);
                    if (!string.IsNullOrEmpty(animPath))
                    {
                        try
                        {
                            NinjaNext animLoader = new NinjaNext();
                            animLoader.Load(animPath);
                            NinjaMotion mot = isMat ? (animLoader.Data.MaterialMotion ?? animLoader.Data.Motion) : animLoader.Data.Motion;
                            if (mot != null)
                            {
                                ctx.DependsOnSourceAsset(animPath);
                                string clipId = $"{prefix}_{key}";
                                AnimationClip clip = NinjaMotionResolver.ResolveMotion(mot, clipId, settings.Scale, rootGO, nodeTransforms, settings.MeshImportMode);
                                if (clip != null)
                                {
                                    if (loadedClipNames.Add(clip.name))
                                    {
                                        ctx.AddObjectToAsset(clipId, clip);
                                        loadedClips.Add(clip);
                                    }
                                    loadedClipCache[key] = clip;
                                    return clip;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[NinjaAnimatorResolver] Failed loading anim {animPath}: {ex.Message}");
                        }
                    }
                    return null;
                }

                for (int a = 0; a < paramEntry.Animations.Count; a++)
                {
                    var aRef = paramEntry.Animations[a];
                    if (!string.IsNullOrEmpty(aRef.BoneAnimName)) distinctBoneFiles.Add(aRef.BoneAnimName.Trim());
                    if (!string.IsNullOrEmpty(aRef.TexAnimName)) distinctTexFiles.Add(aRef.TexAnimName.Trim());

                    AnimationClip bClip = ResolveParamClip(aRef.BoneAnimName, false, "Anim");
                    AnimationClip mClip = ResolveParamClip(aRef.TexAnimName, true, "MatAnim");
                    mainNodeClip ??= bClip;
                    mainMatClip ??= mClip;

                    animMeta.animations.Add(new ObjectAnimationEntryData
                    {
                        id1 = aRef.UnknownIdentifier1,
                        id2 = aRef.UnknownIdentifier2,
                        boneAnimName = aRef.BoneAnimName,
                        texAnimName = aRef.TexAnimName,
                        boneClip = bClip,
                        materialClip = mClip,
                        paramFloat1 = aRef.UnknownFloat1,
                        paramFloat2 = aRef.UnknownFloat2,
                        paramFloat3 = aRef.UnknownFloat3,
                        paramFloat4 = aRef.UnknownFloat4,
                        paramFloat5 = aRef.UnknownFloat5,
                        paramFloat6 = aRef.UnknownFloat6
                    });
                }
            }

            // 3. Attach Animator & Controller Auto-Setup
            if (loadedClips.Count > 0)
            {
                Animator animator = rootGO.AddComponent<Animator>();
                bool isSingleAnim = distinctBoneFiles.Count <= 1 && distinctTexFiles.Count <= 1;

                if (settings.GenerateAnimatorController && isSingleAnim && (mainNodeClip != null || mainMatClip != null))
                {
                    BuildTwoLayerAnimatorController(assetName, mainNodeClip, mainMatClip, animator, ctx);
                }
            }
        }

        public static bool CanGenerateAnimatorController(string assetPath, out int distinctBoneCount, out int distinctTexCount)
        {
            distinctBoneCount = 0; distinctTexCount = 0;
            if (string.IsNullOrEmpty(assetPath)) return true;

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            ResolvedStageContext stageCtx = RelFolderResolver.ResolveAdjacentStageFiles(assetPath);
            var matchedParam = RelFolderResolver.FindParamEntryForModel(stageCtx.ObjectParams, assetName);

            if (!matchedParam.HasValue) return true;

            HashSet<string> distinctBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> distinctTexs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in matchedParam.Value.Value.Animations)
            {
                if (!string.IsNullOrEmpty(a.BoneAnimName)) distinctBones.Add(a.BoneAnimName.Trim());
                if (!string.IsNullOrEmpty(a.TexAnimName)) distinctTexs.Add(a.TexAnimName.Trim());
            }

            distinctBoneCount = distinctBones.Count;
            distinctTexCount = distinctTexs.Count;
            return distinctBones.Count <= 1 && distinctTexs.Count <= 1;
        }

        private static void BuildTwoLayerAnimatorController(
            string assetName,
            AnimationClip mainNodeClip,
            AnimationClip mainMatClip,
            Animator animator,
            AssetImportContext ctx)
        {
            AnimatorController controller = new AnimatorController { name = $"{assetName}_Controller" };
            ctx.AddObjectToAsset("AnimatorController", controller);

            void AddControllerLayer(string layerName, string statePrefix, AnimationClip clip, float weight)
            {
                if (clip == null) return;
                int idx = controller.layers.Length;
                controller.AddLayer(layerName);
                if (idx > 0)
                {
                    var layers = controller.layers;
                    layers[idx].defaultWeight = weight;
                    controller.layers = layers;
                }
                AnimatorStateMachine sm = controller.layers[idx].stateMachine;
                if (sm != null)
                {
                    ctx.AddObjectToAsset($"{statePrefix}StateMachine", sm);
                    AnimatorState st = sm.AddState(clip.name);
                    st.motion = clip;
                    sm.defaultState = st;
                    ctx.AddObjectToAsset($"{statePrefix}State", st);
                }
            }

            AddControllerLayer("Base Layer", "Node", mainNodeClip, 1.0f);
            AddControllerLayer("Material Layer", "Mat", mainMatClip, 1.0f);

            animator.runtimeAnimatorController = controller;
        }
    }
}