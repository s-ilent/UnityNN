// File: Marathon/Editor/UnityParsers/NinjaAnimatorResolver.cs
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
        private static readonly string[] AnimationExtensions = {
            ".xnm", ".xnv", ".gnm", ".gnv", ".znm", ".znv"
        };

        /// <summary>
        /// Finds all animation files matching the model's name (exact or prefix: modelName_*.xnm/xnv)
        /// in the local directory and immediate parent/sibling directories.
        /// </summary>
        public static List<string> FindModelAnimationFiles(string assetPath)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrEmpty(assetPath)) return results;

            string baseDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            string prefix = assetName + "_";

            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> animExts = new HashSet<string>(AnimationExtensions, StringComparer.OrdinalIgnoreCase);

            List<string> candidateDirs = new List<string> { baseDir };

            // Check parent and sibling directories (e.g., _HoltesCommon <-> map folders)
            string parent = Path.GetDirectoryName(baseDir)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
                candidateDirs.Add(parent);
                try
                {
                    string[] subDirs = Directory.GetDirectories(parent, "*", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < subDirs.Length; i++)
                    {
                        string sub = subDirs[i].Replace('\\', '/');
                        if (!sub.Equals(baseDir, StringComparison.OrdinalIgnoreCase))
                            candidateDirs.Add(sub);
                    }
                }
                catch { }
            }

            for (int d = 0; d < candidateDirs.Count; d++)
            {
                string dir = candidateDirs[d];
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                try
                {
                    string[] files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
                    for (int f = 0; f < files.Length; f++)
                    {
                        string file = files[f];
                        string ext = Path.GetExtension(file);
                        if (animExts.Contains(ext))
                        {
                            string fn = Path.GetFileNameWithoutExtension(file);
                            if (fn.Equals(assetName, StringComparison.OrdinalIgnoreCase) ||
                                fn.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                string normPath = file.Replace('\\', '/');
                                if (seenPaths.Add(normPath))
                                {
                                    results.Add(normPath);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return results;
        }

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

            HashSet<string> distinctBoneFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> distinctTexFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            RelObjectAnimationComponent animMeta = null;

            // 1. Embedded Motions in Model chunk (.xnj / multi-chunk containers)
            if (loader.Data.Motion != null)
            {
                mainNodeClip = NinjaMotionResolver.ResolveMotion(loader.Data.Motion, $"{assetName}_Animation", settings.Scale, rootGO, nodeTransforms, settings.MeshImportMode);
                if (mainNodeClip != null && loadedClipNames.Add(mainNodeClip.name))
                {
                    ctx.AddObjectToAsset("NodeAnimation", mainNodeClip);
                    loadedClips.Add(mainNodeClip);
                    loadedClipCache[assetName] = mainNodeClip;
                    distinctBoneFiles.Add(assetName);
                }
            }

            if (loader.Data.MaterialMotion != null)
            {
                mainMatClip = NinjaMotionResolver.ResolveMotion(loader.Data.MaterialMotion, $"{assetName}_MaterialAnimation", settings.Scale, rootGO, nodeTransforms, settings.MeshImportMode);
                if (mainMatClip != null && loadedClipNames.Add(mainMatClip.name))
                {
                    ctx.AddObjectToAsset("MaterialAnimation", mainMatClip);
                    loadedClips.Add(mainMatClip);
                    loadedClipCache[$"{assetName}_mat"] = mainMatClip;
                    distinctTexFiles.Add(assetName);
                }
            }

            // 2. Discover Associated Named Animation Files (Exact: assetName.* and Prefix: assetName_*.*)
            List<string> animFiles = FindModelAnimationFiles(assetPath);

            for (int i = 0; i < animFiles.Count; i++)
            {
                string animPath = animFiles[i];
                if (animPath.Equals(assetPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;

                string rawAnimName = Path.GetFileNameWithoutExtension(animPath);
                if (loadedClipCache.ContainsKey(rawAnimName)) continue;

                try
                {
                    NinjaNext animLoader = new NinjaNext();
                    animLoader.Load(animPath);

                    string ext = Path.GetExtension(animPath).ToLowerInvariant();
                    bool isMat = ext is ".xnv" or ".gnv" or ".znv" || 
                                (animLoader.Data.MaterialMotion != null) || 
                                (animLoader.Data.Motion?.Type.HasFlag(MotionType.NND_MOTIONTYPE_MATERIAL) == true);

                    NinjaMotion mot = isMat ? (animLoader.Data.MaterialMotion ?? animLoader.Data.Motion) : animLoader.Data.Motion;
                    if (mot != null)
                    {
                        ctx.DependsOnSourceAsset(animPath);
                        string clipId = isMat ? $"MatAnim_{rawAnimName}" : $"Anim_{rawAnimName}";
                        AnimationClip clip = NinjaMotionResolver.ResolveMotion(mot, rawAnimName, settings.Scale, rootGO, nodeTransforms, settings.MeshImportMode);

                        if (clip != null && loadedClipNames.Add(clip.name))
                        {
                            ctx.AddObjectToAsset(clipId, clip);
                            loadedClips.Add(clip);
                            loadedClipCache[rawAnimName] = clip;

                            if (isMat)
                            {
                                distinctTexFiles.Add(rawAnimName);
                                mainMatClip ??= clip;
                            }
                            else
                            {
                                distinctBoneFiles.Add(rawAnimName);
                                mainNodeClip ??= clip;
                            }

                            if (animMeta == null)
                            {
                                animMeta = rootGO.AddComponent<RelObjectAnimationComponent>();
                            }

                            animMeta.animations.Add(new ObjectAnimationEntryData
                            {
                                boneAnimName = isMat ? "" : rawAnimName,
                                texAnimName = isMat ? rawAnimName : "",
                                boneClip = isMat ? null : clip,
                                materialClip = isMat ? clip : null
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NinjaAnimatorResolver] Failed loading associated animation {animPath}: {ex.Message}");
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

            HashSet<string> distinctBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> distinctTexs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            List<string> animFiles = FindModelAnimationFiles(assetPath);
            for (int i = 0; i < animFiles.Count; i++)
            {
                string animPath = animFiles[i];
                string ext = Path.GetExtension(animPath).ToLowerInvariant();
                string fn = Path.GetFileNameWithoutExtension(animPath);
                if (ext is ".xnv" or ".gnv" or ".znv") distinctTexs.Add(fn);
                else distinctBones.Add(fn);
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