// File: Marathon/UnityParsers/NinjaMotionResolver.cs
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    /// <summary>
    /// Converts Sega NN BAMS/Radian motion tracks into native Unity AnimationClip curves.
    /// Handles node bone transformations, UV scrolling tracks, and material color animations.
    /// </summary>
    public static class NinjaMotionResolver
    {
        private static readonly string[] MotionExtensions = new[] { ".xnm", ".gnm", ".znm" };
        private static readonly string[] MaterialMotionExtensions = new[] { ".xnv", ".gnv", ".znv" };
        private static readonly string[] ModelExtensions = new[] { ".xna", ".xnn", ".xnj", ".xno", ".gna", ".gnn", ".gno" };

        #region Angle Conversion Helpers
        /// <summary>
        /// Converts a 16-bit Binary Angle Measurement System (BAMS) short to degrees.
        /// Range: 32768 = 180 degrees, 65536 = 360 degrees.
        /// </summary>
        public static float BamsToDegrees(int bamAngle) => (float)((double)bamAngle * (180.0 / 32768.0));

        /// <summary>
        /// Converts a 32-bit BAMS integer to degrees.
        /// </summary>
        public static float Bams32ToDegrees(int bam32Angle) => (float)((double)bam32Angle * (360.0 / 65536.0));

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        public static float RadiansToDegrees(float radAngle) => radAngle * Mathf.Rad2Deg;
        #endregion

        private readonly struct PropertyKey : IEquatable<PropertyKey>
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

            public bool Equals(PropertyKey other)
            {
                return TargetPath == other.TargetPath &&
                       ComponentType == other.ComponentType &&
                       PropertyName == other.PropertyName;
            }

            public override bool Equals(object obj) => obj is PropertyKey other && Equals(other);

            public override int GetHashCode()
            {
                return HashCode.Combine(TargetPath, ComponentType, PropertyName);
            }
        }

        private class SubMotionSegment
        {
            public SubMotionInterpolationType InterpolationType;
            public readonly List<Keyframe> Keyframes = new List<Keyframe>();
        }

        #region Motion Discovery & Linking
        /// <summary>
        /// Auto-discovers and loads adjacent .xnm (node motion) and .xnv (material motion) files.
        /// </summary>
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

        /// <summary>
        /// Resolves the bone/node hierarchy paths for an asset to bind AnimationClip tracks correctly.
        /// </summary>
        public static string[] ResolveNodeHierarchyTargets(string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
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

                        if (loader.Data.Object?.Nodes != null && loader.Data.Object.Nodes.Count > 0)
                        {
                            ctx?.DependsOnSourceAsset(candidate);
                            return ComputeNodeHierarchyPaths(loader.Data.Object.Nodes);
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
        /// <summary>
        /// Converts a NinjaMotion chunk into a native Unity AnimationClip bound to GameObject hierarchies.
        /// </summary>
        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            GameObject rootGO,
            List<Transform> nodeTransforms = null,
            MeshImportMode importMode = MeshImportMode.CombinedByNode)
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

            return ResolveMotionInternal(motionData, clipName, scale, paths, nodeTransforms, rootGO);
        }

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            MeshImportMode importMode = MeshImportMode.CombinedByNode)
        {
            return ResolveMotionInternal(motionData, clipName, scale, nodeHierarchyTargets, null, null);
        }

        private static AnimationClip ResolveMotionInternal(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            List<Transform> nodeTransforms,
            GameObject rootGO)
        {
            if (motionData == null) return null;

            AnimationClip clip = new AnimationClip { name = clipName };
            float framerate = motionData.Framerate <= 0 ? 60.0f : motionData.Framerate;
            float timeScale = 60.0f / framerate;
            float maxTime = (motionData.EndFrame / 60.0f) * timeScale;

            nodeHierarchyTargets ??= Array.Empty<string>();
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments = new Dictionary<PropertyKey, List<SubMotionSegment>>();
            bool isMatMotion = (motionData.Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;

            foreach (NinjaSubMotion sm in motionData.SubMotions)
            {
                if (sm?.Keyframes == null || sm.Keyframes.Count == 0) continue;

                string targetPath = !isMatMotion
                    ? ((sm.NodeIndex >= 0 && sm.NodeIndex < nodeHierarchyTargets.Length && !string.IsNullOrEmpty(nodeHierarchyTargets[sm.NodeIndex]))
                        ? nodeHierarchyTargets[sm.NodeIndex] : sm.NodeIndex.ToString("0000"))
                    : ResolveMaterialRendererPath(sm.NodeIndex, rootGO);

                CollectSubMotionSegments(sm, targetPath, propertySegments, timeScale, scale, motionData.Type);
            }

            // Fill companion X/Y/Z channels so un-animated axes stay locked to their rest poses
            FillMissingCompanionChannels(propertySegments, nodeTransforms, nodeHierarchyTargets, maxTime);

            foreach (var kvp in propertySegments)
            {
                AnimationCurve merged = BuildMergedCurve(kvp.Value);
                if (merged?.keys.Length > 0)
                {
                    clip.SetCurve(kvp.Key.TargetPath, kvp.Key.ComponentType, kvp.Key.PropertyName, merged);
                }
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_CONSTREPEAT) || motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_REPEAT);
            settings.loopBlend = motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_REPEAT);
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        private static string ResolveMaterialRendererPath(int materialIndex, GameObject rootGO)
        {
            if (rootGO == null) return "";

            foreach (var r in rootGO.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && (r.name.Contains($"Mat_{materialIndex:00}") || r.name.Contains($"Mat_{materialIndex}")))
                {
                    return GetTransformPath(r.transform, rootGO.transform);
                }
            }
            return "";
        }
        #endregion

        #region Segment Collection & Companion Filling
        private static void CollectSubMotionSegments(
            NinjaSubMotion subMotion,
            string targetPath,
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            float timeScale,
            float scale,
            MotionType parentType)
        {
            bool isNode = (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) is MotionType.NND_MOTIONTYPE_NODE or 0;
            bool isMat = (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;
            uint flags = (uint)subMotion.Type;

            // 1. Vector3 Tracks (Translation, Scale, Diffuse RGB)
            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_VECTOR)
            {
                bool hasTranslation = isNode && (flags & 0x700U) != 0;
                bool hasScale = isNode && (flags & 0x38000U) != 0;
                bool hasColor = isMat && (flags & 0xE00U) != 0;

                string prefix = hasTranslation ? "localPosition" : (hasScale ? "localScale" : "material._Color");
                Type compType = isNode ? typeof(Transform) : typeof(Renderer);
                string[] suffixes = isNode ? new[] { ".x", ".y", ".z" } : new[] { ".r", ".g", ".b" };

                bool[] active = hasTranslation
                    ? new[] { (flags & 0x100U) != 0, (flags & 0x200U) != 0, (flags & 0x400U) != 0 }
                    : (hasScale
                        ? new[] { (flags & 0x8000U) != 0, (flags & 0x10000U) != 0, (flags & 0x20000U) != 0 }
                        : new[] { (flags & 0x200U) != 0, (flags & 0x400U) != 0, (flags & 0x800U) != 0 });

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_VECTOR)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;
                    Vector3 val = hasTranslation
                        ? new Vector3(-kf.Value.x * scale, kf.Value.y * scale, kf.Value.z * scale)
                        : kf.Value;

                    for (int c = 0; c < 3; c++)
                    {
                        if (active[c])
                        {
                            PropertyKey key = new PropertyKey(targetPath, compType, prefix + suffixes[c]);
                            float channelVal = c == 0 ? val.x : (c == 1 ? val.y : val.z);
                            AddKeyframe(propertySegments, key, subMotion.InterpolationType, new Keyframe(time, channelVal));
                        }
                    }
                }
                return;
            }

            // 2. 3-Axis BAMS Rotation Tracks (RotateA16)
            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)
            {
                bool hasRX = (flags & 0x800U) != 0;
                bool hasRY = (flags & 0x1000U) != 0;
                bool hasRZ = (flags & 0x2000U) != 0;

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;

                    if (hasRX) AddKeyframe(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x"), subMotion.InterpolationType, new Keyframe(time, BamsToDegrees(kf.Value1)));
                    if (hasRY) AddKeyframe(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y"), subMotion.InterpolationType, new Keyframe(time, -BamsToDegrees(kf.Value2)));
                    if (hasRZ) AddKeyframe(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z"), subMotion.InterpolationType, new Keyframe(time, -BamsToDegrees(kf.Value3)));
                }
                return;
            }

            // 3. Scalar Tracks (Single Axis Translation, Rotation, UV Offset)
            List<PropertyKey> keys = GetTargetPropertyKeys(subMotion.Type, parentType, targetPath);
            if (keys.Count == 0) return;

            foreach (var kf in subMotion.Keyframes)
            {
                float time = 0f;
                float scalar = 0f;

                if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32)
                {
                    time = (s32.Frame / 60f) * timeScale;
                    scalar = (flags & 8U) != 0 ? Bams32ToDegrees(s32.Value) : s32.Value;
                }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT f)
                {
                    time = (f.Frame / 60f) * timeScale;
                    scalar = (flags & 4U) != 0 ? RadiansToDegrees(f.Value) : f.Value;
                }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16)
                {
                    time = (s16.Frame / 60f) * timeScale;
                    scalar = BamsToDegrees(s16.Value);
                }

                foreach (var key in keys)
                {
                    float val = scalar;
                    if (key.PropertyName.Contains("Position")) val *= scale;
                    if (key.PropertyName.EndsWith(".x") || key.PropertyName.EndsWith("AnglesRaw.y") || key.PropertyName.EndsWith("AnglesRaw.z")) val *= -1f;

                    AddKeyframe(propertySegments, key, subMotion.InterpolationType, new Keyframe(time, val));
                }
            }
        }

        private static List<PropertyKey> GetTargetPropertyKeys(SubMotionType subType, MotionType parentType, string path)
        {
            List<PropertyKey> keys = new List<PropertyKey>();
            bool isNode = (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) is MotionType.NND_MOTIONTYPE_NODE or 0;
            bool isMat = (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;
            uint val = (uint)subType;

            if (isNode)
            {
                if ((val & 0x100U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localPosition.x"));
                if ((val & 0x200U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localPosition.y"));
                if ((val & 0x400U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localPosition.z"));

                if ((val & 0x800U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localEulerAnglesRaw.x"));
                if ((val & 0x1000U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localEulerAnglesRaw.y"));
                if ((val & 0x2000U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localEulerAnglesRaw.z"));

                if ((val & 0x8000U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localScale.x"));
                if ((val & 0x10000U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localScale.y"));
                if ((val & 0x20000U) != 0) keys.Add(new PropertyKey(path, typeof(Transform), "localScale.z"));
            }
            else if (isMat)
            {
                if ((val & 0x200U) != 0) keys.Add(new PropertyKey(path, typeof(Renderer), "material._Color.r"));
                if ((val & 0x400U) != 0) keys.Add(new PropertyKey(path, typeof(Renderer), "material._Color.g"));
                if ((val & 0x800U) != 0) keys.Add(new PropertyKey(path, typeof(Renderer), "material._Color.b"));
                if ((val & 0x1000U) != 0) keys.Add(new PropertyKey(path, typeof(Renderer), "material._Color.a"));

                // UV Offset Tracks
                if ((val & 0x800000U) != 0) keys.Add(new PropertyKey(path, typeof(Renderer), "material._MainTex_ST.z"));
                if ((val & 0x1000000U) != 0) keys.Add(new PropertyKey(path, typeof(Renderer), "material._MainTex_ST.w"));
            }
            return keys;
        }

        private static void FillMissingCompanionChannels(
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            List<Transform> nodeTransforms,
            string[] nodeHierarchyTargets,
            float maxTime)
        {
            var existingKeys = new List<PropertyKey>(propertySegments.Keys);
            Regex vectorPattern = new Regex(@"^(.*?)(localPosition|localEulerAnglesRaw|localScale|_MainTex_ST|_Color|_SpecColor|_EmissionColor)(\.[xyzw|rgba])$");
            HashSet<string> processedGroups = new HashSet<string>();

            foreach (var key in existingKeys)
            {
                Match m = vectorPattern.Match(key.PropertyName);
                if (!m.Success) continue;

                string propBase = m.Groups[1].Value;
                string propType = m.Groups[2].Value;
                string groupKey = $"{key.TargetPath}|{key.ComponentType.Name}|{propBase}{propType}";

                if (!processedGroups.Add(groupKey)) continue;

                Transform nodeTr = FindNodeTransform(key.TargetPath, nodeTransforms, nodeHierarchyTargets);
                string[] suffixes = (propType is "_Color" or "_SpecColor" or "_EmissionColor")
                    ? new[] { ".r", ".g", ".b", ".a" }
                    : (propType == "_MainTex_ST" ? new[] { ".x", ".y", ".z", ".w" } : new[] { ".x", ".y", ".z" });

                for (int s = 0; s < suffixes.Length; s++)
                {
                    PropertyKey channelKey = new PropertyKey(key.TargetPath, key.ComponentType, propBase + propType + suffixes[s]);
                    if (!propertySegments.ContainsKey(channelKey))
                    {
                        float defVal = GetDefaultChannelValue(propType, s, nodeTr);
                        var seg = new SubMotionSegment { InterpolationType = SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT };
                        seg.Keyframes.Add(new Keyframe(0f, defVal, float.PositiveInfinity, float.PositiveInfinity));

                        if (maxTime > 0.001f)
                        {
                            seg.Keyframes.Add(new Keyframe(maxTime, defVal, float.PositiveInfinity, float.PositiveInfinity));
                        }

                        propertySegments[channelKey] = new List<SubMotionSegment> { seg };
                    }
                }
            }
        }

        private static float GetDefaultChannelValue(string propType, int index, Transform tr)
        {
            switch (propType)
            {
                case "localPosition":
                    return tr != null ? (index == 0 ? tr.localPosition.x : (index == 1 ? tr.localPosition.y : tr.localPosition.z)) : 0f;
                case "localEulerAnglesRaw":
                    return tr != null ? (index == 0 ? tr.localEulerAngles.x : (index == 1 ? tr.localEulerAngles.y : tr.localEulerAngles.z)) : 0f;
                case "localScale":
                    return tr != null ? (index == 0 ? tr.localScale.x : (index == 1 ? tr.localScale.y : tr.localScale.z)) : 1f;
                case "_MainTex_ST":
                    return (index is 0 or 1) ? 1.0f : 0.0f; // Scale X/Y = 1.0, Offset Z/W = 0.0
                case "_Color":
                    return 1.0f;
                case "_SpecColor":
                case "_EmissionColor":
                    return index == 3 ? 1.0f : 0.0f; // Alpha = 1.0, RGB = 0.0
                default:
                    return 0f;
            }
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
            if (nodeTransforms == null) return null;

            for (int i = 0; i < nodeTransforms.Count; i++)
            {
                if (i < targets.Length && targets[i] == path) return nodeTransforms[i];
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
        private static AnimationCurve BuildMergedCurve(List<SubMotionSegment> segments)
        {
            if (segments == null || segments.Count == 0) return null;
            List<Keyframe> allKfs = new List<Keyframe>();

            foreach (var seg in segments)
            {
                if (seg.Keyframes.Count == 0) continue;

                bool isConstant = seg.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT);
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
                    // Linear tangent calculation across keyframe intervals
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

            // Remove co-located duplicate keyframes
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

            return new AnimationCurve(unique.ToArray());
        }
        #endregion
    }
}