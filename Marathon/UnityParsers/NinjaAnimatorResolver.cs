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
            float scale,
            MeshImportMode importMode,
            bool generateController,
            AssetImportContext ctx)
        {
            if (rootGO == null || loader?.Data == null) return;

            List<AnimationClip> loadedClips = new List<AnimationClip>();
            HashSet<string> loadedClipNames = new HashSet<string>();
            Dictionary<string, AnimationClip> loadedClipCache = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

            AnimationClip mainNodeClip = null;
            AnimationClip mainMatClip = null;

            // 1. Embedded & Adjacent Matching Motions (.xnm / .xnv)
            NinjaMotion nodeMotion = loader.Data.Motion;
            NinjaMotion matMotion = loader.Data.MaterialMotion;

            NinjaMotionResolver.ResolveLinkedMotions(assetPath, ctx, out NinjaMotion extraNodeMot, out NinjaMotion extraMatMot, out _, out _);
            if (nodeMotion == null) nodeMotion = extraNodeMot;
            if (matMotion == null) matMotion = extraMatMot;

            if (nodeMotion != null)
            {
                mainNodeClip = NinjaMotionResolver.ResolveMotion(nodeMotion, $"{assetName}_Animation", scale, rootGO, nodeTransforms, importMode);
                if (mainNodeClip != null && loadedClipNames.Add(mainNodeClip.name))
                {
                    ctx.AddObjectToAsset("NodeAnimation", mainNodeClip);
                    loadedClips.Add(mainNodeClip);
                    loadedClipCache[assetName] = mainNodeClip;
                }
            }

            if (matMotion != null)
            {
                mainMatClip = NinjaMotionResolver.ResolveMotion(matMotion, $"{assetName}_MaterialAnimation", scale, rootGO, nodeTransforms, importMode);
                if (mainMatClip != null && loadedClipNames.Add(mainMatClip.name))
                {
                    ctx.AddObjectToAsset("MaterialAnimation", mainMatClip);
                    loadedClips.Add(mainMatClip);
                    loadedClipCache[$"{assetName}_mat"] = mainMatClip;
                }
            }

            // 2. obj_param.xnr Associated Animations Resolution
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

                for (int a = 0; a < paramEntry.Animations.Count; a++)
                {
                    var aRef = paramEntry.Animations[a];
                    if (!string.IsNullOrEmpty(aRef.BoneAnimName)) distinctBoneFiles.Add(aRef.BoneAnimName.Trim());
                    if (!string.IsNullOrEmpty(aRef.TexAnimName)) distinctTexFiles.Add(aRef.TexAnimName.Trim());

                    ObjectAnimationEntryData entryData = new ObjectAnimationEntryData
                    {
                        id1 = aRef.UnknownIdentifier1,
                        id2 = aRef.UnknownIdentifier2,
                        boneAnimName = aRef.BoneAnimName,
                        texAnimName = aRef.TexAnimName,
                        paramFloat1 = aRef.UnknownFloat1,
                        paramFloat2 = aRef.UnknownFloat2,
                        paramFloat3 = aRef.UnknownFloat3,
                        paramFloat4 = aRef.UnknownFloat4,
                        paramFloat5 = aRef.UnknownFloat5,
                        paramFloat6 = aRef.UnknownFloat6
                    };

                    // A. Resolve Bone Animation from obj_param (Deduplicated)
                    if (!string.IsNullOrEmpty(aRef.BoneAnimName))
                    {
                        string bKey = aRef.BoneAnimName.Trim();
                        if (loadedClipCache.TryGetValue(bKey, out AnimationClip cachedClip))
                        {
                            entryData.boneClip = cachedClip;
                            if (mainNodeClip == null) mainNodeClip = cachedClip;
                        }
                        else
                        {
                            string boneAnimPath = RelFolderResolver.FindAnimationFilePath(bKey, stageContext.BaseDirectory, false);
                            if (!string.IsNullOrEmpty(boneAnimPath))
                            {
                                try
                                {
                                    NinjaNext animLoader = new NinjaNext();
                                    animLoader.Load(boneAnimPath);
                                    if (animLoader.Data.Motion != null)
                                    {
                                        ctx.DependsOnSourceAsset(boneAnimPath);
                                        string clipId = $"Anim_{bKey}";
                                        AnimationClip paramClip = NinjaMotionResolver.ResolveMotion(animLoader.Data.Motion, clipId, scale, rootGO, nodeTransforms, importMode);
                                        if (paramClip != null)
                                        {
                                            if (loadedClipNames.Add(paramClip.name))
                                            {
                                                ctx.AddObjectToAsset(clipId, paramClip);
                                                loadedClips.Add(paramClip);
                                            }
                                            loadedClipCache[bKey] = paramClip;
                                            entryData.boneClip = paramClip;
                                            if (mainNodeClip == null) mainNodeClip = paramClip;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[NinjaAnimatorResolver] Could not load bone anim {boneAnimPath}: {ex.Message}");
                                }
                            }
                        }
                    }

                    // B. Resolve Material Animation from obj_param (Deduplicated)
                    if (!string.IsNullOrEmpty(aRef.TexAnimName))
                    {
                        string tKey = aRef.TexAnimName.Trim();
                        if (loadedClipCache.TryGetValue(tKey, out AnimationClip cachedMatClip))
                        {
                            entryData.materialClip = cachedMatClip;
                            if (mainMatClip == null) mainMatClip = cachedMatClip;
                        }
                        else
                        {
                            string texAnimPath = RelFolderResolver.FindAnimationFilePath(tKey, stageContext.BaseDirectory, true);
                            if (!string.IsNullOrEmpty(texAnimPath))
                            {
                                try
                                {
                                    NinjaNext animLoader = new NinjaNext();
                                    animLoader.Load(texAnimPath);
                                    NinjaMotion foundMatMot = animLoader.Data.MaterialMotion ?? animLoader.Data.Motion;
                                    if (foundMatMot != null)
                                    {
                                        ctx.DependsOnSourceAsset(texAnimPath);
                                        string clipId = $"MatAnim_{tKey}";
                                        AnimationClip paramMatClip = NinjaMotionResolver.ResolveMotion(foundMatMot, clipId, scale, rootGO, nodeTransforms, importMode);
                                        if (paramMatClip != null)
                                        {
                                            if (loadedClipNames.Add(paramMatClip.name))
                                            {
                                                ctx.AddObjectToAsset(clipId, paramMatClip);
                                                loadedClips.Add(paramMatClip);
                                            }
                                            loadedClipCache[tKey] = paramMatClip;
                                            entryData.materialClip = paramMatClip;
                                            if (mainMatClip == null) mainMatClip = paramMatClip;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[NinjaAnimatorResolver] Could not load material anim {texAnimPath}: {ex.Message}");
                                }
                            }
                        }
                    }

                    animMeta.animations.Add(entryData);
                }
            }

            // 3. Attach Animator Component
            if (loadedClips.Count > 0)
            {
                Animator animator = rootGO.AddComponent<Animator>();

                // 4. Optional 2-Layer Controller Auto-Setup
                bool isSingleAnimAsset = distinctBoneFiles.Count <= 1 && distinctTexFiles.Count <= 1;

                if (generateController && isSingleAnimAsset && (mainNodeClip != null || mainMatClip != null))
                {
                    BuildTwoLayerAnimatorController(assetName, mainNodeClip, mainMatClip, animator, ctx);
                }
            }
        }

        public static bool CanGenerateAnimatorController(string assetPath, out int distinctBoneCount, out int distinctTexCount)
        {
            distinctBoneCount = 0;
            distinctTexCount = 0;
            if (string.IsNullOrEmpty(assetPath)) return true;

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            ResolvedStageContext stageCtx = RelFolderResolver.ResolveAdjacentStageFiles(assetPath);
            var matchedParam = RelFolderResolver.FindParamEntryForModel(stageCtx.ObjectParams, assetName);

            if (!matchedParam.HasValue) return true;

            HashSet<string> distinctBoneFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> distinctTexFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in matchedParam.Value.Value.Animations)
            {
                if (!string.IsNullOrEmpty(a.BoneAnimName)) distinctBoneFiles.Add(a.BoneAnimName.Trim());
                if (!string.IsNullOrEmpty(a.TexAnimName)) distinctTexFiles.Add(a.TexAnimName.Trim());
            }

            distinctBoneCount = distinctBoneFiles.Count;
            distinctTexCount = distinctTexFiles.Count;
            return distinctBoneFiles.Count <= 1 && distinctTexFiles.Count <= 1;
        }

        private static void BuildTwoLayerAnimatorController(
            string assetName,
            AnimationClip mainNodeClip,
            AnimationClip mainMatClip,
            Animator animator,
            AssetImportContext ctx)
        {
            AnimatorController controller = new AnimatorController();
            controller.name = $"{assetName}_Controller";
            ctx.AddObjectToAsset("AnimatorController", controller);

            // Layer 0: Base Layer (Node / Transform Animation)
            if (mainNodeClip != null)
            {
                controller.AddLayer("Base Layer");
                AnimatorStateMachine baseSM = controller.layers[0].stateMachine;
                if (baseSM != null)
                {
                    ctx.AddObjectToAsset("BaseStateMachine", baseSM);
                    AnimatorState nodeState = baseSM.AddState(mainNodeClip.name);
                    nodeState.motion = mainNodeClip;
                    baseSM.defaultState = nodeState;
                    ctx.AddObjectToAsset("NodeState", nodeState);
                }
            }

            // Layer 1: Material Layer (Material / UV Animation)
            if (mainMatClip != null)
            {
                int layerIdx = controller.layers.Length;
                if (layerIdx == 0)
                {
                    controller.AddLayer("Material Layer");
                    AnimatorStateMachine matSM = controller.layers[0].stateMachine;
                    if (matSM != null)
                    {
                        ctx.AddObjectToAsset("MatStateMachine", matSM);
                        AnimatorState matState = matSM.AddState(mainMatClip.name);
                        matState.motion = mainMatClip;
                        matSM.defaultState = matState;
                        ctx.AddObjectToAsset("MatState", matState);
                    }
                }
                else
                {
                    controller.AddLayer("Material Layer");
                    AnimatorControllerLayer[] layers = controller.layers;
                    layers[layerIdx].defaultWeight = 1.0f; // Simultaneous evaluation with Base Layer
                    controller.layers = layers;

                    AnimatorStateMachine matSM = controller.layers[layerIdx].stateMachine;
                    if (matSM != null)
                    {
                        ctx.AddObjectToAsset("MatStateMachine", matSM);
                        AnimatorState matState = matSM.AddState(mainMatClip.name);
                        matState.motion = mainMatClip;
                        matSM.defaultState = matState;
                        ctx.AddObjectToAsset("MatState", matState);
                    }
                }
            }

            animator.runtimeAnimatorController = controller;
        }
    }
}