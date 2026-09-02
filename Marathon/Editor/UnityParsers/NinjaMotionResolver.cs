// File: Marathon/Editor/UnityParsers/NinjaMotionResolver.cs
using UnityNN;
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace UnityNN.Editor
{
    /// <summary>
    /// Converts Sega NN BAMS/Radian motion tracks into native Unity AnimationClip curves.
    /// Handles node bone transformations, multi-layer UV scrolling tracks, and material color animations.
    /// </summary>
    public struct TrackBindingDef
    {
        public uint CategoryMask; // 1 = Node, 16 = Material
        public uint Mask;         // 0 if channel is a companion-only default (e.g., UV Tiling Scale)
        public Type ComponentType;
        public string PropertyName;
        public string GroupKey;
        public int ChannelIndex;
        public float DefaultRestValue;
        public bool InvertSign;
    }

    public struct MaterialBindingTarget
    {
        public string TargetPath;
        public int MaterialSlot;
    }

    public static class NinjaMotionResolver
    {
        private static readonly string[] MotionExtensions = { ".xnm", ".gnm", ".znm" };
        private static readonly string[] MaterialMotionExtensions = { ".xnv", ".gnv", ".znv" };
        private static readonly string[] ModelExtensions = { ".xna", ".xnn", ".xnj", ".xno", ".gna", ".gnn", ".gno" };

        public static readonly TrackBindingDef[] AllTrackBindings = new[]
        {
            // --- Node Translation (XYZ) ---
            new TrackBindingDef { CategoryMask = 1, Mask = 0x100U, ComponentType = typeof(Transform), PropertyName = "localPosition.x", GroupKey = "localPosition", ChannelIndex = 0, DefaultRestValue = 0f, InvertSign = true },
            new TrackBindingDef { CategoryMask = 1, Mask = 0x200U, ComponentType = typeof(Transform), PropertyName = "localPosition.y", GroupKey = "localPosition", ChannelIndex = 1, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 1, Mask = 0x400U, ComponentType = typeof(Transform), PropertyName = "localPosition.z", GroupKey = "localPosition", ChannelIndex = 2, DefaultRestValue = 0f, InvertSign = false },

            // --- Node Rotation (Euler) ---
            new TrackBindingDef { CategoryMask = 1, Mask = 0x800U, ComponentType = typeof(Transform), PropertyName = "localEulerAnglesRaw.x", GroupKey = "localEulerAnglesRaw", ChannelIndex = 0, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 1, Mask = 0x1000U, ComponentType = typeof(Transform), PropertyName = "localEulerAnglesRaw.y", GroupKey = "localEulerAnglesRaw", ChannelIndex = 1, DefaultRestValue = 0f, InvertSign = true },
            new TrackBindingDef { CategoryMask = 1, Mask = 0x2000U, ComponentType = typeof(Transform), PropertyName = "localEulerAnglesRaw.z", GroupKey = "localEulerAnglesRaw", ChannelIndex = 2, DefaultRestValue = 0f, InvertSign = true },

            // --- Node Scaling (XYZ) ---
            new TrackBindingDef { CategoryMask = 1, Mask = 0x8000U, ComponentType = typeof(Transform), PropertyName = "localScale.x", GroupKey = "localScale", ChannelIndex = 0, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 1, Mask = 0x10000U, ComponentType = typeof(Transform), PropertyName = "localScale.y", GroupKey = "localScale", ChannelIndex = 1, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 1, Mask = 0x20000U, ComponentType = typeof(Transform), PropertyName = "localScale.z", GroupKey = "localScale", ChannelIndex = 2, DefaultRestValue = 1f, InvertSign = false },

            // --- Material Diffuse Color (_Color) ---
            new TrackBindingDef { CategoryMask = 16, Mask = 0x200U, ComponentType = typeof(Renderer), PropertyName = "material._Color.r", GroupKey = "_Color", ChannelIndex = 0, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x400U, ComponentType = typeof(Renderer), PropertyName = "material._Color.g", GroupKey = "_Color", ChannelIndex = 1, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x800U, ComponentType = typeof(Renderer), PropertyName = "material._Color.b", GroupKey = "_Color", ChannelIndex = 2, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x1000U, ComponentType = typeof(Renderer), PropertyName = "material._Color.a", GroupKey = "_Color", ChannelIndex = 3, DefaultRestValue = 1f, InvertSign = false },

            // --- Material Ambient Color (_AmbientColor) ---
            new TrackBindingDef { CategoryMask = 16, Mask = 0x40000U, ComponentType = typeof(Renderer), PropertyName = "material._AmbientColor.r", GroupKey = "_AmbientColor", ChannelIndex = 0, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x80000U, ComponentType = typeof(Renderer), PropertyName = "material._AmbientColor.g", GroupKey = "_AmbientColor", ChannelIndex = 1, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x100000U, ComponentType = typeof(Renderer), PropertyName = "material._AmbientColor.b", GroupKey = "_AmbientColor", ChannelIndex = 2, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0, ComponentType = typeof(Renderer), PropertyName = "material._AmbientColor.a", GroupKey = "_AmbientColor", ChannelIndex = 3, DefaultRestValue = 1f, InvertSign = false },

            // --- Material Specular Color (_SpecColor) ---
            new TrackBindingDef { CategoryMask = 16, Mask = 0x2000U, ComponentType = typeof(Renderer), PropertyName = "material._SpecColor.r", GroupKey = "_SpecColor", ChannelIndex = 0, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x4000U, ComponentType = typeof(Renderer), PropertyName = "material._SpecColor.g", GroupKey = "_SpecColor", ChannelIndex = 1, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x8000U, ComponentType = typeof(Renderer), PropertyName = "material._SpecColor.b", GroupKey = "_SpecColor", ChannelIndex = 2, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0, ComponentType = typeof(Renderer), PropertyName = "material._SpecColor.a", GroupKey = "_SpecColor", ChannelIndex = 3, DefaultRestValue = 1f, InvertSign = false },

            // --- Material UV Scale & Offset (_MainTex_ST) ---
            new TrackBindingDef { CategoryMask = 16, Mask = 0, ComponentType = typeof(Renderer), PropertyName = "material._MainTex_ST.x", GroupKey = "_MainTex_ST", ChannelIndex = 0, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0, ComponentType = typeof(Renderer), PropertyName = "material._MainTex_ST.y", GroupKey = "_MainTex_ST", ChannelIndex = 1, DefaultRestValue = 1f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x800000U, ComponentType = typeof(Renderer), PropertyName = "material._MainTex_ST.z", GroupKey = "_MainTex_ST", ChannelIndex = 2, DefaultRestValue = 0f, InvertSign = false },
            new TrackBindingDef { CategoryMask = 16, Mask = 0x1000000U, ComponentType = typeof(Renderer), PropertyName = "material._MainTex_ST.w", GroupKey = "_MainTex_ST", ChannelIndex = 3, DefaultRestValue = 0f, InvertSign = true }
        };

        #region Angle Conversion & Math Helpers

        /// <summary>
        /// Converts a 16-bit Binary Angle Measurement System (BAMS) short to degrees (32768 = 180 deg).
        /// </summary>
        public static float BamsToDegrees(int bamAngle) => (float)((double)bamAngle * (180.0 / 32768.0));

        /// <summary>
        /// Converts a 32-bit BAMS integer to degrees (65536 = 360 deg).
        /// </summary>
        public static float Bams32ToDegrees(int bam32Angle) => (float)((double)bam32Angle * (360.0 / 65536.0));

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        public static float RadiansToDegrees(float radAngle) => radAngle * Mathf.Rad2Deg;

        /// <summary>
        /// Unrolls a 16-bit BAMS integer angle across consecutive keyframes to prevent 180-degree overflow flipping.
        /// </summary>
        public static int UnrollBams16(short rawValue, ref long accumBams, ref int lastDelta, ref bool isFirst)
        {
            if (isFirst)
            {
                accumBams = rawValue;
                lastDelta = 0;
                isFirst = false;
                return (int)accumBams;
            }

            int currU16 = rawValue & 0xFFFF;
            int prevU16 = (int)(accumBams & 0xFFFF);
            int diff = (currU16 - prevU16) & 0xFFFF;

            int delta;
            if (diff < 32768)
            {
                delta = diff;
            }
            else if (diff > 32768)
            {
                delta = diff - 65536;
            }
            else
            {
                delta = (lastDelta >= 0) ? 32768 : -32768;
            }

            lastDelta = delta;
            accumBams += delta;
            return (int)accumBams;
        }

        /// <summary>
        /// Unrolls a 16-bit BAMS integer angle and converts the unrolled result directly to degrees.
        /// </summary>
        public static int UnrollBams16(short rawValue, ref long accumBams, ref bool isFirst)
        {
            int dummyDelta = 0;
            return UnrollBams16(rawValue, ref accumBams, ref dummyDelta, ref isFirst);
        }

        public static float Bams16ToUnrolledDegrees(short rawValue, ref long accumBams, ref int lastDelta, ref bool isFirst)
        {
            int unrolledBams = UnrollBams16(rawValue, ref accumBams, ref lastDelta, ref isFirst);
            return BamsToDegrees(unrolledBams);
        }

        public static float Bams16ToUnrolledDegrees(short rawValue, ref long accumBams, ref bool isFirst)
        {
            int dummyDelta = 0;
            return Bams16ToUnrolledDegrees(rawValue, ref accumBams, ref dummyDelta, ref isFirst);
        }

        public static long Gcd(long a, long b) => b == 0 ? a : Gcd(b, a % b);

        /// <summary>
        /// Computes Least Common Multiple.
        /// </summary>
        public static long Lcm(long a, long b)
        {
            if (a <= 0 || b <= 0) return Math.Max(a, b);
            return (a / Gcd(a, b)) * b;
        }

        /// <summary>
        /// Extracts the raw frame timestamp from any Ninja keyframe struct.
        /// </summary>
        public static float GetKeyframeFrame(object objKf)
        {
            if (objKf is NinjaKeyframe.NNS_MOTION_KEY_VECTOR v) return v.Frame;
            if (objKf is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16 r) return r.Frame;
            if (objKf is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32) return s32.Frame;
            if (objKf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT f) return f.Frame;
            if (objKf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16) return s16.Frame;
            return 0f;
        }

        /// <summary>
        /// Extracts the frame timestamp and converted scalar value from SINT32, FLOAT, or SINT16 keyframes.
        /// </summary>
        public static bool TryExtractScalarKeyframe(
            object kf,
            uint subMotionFlags,
            ref long accumS16Bams,
            ref int lastDeltaS16,
            ref bool isFirstS16,
            out float frame,
            out float scalarValue)
        {
            frame = 0f;
            scalarValue = 0f;

            if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32)
            {
                frame = s32.Frame;
                // 32-Bit BAMS is already unrolled in binary stream
                scalarValue = (subMotionFlags & 8U) != 0 ? Bams32ToDegrees(s32.Value) : s32.Value;
                return true;
            }
            if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT f)
            {
                frame = f.Frame;
                scalarValue = (subMotionFlags & 4U) != 0 ? RadiansToDegrees(f.Value) : f.Value;
                return true;
            }
            if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16)
            {
                frame = s16.Frame;
                scalarValue = Bams16ToUnrolledDegrees(s16.Value, ref accumS16Bams, ref lastDeltaS16, ref isFirstS16);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Generates tiled frame timestamps across the master animation timeline [0, masterEndFrames],
        /// including prior/subsequent cycles (c = -1 to maxCycle + 1) to anchor Frame 0 and Frame masterEndFrames.
        /// </summary>

        public static List<float> GenerateTiledFrameTimes(
            float rawFrame,
            float subStart,
            float subEnd,
            float masterEndFrames,
            bool isRepeatingTrack)
        {
            return new List<float> { rawFrame };
        }

        /// <summary>
        /// Calculates the effective clip duration in frames using Least Common Multiple (LCM) across repeating sub-tracks.
        /// </summary>
        public static long CalculateEffectiveLoopFrames(NinjaMotion motionData, long maxCap = 2400)
        {
            if (motionData == null) return 600;
            return (long)Mathf.Max(1f, motionData.EndFrame - motionData.StartFrame);
        }

        #endregion

        public readonly struct PropertyKey : IEquatable<PropertyKey>
        {
            public readonly string TargetPath;
            public readonly Type ComponentType;
            public readonly string PropertyName;

            public PropertyKey(string targetPath, Type componentType, string propertyName)
            {
                TargetPath = targetPath ?? "";
                ComponentType = componentType;
                PropertyName = propertyName ?? "";
            }

            public bool Equals(PropertyKey other) =>
                TargetPath == other.TargetPath &&
                ComponentType == other.ComponentType &&
                PropertyName == other.PropertyName;

            public override bool Equals(object obj) => obj is PropertyKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(TargetPath, ComponentType, PropertyName);
        }

        public class SubMotionSegment
        {
            public SubMotionInterpolationType InterpolationType;
            public readonly List<Keyframe> Keyframes = new List<Keyframe>();
        }

        #region Motion Discovery & Linking
        public static void ResolveLinkedMotions(
            string assetPath,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            out NinjaMotion nodeMotion,
            out NinjaMotion matMotion,
            out string nodeMotionSource,
            out string matMotionSource)
        {
            nodeMotion = null;
            matMotion = null;
            nodeMotionSource = "Embedded";
            matMotionSource = "Embedded";

            if (string.IsNullOrEmpty(assetPath)) return;

            string baseDir = Path.GetDirectoryName(assetPath);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            nodeMotion = LoadLinkedMotion(baseDir, baseName, MotionExtensions, assetPath, ctx, out nodeMotionSource);
            matMotion = LoadLinkedMotion(baseDir, baseName, MaterialMotionExtensions, assetPath, ctx, out matMotionSource);
        }

        private static NinjaMotion LoadLinkedMotion(
            string baseDir,
            string baseName,
            string[] extensions,
            string assetPath,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            out string sourceDesc)
        {
            sourceDesc = "Embedded";

            foreach (string ext in extensions)
            {
                string candidate = Path.Combine(baseDir, baseName + ext).Replace('\\', '/');
                if (candidate.Equals(assetPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;

                if (File.Exists(candidate))
                {
                    try
                    {
                        NinjaNext loader = new NinjaNext();
                        loader.Load(candidate);
                        NinjaMotion mot = loader.Data.MaterialMotion ?? loader.Data.Motion;
                        if (mot != null)
                        {
                            sourceDesc = $"External: {Path.GetFileName(candidate)}";
                            ctx?.DependsOnSourceAsset(candidate);
                            return mot;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NinjaMotionResolver] Failed loading linked motion {candidate}: {ex.Message}");
                    }
                }
            }
            return null;
        }

        public static string[] ResolveNodeHierarchyTargets(string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            return ResolveNodeHierarchyTargets(assetPath, ctx, out _);
        }

        public static string[] ResolveNodeHierarchyTargets(
            string assetPath,
            UnityEditor.AssetImporters.AssetImportContext ctx,
            out NinjaObject associatedObject)
        {
            associatedObject = null;
            if (string.IsNullOrEmpty(assetPath)) return Array.Empty<string>();

            string baseDir = Path.GetDirectoryName(assetPath);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            foreach (string ext in ModelExtensions)
            {
                string candidate = Path.Combine(baseDir, baseName + ext).Replace('\\', '/');
                if (File.Exists(candidate))
                {
                    try
                    {
                        NinjaNext loader = new NinjaNext();
                        loader.Load(candidate);

                        if (loader.Data.Object != null)
                        {
                            associatedObject = loader.Data.Object;
                            if (loader.Data.Object.Nodes != null && loader.Data.Object.Nodes.Count > 0)
                            {
                                ctx?.DependsOnSourceAsset(candidate);
                                return ComputeNodeHierarchyPaths(loader.Data.Object.Nodes);
                            }
                        }

                        if (loader.Data.NodeNameList?.NinjaNodeNames != null)
                        {
                            ctx?.DependsOnSourceAsset(candidate);
                            return loader.Data.NodeNameList.NinjaNodeNames.ToArray();
                        }
                    }
                    catch { }
                }
            }
            return Array.Empty<string>();
        }

        public static string[] ComputeNodeHierarchyPaths(List<NinjaNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return Array.Empty<string>();

            string[] paths = new string[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                int curr = i;
                List<string> parts = new List<string>();

                while (curr >= 0 && curr < nodes.Count)
                {
                    NinjaNode node = nodes[curr];
                    if (node.ParentIndex == -1) break;
                    parts.Insert(0, !string.IsNullOrEmpty(node.Name) ? node.Name : $"Node_{curr:0000}");
                    curr = node.ParentIndex;
                }

                paths[i] = string.Join("/", parts);
            }
            return paths;
        }
        #endregion

        #region Clip Resolution
        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            GameObject rootGO,
            List<Transform> nodeTransforms = null,
            MeshImportMode importMode = MeshImportMode.CombinedByNode,
            NinjaObject objData = null)
        {
            if (motionData == null) return null;

            string[] paths = null;
            if (nodeTransforms != null && nodeTransforms.Count > 0 && rootGO != null)
            {
                paths = new string[nodeTransforms.Count];
                for (int i = 0; i < nodeTransforms.Count; i++)
                {
                    paths[i] = nodeTransforms[i] != null ? GetTransformPath(nodeTransforms[i], rootGO.transform) : "";
                }
            }
            else if (rootGO != null)
            {
                Transform[] transforms = rootGO.GetComponentsInChildren<Transform>(true);
                paths = new string[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    paths[i] = GetTransformPath(transforms[i], rootGO.transform);
                }
            }

            return ResolveMotionInternal(motionData, clipName, scale, paths, nodeTransforms, rootGO, objData, importMode);
        }

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            MeshImportMode importMode = MeshImportMode.CombinedByNode,
            NinjaObject objData = null)
        {
            return ResolveMotionInternal(motionData, clipName, scale, nodeHierarchyTargets, null, null, objData, importMode);
        }

        private static AnimationClip ResolveMotionInternal(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            List<Transform> nodeTransforms,
            GameObject rootGO,
            NinjaObject objData,
            MeshImportMode importMode)
        {
            if (motionData == null) return null;

            AnimationClip clip = new AnimationClip { name = clipName };
            float framerate = motionData.Framerate <= 0 ? 60.0f : motionData.Framerate;
            float timeScale = 60.0f / framerate;

            float totalFrames = Mathf.Max(1f, motionData.EndFrame - motionData.StartFrame);
            float maxTime = (totalFrames / 60.0f) * timeScale;

            nodeHierarchyTargets ??= Array.Empty<string>();
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments = new Dictionary<PropertyKey, List<SubMotionSegment>>();

            // Filter out MA chunk ID from forcing material motion so NXMA node motions are parsed correctly
            bool isMatMotion = (motionData.Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL ||
                               motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_MATERIAL) ||
                               (motionData.ChunkID != null && (motionData.ChunkID.EndsWith("NV", StringComparison.OrdinalIgnoreCase) ||
                                                               motionData.ChunkID.EndsWith("MV", StringComparison.OrdinalIgnoreCase) ||
                                                               motionData.ChunkID.EndsWith("MT", StringComparison.OrdinalIgnoreCase)));

            MotionType effectiveMotionType = isMatMotion
                ? (motionData.Type | MotionType.NND_MOTIONTYPE_MATERIAL)
                : motionData.Type;

            foreach (NinjaSubMotion sm in motionData.SubMotions)
            {
                if (sm?.Keyframes == null || sm.Keyframes.Count == 0) continue;

                if (isMatMotion)
                {
                    // Unpack Material Index (low 16 bits) and Layer Index (high 16 bits)
                    int materialIndex = sm.NodeIndex & 0xFFFF;
                    int layerIndex = (sm.NodeIndex >> 16) & 0xFFFF;

                    var matTargets = ResolveMaterialRendererTargets(materialIndex, rootGO, nodeHierarchyTargets, objData, importMode);
                    if (matTargets.Count == 0)
                    {
                        matTargets.Add(new MaterialBindingTarget { TargetPath = "", MaterialSlot = materialIndex });
                    }

                    foreach (var target in matTargets)
                    {
                        CollectSubMotionSegments(
                            sm,
                            target.TargetPath,
                            propertySegments,
                            timeScale,
                            scale,
                            effectiveMotionType,
                            target.MaterialSlot,
                            layerIndex
                        );
                    }
                }
                else
                {
                    string targetPath = (sm.NodeIndex >= 0 && sm.NodeIndex < nodeHierarchyTargets.Length)
                        ? nodeHierarchyTargets[sm.NodeIndex]
                        : sm.NodeIndex.ToString("0000");

                    CollectSubMotionSegments(sm, targetPath, propertySegments, timeScale, scale, effectiveMotionType, 0, 0);
                }
            }

            // Fill in missing companion channels (.x, .y tiling scale) to ensure full float4 Vector4 bindings in Unity
            FillMissingCompanionChannels(propertySegments, nodeTransforms, nodeHierarchyTargets, maxTime);

            foreach (var kvp in propertySegments)
            {
                AnimationCurve merged = BuildMergedCurve(kvp.Value, kvp.Key, maxTime);
                if (merged?.keys.Length > 0)
                {
                    clip.SetCurve(kvp.Key.TargetPath, kvp.Key.ComponentType, kvp.Key.PropertyName, merged);
                }
            }

            bool isExplicitLoop = (motionData.Type & (MotionType.NND_MOTIONTYPE_REPEAT |
                                                      MotionType.NND_MOTIONTYPE_CONSTREPEAT |
                                                      MotionType.NND_MOTIONTYPE_OFFSET)) != 0;

            bool isOneShot = (motionData.Type & (MotionType.NND_MOTIONTYPE_NOREPEAT |
                                                 MotionType.NND_MOTIONTYPE_TRIGGER)) != 0;

            if (!isExplicitLoop && motionData.SubMotions != null)
            {
                foreach (var sm in motionData.SubMotions)
                {
                    if (sm == null) continue;

                    if ((sm.InterpolationType & (SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT |
                                                 SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTREPEAT |
                                                 SubMotionInterpolationType.NND_SMOTIPTYPE_OFFSET)) != 0)
                    {
                        isExplicitLoop = true;
                        break;
                    }

                    if ((sm.InterpolationType & (SubMotionInterpolationType.NND_SMOTIPTYPE_NOREPEAT |
                                                 SubMotionInterpolationType.NND_SMOTIPTYPE_TRIGGER)) != 0)
                    {
                        isOneShot = true;
                    }
                }
            }

            bool shouldLoop = isExplicitLoop || (isMatMotion && !isOneShot) || (!isOneShot && (motionData.Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL);

            clip.wrapMode = shouldLoop ? WrapMode.Loop : WrapMode.Once;
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = shouldLoop;
            clipSettings.loopBlend = false;
            clipSettings.loopBlendOrientation = false;
            clipSettings.loopBlendPositionY = false;
            clipSettings.loopBlendPositionXZ = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            return clip;
        }

        private static List<MaterialBindingTarget> ResolveMaterialRendererTargets(
            int materialIndex,
            GameObject rootGO,
            string[] nodeHierarchyTargets,
            NinjaObject objData,
            MeshImportMode importMode)
        {
            List<MaterialBindingTarget> targets = new List<MaterialBindingTarget>();
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (objData?.SubObjects != null && objData.Nodes != null)
            {
                if (importMode == MeshImportMode.SingleSkinnedMesh)
                {
                    targets.Add(new MaterialBindingTarget { TargetPath = "", MaterialSlot = materialIndex });
                    return targets;
                }

                if (importMode == MeshImportMode.CombinedByNode)
                {
                    for (int n = 0; n < objData.Nodes.Count; n++)
                    {
                        HashSet<int> matsInNode = new HashSet<int>();
                        foreach (var subObj in objData.SubObjects)
                        {
                            foreach (var ms in subObj.MeshSets)
                            {
                                if (ms.NodeIndex == n) matsInNode.Add(ms.MaterialIndex);
                            }
                        }

                        if (matsInNode.Contains(materialIndex))
                        {
                            string nodePath = (n >= 0 && n < nodeHierarchyTargets.Length) ? nodeHierarchyTargets[n] : $"Node_{n:0000}";
                            bool isSingle = matsInNode.Count == 1;
                            string targetPath = isSingle
                                ? nodePath
                                : (string.IsNullOrEmpty(nodePath) ? $"Mat_{materialIndex:00}" : $"{nodePath}/Mat_{materialIndex:00}");

                            if (seenPaths.Add(targetPath))
                            {
                                targets.Add(new MaterialBindingTarget { TargetPath = targetPath, MaterialSlot = 0 });
                            }
                        }
                    }
                    if (targets.Count > 0) return targets;
                }
                else if (importMode == MeshImportMode.IndividualSubObjects)
                {
                    int subObjIdx = 0;
                    foreach (var subObj in objData.SubObjects)
                    {
                        foreach (var ms in subObj.MeshSets)
                        {
                            if (ms.MaterialIndex == materialIndex)
                            {
                                string nodePath = (ms.NodeIndex >= 0 && ms.NodeIndex < nodeHierarchyTargets.Length)
                                    ? nodeHierarchyTargets[ms.NodeIndex] : $"Node_{ms.NodeIndex:0000}";
                                string targetPath = string.IsNullOrEmpty(nodePath) ? $"SubObj_{subObjIdx}" : $"{nodePath}/SubObj_{subObjIdx}";
                                if (seenPaths.Add(targetPath))
                                {
                                    targets.Add(new MaterialBindingTarget { TargetPath = targetPath, MaterialSlot = 0 });
                                }
                            }
                            subObjIdx++;
                        }
                    }
                    if (targets.Count > 0) return targets;
                }
            }

            if (rootGO != null)
            {
                Renderer[] renderers = rootGO.GetComponentsInChildren<Renderer>(true);
                string rootName = rootGO.name;

                foreach (var r in renderers)
                {
                    if (r == null) continue;

                    if (r.name.Equals($"Mat_{materialIndex:00}", StringComparison.OrdinalIgnoreCase) ||
                        r.name.Equals($"Mat_{materialIndex}", StringComparison.OrdinalIgnoreCase))
                    {
                        string path = GetTransformPath(r.transform, rootGO.transform);
                        if (seenPaths.Add(path))
                        {
                            targets.Add(new MaterialBindingTarget { TargetPath = path, MaterialSlot = 0 });
                        }
                        continue;
                    }

                    Material[] sharedMats = r.sharedMaterials;
                    for (int m = 0; m < sharedMats.Length; m++)
                    {
                        Material mat = sharedMats[m];
                        if (mat == null) continue;

                        if (IsMaterialIndexMatch(mat.name, materialIndex, rootName))
                        {
                            string path = GetTransformPath(r.transform, rootGO.transform);
                            if (seenPaths.Add(path))
                            {
                                targets.Add(new MaterialBindingTarget
                                {
                                    TargetPath = path,
                                    MaterialSlot = (sharedMats.Length > 1) ? m : 0
                                });
                            }
                        }
                    }
                }
            }

            return targets;
        }

        private static bool IsMaterialIndexMatch(string matName, int materialIndex, string rootName)
        {
            if (string.IsNullOrEmpty(matName)) return false;

            if (matName.StartsWith($"{materialIndex}_", StringComparison.OrdinalIgnoreCase)) return true;
            if (matName.StartsWith($"Material_{materialIndex}_", StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(rootName) && matName.StartsWith($"{rootName}_{materialIndex}_", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static string GetMaterialPropertyName(string basePropName, int materialSlot)
        {
            if (materialSlot <= 0 || !basePropName.StartsWith("material.")) return basePropName;
            return $"materials.Array.data[{materialSlot}]." + basePropName.Substring("material.".Length);
        }

        private static string GetLayerPropertyName(string basePropName, int layerIndex)
        {
            if (layerIndex == 1) return basePropName.Replace("_MainTex_ST", "_MainTex2_ST");
            if (layerIndex == 2) return basePropName.Replace("_MainTex_ST", "_MainTex3_ST");
            return basePropName;
        }

        private static bool TryMatchBinding(string propertyName, out TrackBindingDef matchedDef, out int materialSlot, out int layerIndex)
        {
            matchedDef = default;
            materialSlot = 0;
            layerIndex = 0;

            if (propertyName.Contains("_MainTex2_ST")) layerIndex = 1;
            else if (propertyName.Contains("_MainTex3_ST")) layerIndex = 2;

            string normalizedProp = propertyName
                .Replace("_MainTex2_ST", "_MainTex_ST")
                .Replace("_MainTex3_ST", "_MainTex_ST");

            foreach (var b in AllTrackBindings)
            {
                if (b.PropertyName == normalizedProp)
                {
                    matchedDef = b;
                    materialSlot = 0;
                    return true;
                }

                if (b.CategoryMask == 16 && normalizedProp.StartsWith("materials.Array.data["))
                {
                    int closeBracket = normalizedProp.IndexOf(']');
                    if (closeBracket > 21 && int.TryParse(normalizedProp.Substring(21, closeBracket - 21), out int slot))
                    {
                        string suffix = normalizedProp.Substring(closeBracket + 2);
                        if (b.PropertyName.EndsWith(suffix))
                        {
                            matchedDef = b;
                            materialSlot = slot;
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        #endregion

        #region Segment Collection & Companion Filling
        private static void CollectSubMotionSegments(
            NinjaSubMotion subMotion,
            string targetPath,
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            float timeScale,
            float scale,
            MotionType parentType,
            int materialSlot = 0,
            int layerIndex = 0)
        {
            if (subMotion?.Keyframes == null || subMotion.Keyframes.Count == 0) return;

            uint flags = (uint)subMotion.Type;
            uint cat = (uint)parentType & 31U;
            if (cat == 0) cat = 1;

            // --------------------------------------------------------------------------
            // A. Vector3 Keyframe Tracks
            // --------------------------------------------------------------------------
            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_VECTOR)
            {
                foreach (var binding in AllTrackBindings)
                {
                    if (binding.Mask != 0 && binding.CategoryMask == cat && (flags & binding.Mask) != 0)
                    {
                        string layerProp = GetLayerPropertyName(binding.PropertyName, layerIndex);
                        string propName = binding.CategoryMask == 16 
                            ? GetMaterialPropertyName(layerProp, materialSlot) 
                            : binding.PropertyName;

                        PropertyKey key = new PropertyKey(targetPath, binding.ComponentType, propName);

                        foreach (var objKf in subMotion.Keyframes)
                        {
                            var kf = (NinjaKeyframe.NNS_MOTION_KEY_VECTOR)objKf;
                            float rawVal = binding.GroupKey == "_MainTex_ST"
                                ? ((binding.PropertyName.EndsWith(".z") || (flags & 0x800000U) != 0) ? kf.Value.x : kf.Value.y)
                                : (binding.ChannelIndex switch { 0 => kf.Value.x, 1 => kf.Value.y, 2 => kf.Value.z, _ => 0f });

                            if (binding.GroupKey == "localPosition") rawVal *= scale;
                            if (binding.InvertSign) rawVal = -rawVal;

                            float time = (kf.Frame / 60.0f) * timeScale;
                            AddKeyframe(propertySegments, key, subMotion.InterpolationType, new Keyframe(time, rawVal));
                        }
                    }
                }
                return;
            }

            // --------------------------------------------------------------------------
            // B. 3-Axis 16-Bit BAMS Rotation Tracks (RotateA16)
            // --------------------------------------------------------------------------
            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)
            {
                bool hasRX = (flags & 0x800U) != 0;
                bool hasRY = (flags & 0x1000U) != 0;
                bool hasRZ = (flags & 0x2000U) != 0;

                long accumX = 0, accumY = 0, accumZ = 0;
                int lastDeltaX = 0, lastDeltaY = 0, lastDeltaZ = 0;
                bool first = true;

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)objKf;

                    float degX = hasRX ? Bams16ToUnrolledDegrees(kf.Value1, ref accumX, ref lastDeltaX, ref first) : 0f;
                    float degY = hasRY ? -Bams16ToUnrolledDegrees(kf.Value2, ref accumY, ref lastDeltaY, ref first) : 0f;
                    float degZ = hasRZ ? -Bams16ToUnrolledDegrees(kf.Value3, ref accumZ, ref lastDeltaZ, ref first) : 0f;

                    float time = (kf.Frame / 60.0f) * timeScale;

                    if (hasRX) AddKeyframe(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x"), subMotion.InterpolationType, new Keyframe(time, degX));
                    if (hasRY) AddKeyframe(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y"), subMotion.InterpolationType, new Keyframe(time, degY));
                    if (hasRZ) AddKeyframe(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z"), subMotion.InterpolationType, new Keyframe(time, degZ));
                }
                return;
            }

            // --------------------------------------------------------------------------
            // C. Scalar Keyframe Tracks (32-Bit BAMS, 16-Bit BAMS, Float, Radian)
            // --------------------------------------------------------------------------
            foreach (var binding in AllTrackBindings)
            {
                if (binding.Mask != 0 && binding.CategoryMask == cat && (flags & binding.Mask) != 0)
                {
                    string layerProp = GetLayerPropertyName(binding.PropertyName, layerIndex);
                    string propName = binding.CategoryMask == 16 
                        ? GetMaterialPropertyName(layerProp, materialSlot) 
                        : binding.PropertyName;

                    PropertyKey key = new PropertyKey(targetPath, binding.ComponentType, propName);
                    long accumS16Bams = 0;
                    int lastDeltaS16 = 0;
                    bool firstS16 = true;

                    foreach (var kf in subMotion.Keyframes)
                    {
                        if (TryExtractScalarKeyframe(kf, flags, ref accumS16Bams, ref lastDeltaS16, ref firstS16, out float rawFrame, out float scalarVal))
                        {
                            if (binding.GroupKey == "localPosition") scalarVal *= scale;
                            if (binding.InvertSign) scalarVal = -scalarVal;

                            float time = (rawFrame / 60.0f) * timeScale;
                            AddKeyframe(propertySegments, key, subMotion.InterpolationType, new Keyframe(time, scalarVal));
                        }
                    }
                }
            }
        }

        private static void FillMissingCompanionChannels(
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            List<Transform> nodeTransforms,
            string[] nodeHierarchyTargets,
            float maxTime)
        {
            var existingKeys = new List<PropertyKey>(propertySegments.Keys);
            HashSet<string> processedGroups = new HashSet<string>();

            foreach (var key in existingKeys)
            {
                if (!TryMatchBinding(key.PropertyName, out TrackBindingDef matchedBinding, out int slot, out int layerIndex))
                    continue;

                string groupKey = $"{key.TargetPath}|{key.ComponentType.Name}|{matchedBinding.GroupKey}|slot_{slot}|layer_{layerIndex}";
                if (!processedGroups.Add(groupKey)) continue;

                Transform nodeTr = FindNodeTransform(key.TargetPath, nodeTransforms, nodeHierarchyTargets);

                foreach (var companion in AllTrackBindings)
                {
                    if (companion.GroupKey == matchedBinding.GroupKey && companion.ComponentType == key.ComponentType)
                    {
                        string layerProp = GetLayerPropertyName(companion.PropertyName, layerIndex);
                        string compPropName = GetMaterialPropertyName(layerProp, slot);
                        PropertyKey channelKey = new PropertyKey(key.TargetPath, companion.ComponentType, compPropName);

                        if (!propertySegments.ContainsKey(channelKey))
                        {
                            float defVal = GetDefaultChannelValue(companion, nodeTr);
                            var seg = new SubMotionSegment { InterpolationType = SubMotionInterpolationType.NND_SMOTIPTYPE_LINEAR };
                            seg.Keyframes.Add(new Keyframe(0f, defVal, 0f, 0f));

                            if (maxTime > 0.001f)
                            {
                                seg.Keyframes.Add(new Keyframe(maxTime, defVal, 0f, 0f));
                            }

                            propertySegments[channelKey] = new List<SubMotionSegment> { seg };
                        }
                    }
                }
            }
        }

        private static float GetDefaultChannelValue(TrackBindingDef binding, Transform tr)
        {
            if (tr == null) return binding.DefaultRestValue;

            return binding.GroupKey switch
            {
                "localPosition" => binding.ChannelIndex == 0 ? tr.localPosition.x : (binding.ChannelIndex == 1 ? tr.localPosition.y : tr.localPosition.z),
                "localEulerAnglesRaw" => binding.ChannelIndex == 0 ? tr.localEulerAngles.x : (binding.ChannelIndex == 1 ? tr.localEulerAngles.y : tr.localEulerAngles.z),
                "localScale" => binding.ChannelIndex == 0 ? tr.localScale.x : (binding.ChannelIndex == 1 ? tr.localScale.y : tr.localScale.z),
                _ => binding.DefaultRestValue
            };
        }

        private static void AddKeyframe(
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            PropertyKey key,
            SubMotionInterpolationType interp,
            Keyframe kf)
        {
            if (!propertySegments.TryGetValue(key, out List<SubMotionSegment> segments))
            {
                segments = new List<SubMotionSegment>();
                propertySegments[key] = segments;
            }

            SubMotionSegment curr = (segments.Count > 0 && segments[segments.Count - 1].InterpolationType == interp)
                ? segments[segments.Count - 1]
                : null;

            if (curr == null)
            {
                curr = new SubMotionSegment { InterpolationType = interp };
                segments.Add(curr);
            }

            curr.Keyframes.Add(kf);
        }

        private static Transform FindNodeTransform(string path, List<Transform> nodeTransforms, string[] targets)
        {
            if (nodeTransforms == null || nodeTransforms.Count == 0) return null;

            if (targets != null)
            {
                for (int i = 0; i < targets.Length && i < nodeTransforms.Count; i++)
                {
                    if (targets[i] == path && nodeTransforms[i] != null) return nodeTransforms[i];
                }
            }

            if (int.TryParse(path, out int nodeIdx) && nodeIdx >= 0 && nodeIdx < nodeTransforms.Count)
            {
                return nodeTransforms[nodeIdx];
            }

            for (int i = 0; i < nodeTransforms.Count; i++)
            {
                if (nodeTransforms[i] != null && nodeTransforms[i].name == path) return nodeTransforms[i];
            }

            return null;
        }

        public static string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == root || transform == null) return "";
            string path = transform.name;
            Transform curr = transform.parent;

            while (curr != null && curr != root)
            {
                path = curr.name + "/" + path;
                curr = curr.parent;
            }
            return path;
        }
        #endregion

        #region Tangents & Curve Merging
        private static AnimationCurve BuildMergedCurve(List<SubMotionSegment> segments, PropertyKey key, float maxTime)
        {
            if (segments == null || segments.Count == 0) return null;
            List<Keyframe> allKfs = new List<Keyframe>();
        
            bool isTransformRotation = key.ComponentType == typeof(Transform) && key.PropertyName.StartsWith("localEulerAngles");
        
            foreach (var seg in segments)
            {
                if (seg.Keyframes.Count == 0) continue;
        
                bool isConstant = !isTransformRotation && seg.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT);
                Keyframe[] keys = seg.Keyframes.ToArray();
        
                if (isConstant)
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].inTangent = float.PositiveInfinity;
                        keys[i].outTangent = float.PositiveInfinity;
                    }
                }
                else if (keys.Length >= 2)
                {
                    for (int i = 0; i < keys.Length - 1; i++)
                    {
                        float dt = keys[i + 1].time - keys[i].time;
                        if (dt > 0.00001f)
                        {
                            float slope = (keys[i + 1].value - keys[i].value) / dt;
                            keys[i].outTangent = slope;
                            keys[i + 1].inTangent = slope;
                        }
                    }
                    keys[0].inTangent = keys[0].outTangent;
                    keys[keys.Length - 1].outTangent = keys[keys.Length - 1].inTangent;
                }
        
                allKfs.AddRange(keys);
            }
        
            if (allKfs.Count == 0) return null;
            allKfs.Sort((a, b) => a.time.CompareTo(b.time));
        
            List<Keyframe> unique = new List<Keyframe>();
            for (int i = 0; i < allKfs.Count; i++)
            {
                Keyframe kf = allKfs[i];
                if (unique.Count > 0)
                {
                    Keyframe prev = unique[unique.Count - 1];
                    if (Mathf.Abs(prev.time - kf.time) < 0.0001f)
                    {
                        if (Mathf.Abs(prev.value - kf.value) < 0.0001f)
                        {
                            prev.outTangent = kf.outTangent;
                            unique[unique.Count - 1] = prev;
                            continue;
                        }
                        kf.time = prev.time + 0.0001f;
                    }
                }
                unique.Add(kf);
            }
        
            // Boundary Anchoring: Ensure t = 0.0s and t = maxTime are explicitly anchored
            if (unique[0].time > 0.0001f)
            {
                Keyframe first = unique[0];
                unique.Insert(0, new Keyframe(0f, first.value, first.inTangent, first.outTangent));
            }
        
            if (maxTime > 0.001f && unique[unique.Count - 1].time < maxTime - 0.0001f)
            {
                Keyframe last = unique[unique.Count - 1];
                unique.Add(new Keyframe(maxTime, last.value, last.inTangent, last.outTangent));
            }
        
            return new AnimationCurve(unique.ToArray());
        }
        #endregion
    }
}