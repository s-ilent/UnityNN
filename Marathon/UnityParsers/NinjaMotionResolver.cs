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
    public static class NinjaMotionResolver
    {
        private static readonly string[] MotionExtensions = new string[] { ".xnm", ".gnm", ".znm" };
        private static readonly string[] MaterialMotionExtensions = new string[] { ".xnv", ".gnv", ".znv" };
        private static readonly string[] ModelExtensions = new string[] { ".xna", ".xnn", ".xnj", ".xno", ".gna", ".gnn", ".gno" };

        public static float BamsToDegrees(int bamAngle) => (float)((double)bamAngle * (180.0 / 32768.0));
        public static float Bams32ToDegrees(int bam32Angle) => (float)((double)bam32Angle * (360.0 / 65536.0));
        public static float RadiansToDegrees(float radAngle) => radAngle * Mathf.Rad2Deg;

        private struct PropertyKey : IEquatable<PropertyKey>
        {
            public string TargetPath;
            public Type ComponentType;
            public string PropertyName;

            public PropertyKey(string targetPath, Type componentType, string propertyName)
            {
                TargetPath = targetPath ?? "";
                ComponentType = componentType;
                PropertyName = propertyName ?? "";
            }

            public bool Equals(PropertyKey other) =>
                TargetPath == other.TargetPath && ComponentType == other.ComponentType && PropertyName == other.PropertyName;

            public override bool Equals(object obj) => obj is PropertyKey other && Equals(other);

            public override int GetHashCode() =>
                unchecked(17 * 23 + TargetPath.GetHashCode() * 23 + (ComponentType != null ? ComponentType.GetHashCode() : 0) * 23 + PropertyName.GetHashCode());
        }

        private class SubMotionSegment
        {
            public SubMotionInterpolationType InterpolationType;
            public List<Keyframe> Keyframes = new List<Keyframe>();
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
            nodeMotion = null; matMotion = null;
            nodeMotionSource = "Embedded"; matMotionSource = "Embedded";
            if (string.IsNullOrEmpty(assetPath)) return;

            string baseDir = Path.GetDirectoryName(assetPath);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            nodeMotion = LoadLinkedMotion(baseDir, baseName, MotionExtensions, assetPath, ctx, out nodeMotionSource);
            matMotion = LoadLinkedMotion(baseDir, baseName, MaterialMotionExtensions, assetPath, ctx, out matMotionSource);
        }

        private static NinjaMotion LoadLinkedMotion(string baseDir, string baseName, string[] extensions, string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx, out string sourceDesc)
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
                            if (ctx != null) ctx.DependsOnSourceAsset(candidate);
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
            if (string.IsNullOrEmpty(assetPath)) return new string[0];
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
                            if (ctx != null) ctx.DependsOnSourceAsset(candidate);
                            return ComputeNodeHierarchyPaths(loader.Data.Object.Nodes);
                        }
                        if (loader.Data.NodeNameList?.NinjaNodeNames != null)
                        {
                            if (ctx != null) ctx.DependsOnSourceAsset(candidate);
                            return loader.Data.NodeNameList.NinjaNodeNames.ToArray();
                        }
                    }
                    catch { }
                }
            }
            return new string[0];
        }

        public static string[] ComputeNodeHierarchyPaths(List<NinjaNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return new string[0];
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
            MeshImportMode importMode = MeshImportMode.CombinedByNode)
        {
            if (motionData == null) return null;

            string[] paths = null;
            if (nodeTransforms != null && nodeTransforms.Count > 0 && rootGO != null)
            {
                paths = new string[nodeTransforms.Count];
                for (int i = 0; i < nodeTransforms.Count; i++)
                    paths[i] = nodeTransforms[i] != null ? GetTransformPath(nodeTransforms[i], rootGO.transform) : "";
            }
            else if (rootGO != null)
            {
                Transform[] transforms = rootGO.GetComponentsInChildren<Transform>(true);
                paths = new string[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                    paths[i] = GetTransformPath(transforms[i], rootGO.transform);
            }

            return ResolveMotionInternal(motionData, clipName, scale, paths, nodeTransforms, rootGO, importMode);
        }

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            MeshImportMode importMode = MeshImportMode.CombinedByNode)
        {
            return ResolveMotionInternal(motionData, clipName, scale, nodeHierarchyTargets, null, null, importMode);
        }

        private static AnimationClip ResolveMotionInternal(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            List<Transform> nodeTransforms,
            GameObject rootGO,
            MeshImportMode importMode)
        {
            if (motionData == null) return null;

            var clip = new AnimationClip { name = clipName };
            float framerate = motionData.Framerate <= 0 ? 60.0f : motionData.Framerate;
            float timeScale = 60.0f / framerate;
            float maxTime = (motionData.EndFrame / 60.0f) * timeScale;

            if (nodeHierarchyTargets == null) nodeHierarchyTargets = new string[0];
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments = new Dictionary<PropertyKey, List<SubMotionSegment>>();

            bool isMatMotion = (motionData.Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;

            foreach (NinjaSubMotion subMotion in motionData.SubMotions)
            {
                if (subMotion?.Keyframes == null || subMotion.Keyframes.Count == 0) continue;

                string targetPath = "";
                if (!isMatMotion)
                {
                    targetPath = (subMotion.NodeIndex >= 0 && subMotion.NodeIndex < nodeHierarchyTargets.Length && !string.IsNullOrEmpty(nodeHierarchyTargets[subMotion.NodeIndex]))
                        ? nodeHierarchyTargets[subMotion.NodeIndex] : subMotion.NodeIndex.ToString("0000");
                }
                else
                {
                    targetPath = ResolveMaterialRendererPath(subMotion.NodeIndex, rootGO);
                }

                CollectSubMotionSegments(subMotion, targetPath, propertySegments, timeScale, scale, motionData.Type);
            }

            FillMissingCompanionChannels(propertySegments, nodeTransforms, nodeHierarchyTargets, maxTime);

            foreach (var kvp in propertySegments)
            {
                AnimationCurve mergedCurve = BuildMergedCurve(kvp.Value);
                if (mergedCurve?.keys.Length > 0)
                {
                    clip.SetCurve(kvp.Key.TargetPath, kvp.Key.ComponentType, kvp.Key.PropertyName, mergedCurve);
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

            Renderer[] renderers = rootGO.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.name.Contains($"Mat_{materialIndex:00}") || r.name.Contains($"Mat_{materialIndex}"))
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
            bool isNode = (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_NODE || (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == 0;
            bool isMat = (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;
            uint flags = (uint)subMotion.Type;

            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_VECTOR)
            {
                bool hasTX = isNode && (flags & 0x100U) != 0, hasTY = isNode && (flags & 0x200U) != 0, hasTZ = isNode && (flags & 0x400U) != 0;
                bool hasSX = isNode && (flags & 0x8000U) != 0, hasSY = isNode && (flags & 0x10000U) != 0, hasSZ = isNode && (flags & 0x20000U) != 0;
                bool hasCR = isMat && (flags & 0x200U) != 0, hasCG = isMat && (flags & 0x400U) != 0, hasCB = isMat && (flags & 0x800U) != 0;

                string prefix = (hasTX || hasTY || hasTZ) ? "localPosition" : ((hasSX || hasSY || hasSZ) ? "localScale" : "material._Color");
                Type compType = isNode ? typeof(Transform) : typeof(Renderer);
                string[] suffixes = isNode ? new[] { ".x", ".y", ".z" } : new[] { ".r", ".g", ".b" };
                bool[] active = (hasTX || hasTY || hasTZ) ? new[] { hasTX, hasTY, hasTZ } : ((hasSX || hasSY || hasSZ) ? new[] { hasSX, hasSY, hasSZ } : new[] { hasCR, hasCG, hasCB });

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_VECTOR)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;
                    Vector3 val = kf.Value;

                    if (hasTX || hasTY || hasTZ) val = new Vector3(-val.x * scale, val.y * scale, val.z * scale);

                    for (int c = 0; c < 3; c++)
                    {
                        if (active[c])
                        {
                            PropertyKey key = new PropertyKey(targetPath, compType, prefix + suffixes[c]);
                            AddKeyframeToSegment(propertySegments, key, subMotion.InterpolationType, new Keyframe(time, c == 0 ? val.x : (c == 1 ? val.y : val.z)));
                        }
                    }
                }
                return;
            }

            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)
            {
                bool hasRX = (flags & 0x800U) != 0, hasRY = (flags & 0x1000U) != 0, hasRZ = (flags & 0x2000U) != 0;
                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;

                    if (hasRX) AddKeyframeToSegment(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x"), subMotion.InterpolationType, new Keyframe(time, BamsToDegrees(kf.Value1)));
                    if (hasRY) AddKeyframeToSegment(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y"), subMotion.InterpolationType, new Keyframe(time, -BamsToDegrees(kf.Value2)));
                    if (hasRZ) AddKeyframeToSegment(propertySegments, new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z"), subMotion.InterpolationType, new Keyframe(time, -BamsToDegrees(kf.Value3)));
                }
                return;
            }

            List<PropertyKey> keys = GetTargetPropertyKeys(subMotion.Type, parentType, targetPath);
            if (keys.Count == 0) return;

            foreach (var kf in subMotion.Keyframes)
            {
                float time = 0f, scalar = 0f;
                if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32) { time = (s32.Frame / 60f) * timeScale; scalar = (flags & 8U) != 0 ? Bams32ToDegrees(s32.Value) : s32.Value; }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT f) { time = (f.Frame / 60f) * timeScale; scalar = (flags & 4U) != 0 ? RadiansToDegrees(f.Value) : f.Value; }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16) { time = (s16.Frame / 60f) * timeScale; scalar = BamsToDegrees(s16.Value); }

                foreach (var key in keys)
                {
                    float val = scalar;
                    if (key.PropertyName.Contains("Position")) val *= scale;
                    if (key.PropertyName.EndsWith(".x") || key.PropertyName.EndsWith("AnglesRaw.y") || key.PropertyName.EndsWith("AnglesRaw.z")) val *= -1f;

                    AddKeyframeToSegment(propertySegments, key, subMotion.InterpolationType, new Keyframe(time, val));
                }
            }
        }

        private static List<PropertyKey> GetTargetPropertyKeys(SubMotionType subType, MotionType parentType, string path)
        {
            List<PropertyKey> keys = new List<PropertyKey>();
            bool isNode = (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_NODE || (parentType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == 0;
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

                // Category 16 (Material): 0x800000 is OFFSET_U (_MainTex_ST.z) and 0x1000000 is OFFSET_V (_MainTex_ST.w)
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
            List<PropertyKey> existingKeys = new List<PropertyKey>(propertySegments.Keys);
            Regex vectorPattern = new Regex(@"^(.*?)(localPosition|localEulerAnglesRaw|localScale|_MainTex_ST|_Color|_SpecColor|_EmissionColor)(\.[xyzw|rgba])$");
            HashSet<string> processedBases = new HashSet<string>();

            foreach (var key in existingKeys)
            {
                Match m = vectorPattern.Match(key.PropertyName);
                if (!m.Success) continue;

                string propBase = m.Groups[1].Value;
                string propType = m.Groups[2].Value;
                string groupUniqueId = $"{key.TargetPath}|{key.ComponentType.Name}|{propBase}{propType}";

                if (!processedBases.Add(groupUniqueId)) continue;

                Transform nodeTr = FindNodeTransform(key.TargetPath, nodeTransforms, nodeHierarchyTargets);
                string fullPrefix = propBase + propType;

                string[] suffixes = (propType == "_Color" || propType == "_SpecColor" || propType == "_EmissionColor")
                    ? new[] { ".r", ".g", ".b", ".a" }
                    : (propType == "_MainTex_ST" ? new[] { ".x", ".y", ".z", ".w" } : new[] { ".x", ".y", ".z" });

                for (int s = 0; s < suffixes.Length; s++)
                {
                    PropertyKey channelKey = new PropertyKey(key.TargetPath, key.ComponentType, fullPrefix + suffixes[s]);
                    if (!propertySegments.ContainsKey(channelKey))
                    {
                        float defaultVal = GetDefaultChannelValue(propType, s, nodeTr);
                        AddConstantChannel(propertySegments, channelKey, defaultVal, maxTime);
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
                    return (index == 0 || index == 1) ? 1.0f : 0.0f; // Tiling (X=1, Y=1), Offset (Z=0, W=0)
                case "_Color":
                    return 1.0f; // R=1, G=1, B=1, A=1
                case "_SpecColor":
                case "_EmissionColor":
                    return index == 3 ? 1.0f : 0.0f; // Alpha=1, RGB=0
                default:
                    return 0f;
            }
        }

        private static void AddConstantChannel(Dictionary<PropertyKey, List<SubMotionSegment>> segments, PropertyKey key, float val, float maxTime)
        {
            var seg = new SubMotionSegment { InterpolationType = SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT };
            seg.Keyframes.Add(new Keyframe(0f, val, float.PositiveInfinity, float.PositiveInfinity));
            if (maxTime > 0.001f) seg.Keyframes.Add(new Keyframe(maxTime, val, float.PositiveInfinity, float.PositiveInfinity));
            segments[key] = new List<SubMotionSegment> { seg };
        }

        private static void AddKeyframeToSegment(Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments, PropertyKey key, SubMotionInterpolationType interp, Keyframe kf)
        {
            if (!propertySegments.TryGetValue(key, out List<SubMotionSegment> segments))
            {
                segments = new List<SubMotionSegment>();
                propertySegments[key] = segments;
            }

            SubMotionSegment current = (segments.Count > 0 && segments[segments.Count - 1].InterpolationType == interp)
                ? segments[segments.Count - 1] : null;

            if (current == null)
            {
                current = new SubMotionSegment { InterpolationType = interp };
                segments.Add(current);
            }
            current.Keyframes.Add(kf);
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

        private static string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == root) return "";
            string path = transform.name;
            Transform curr = transform.parent;
            while (curr != null && curr != root) { path = curr.name + "/" + path; curr = curr.parent; }
            return path;
        }
        #endregion

        #region Tangent & Curve Merging
        private static AnimationCurve BuildMergedCurve(List<SubMotionSegment> segments)
        {
            if (segments == null || segments.Count == 0) return null;
            List<Keyframe> allKfs = new List<Keyframe>();

            foreach (var seg in segments)
            {
                if (seg.Keyframes == null || seg.Keyframes.Count == 0) continue;

                bool isConstant = seg.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT);
                Keyframe[] keys = seg.Keyframes.ToArray();

                if (isConstant)
                {
                    for (int i = 0; i < keys.Length; i++) { keys[i].inTangent = float.PositiveInfinity; keys[i].outTangent = float.PositiveInfinity; }
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

            List<Keyframe> uniqueKfs = new List<Keyframe>();
            for (int i = 0; i < allKfs.Count; i++)
            {
                Keyframe kf = allKfs[i];
                if (uniqueKfs.Count > 0)
                {
                    Keyframe prev = uniqueKfs[uniqueKfs.Count - 1];
                    if (Mathf.Abs(prev.time - kf.time) < 0.0001f)
                    {
                        if (Mathf.Abs(prev.value - kf.value) < 0.0001f) { prev.outTangent = kf.outTangent; uniqueKfs[uniqueKfs.Count - 1] = prev; continue; }
                        kf.time = prev.time + 0.0001f;
                    }
                }
                uniqueKfs.Add(kf);
            }

            return new AnimationCurve(uniqueKfs.ToArray());
        }
        #endregion
    }
}