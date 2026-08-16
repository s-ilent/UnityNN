// File: Marathon/UnityParsers/NinjaMotionResolver.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    public static class NinjaMotionResolver
    {
        private static readonly string[] MotionExtensions = new string[] {
            ".xnm", ".XNM", ".gnm", ".GNM", ".znm", ".ZNM"
        };

        private static readonly string[] MaterialMotionExtensions = new string[] {
            ".xnv", ".XNV", ".gnv", ".GNV", ".znv", ".ZNV"
        };

        private static readonly string[] ModelExtensions = new string[] {
            ".xna", ".XNA", ".xnn", ".XNN", ".xnj", ".XNJ", ".xno", ".XNO", ".gna", ".gnn", ".gno"
        };

        public static float BamsToDegrees(int bamAngle) => (float)((double)bamAngle * (180.0 / 32768.0));
        public static float Bams32ToDegrees(int bam32Angle) => (float)((double)bam32Angle * (360.0 / 65536.0));
        public static float RadiansToDegrees(float radAngle) => radAngle * Mathf.Rad2Deg;

        private struct PropertyKey : System.IEquatable<PropertyKey>
        {
            public string TargetPath;
            public System.Type ComponentType;
            public string PropertyName;

            public PropertyKey(string targetPath, System.Type componentType, string propertyName)
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

            public override bool Equals(object obj)
            {
                return obj is PropertyKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + TargetPath.GetHashCode();
                    hash = hash * 23 + (ComponentType != null ? ComponentType.GetHashCode() : 0);
                    hash = hash * 23 + PropertyName.GetHashCode();
                    return hash;
                }
            }
        }

        private class SubMotionSegment
        {
            public SubMotionInterpolationType InterpolationType;
            public List<Keyframe> Keyframes = new List<Keyframe>();
        }

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

            string baseDirectory = Path.GetDirectoryName(assetPath);
            string baseFileName = Path.GetFileNameWithoutExtension(assetPath);

            foreach (string ext in MotionExtensions)
            {
                string candidatePath = Path.Combine(baseDirectory, baseFileName + ext).Replace('\\', '/');
                if (candidatePath.Equals(assetPath.Replace('\\', '/'), System.StringComparison.OrdinalIgnoreCase)) continue;

                if (File.Exists(candidatePath))
                {
                    try
                    {
                        NinjaNext loader = new NinjaNext();
                        loader.Load(candidatePath);
                        if (loader.Data.Motion != null)
                        {
                            nodeMotion = loader.Data.Motion;
                            nodeMotionSource = $"External: {Path.GetFileName(candidatePath)}";
                            if (ctx != null) ctx.DependsOnSourceAsset(candidatePath);
                            break;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Could not load linked node motion {candidatePath}:\n{ex}");
                    }
                }
            }

            foreach (string ext in MaterialMotionExtensions)
            {
                string candidatePath = Path.Combine(baseDirectory, baseFileName + ext).Replace('\\', '/');
                if (candidatePath.Equals(assetPath.Replace('\\', '/'), System.StringComparison.OrdinalIgnoreCase)) continue;

                if (File.Exists(candidatePath))
                {
                    try
                    {
                        NinjaNext loader = new NinjaNext();
                        loader.Load(candidatePath);
                        NinjaMotion foundMot = loader.Data.MaterialMotion ?? loader.Data.Motion;
                        if (foundMot != null)
                        {
                            matMotion = foundMot;
                            matMotionSource = $"External: {Path.GetFileName(candidatePath)}";
                            if (ctx != null) ctx.DependsOnSourceAsset(candidatePath);
                            break;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Could not load linked material motion {candidatePath}:\n{ex}");
                    }
                }
            }
        }

        public static string[] ResolveNodeHierarchyTargets(string assetPath, UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            if (string.IsNullOrEmpty(assetPath)) return new string[0];

            string baseDirectory = Path.GetDirectoryName(assetPath);
            string baseFileName = Path.GetFileNameWithoutExtension(assetPath);

            foreach (string ext in ModelExtensions)
            {
                string candidatePath = Path.Combine(baseDirectory, baseFileName + ext).Replace('\\', '/');
                if (candidatePath.Equals(assetPath.Replace('\\', '/'), System.StringComparison.OrdinalIgnoreCase)) continue;

                if (File.Exists(candidatePath))
                {
                    try
                    {
                        NinjaNext loader = new NinjaNext();
                        loader.Load(candidatePath);

                        if (loader.Data.Object != null && loader.Data.Object.Nodes != null && loader.Data.Object.Nodes.Count > 0)
                        {
                            if (ctx != null) ctx.DependsOnSourceAsset(candidatePath);
                            return ComputeNodeHierarchyPaths(loader.Data.Object.Nodes);
                        }
                        else if (loader.Data.NodeNameList != null && loader.Data.NodeNameList.NinjaNodeNames != null)
                        {
                            if (ctx != null) ctx.DependsOnSourceAsset(candidatePath);
                            return loader.Data.NodeNameList.NinjaNodeNames.ToArray();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Could not resolve target nodes from {candidatePath}:\n{ex}");
                    }
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
                    int parentIdx = node.ParentIndex;
                    string name = !string.IsNullOrEmpty(node.Name) ? node.Name : $"Node_{curr:0000}";

                    if (parentIdx == -1) // Root node
                        break;

                    parts.Insert(0, name);
                    curr = parentIdx;
                }
                paths[i] = string.Join("/", parts);
            }
            return paths;
        }

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            GameObject rootGO,
            List<Transform> nodeTransforms = null,
            MeshImportMode importMode = MeshImportMode.SingleSkinnedMesh)
        {
            if (motionData == null) return null;

            string[] nodeHierarchyPaths = null;
            if (nodeTransforms != null && nodeTransforms.Count > 0 && rootGO != null)
            {
                nodeHierarchyPaths = new string[nodeTransforms.Count];
                for (int i = 0; i < nodeTransforms.Count; i++)
                {
                    if (nodeTransforms[i] != null)
                        nodeHierarchyPaths[i] = GetTransformPath(nodeTransforms[i], rootGO.transform);
                }
            }
            else if (rootGO != null)
            {
                Transform[] transforms = rootGO.GetComponentsInChildren<Transform>(true);
                nodeHierarchyPaths = new string[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    nodeHierarchyPaths[i] = GetTransformPath(transforms[i], rootGO.transform);
                }
            }

            return ResolveMotion(motionData, clipName, scale, nodeHierarchyPaths, importMode);
        }

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            MeshImportMode importMode = MeshImportMode.SingleSkinnedMesh)
        {
            if (motionData == null) return null;

            var clip = new AnimationClip { name = clipName };
            float framerate = motionData.Framerate <= 0 ? 60.0f : motionData.Framerate;
            float timeScale = 60.0f / framerate;

            if (nodeHierarchyTargets == null) nodeHierarchyTargets = new string[0];

            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments = new Dictionary<PropertyKey, List<SubMotionSegment>>();

            foreach (NinjaSubMotion subMotion in motionData.SubMotions)
            {
                if (subMotion == null || subMotion.Keyframes == null || subMotion.Keyframes.Count == 0) continue;

                string targetPath = subMotion.NodeIndex.ToString("0000");
                if (subMotion.NodeIndex >= 0 && subMotion.NodeIndex < nodeHierarchyTargets.Length &&
                    !string.IsNullOrEmpty(nodeHierarchyTargets[subMotion.NodeIndex]))
                {
                    targetPath = nodeHierarchyTargets[subMotion.NodeIndex];
                }

                // If this is a material motion in SingleSkinnedMesh mode, the Renderer lives on the root GameObject ("")
                bool isMaterialMotion = (motionData.Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;
                if (isMaterialMotion && importMode == MeshImportMode.SingleSkinnedMesh)
                {
                    targetPath = "";
                }

                CollectSubMotionSegments(subMotion, targetPath, propertySegments, timeScale, scale, motionData.Type);
            }

            foreach (var kvp in propertySegments)
            {
                PropertyKey key = kvp.Key;
                List<SubMotionSegment> segments = kvp.Value;

                AnimationCurve mergedCurve = BuildMergedCurve(segments);
                if (mergedCurve != null && mergedCurve.keys.Length > 0)
                {
                    clip.SetCurve(key.TargetPath, key.ComponentType, key.PropertyName, mergedCurve);
                }
            }

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            if (motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_NOREPEAT))
            {
                clipSettings.loopTime = false;
                clipSettings.loopBlend = false;
            }
            else if (motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_CONSTREPEAT) || motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_REPEAT))
            {
                clipSettings.loopTime = true;
                clipSettings.loopBlend = motionData.Type.HasFlag(MotionType.NND_MOTIONTYPE_REPEAT);
            }
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            return clip;
        }

        private static string GetTransformPath(Transform transform, Transform root)
        {
            if (transform == root) return "";
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static void AddKeyframeToSegment(
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

            SubMotionSegment currentSegment = null;
            if (segments.Count > 0 && segments[segments.Count - 1].InterpolationType == interp)
            {
                currentSegment = segments[segments.Count - 1];
            }
            else
            {
                currentSegment = new SubMotionSegment { InterpolationType = interp };
                segments.Add(currentSegment);
            }

            currentSegment.Keyframes.Add(kf);
        }

        private static void CollectSubMotionSegments(
            NinjaSubMotion subMotion,
            string targetPath,
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            float timeScale,
            float scale,
            MotionType parentMotionType)
        {
            if (subMotion.Keyframes == null || subMotion.Keyframes.Count == 0) return;

            bool isNodeMotion = (parentMotionType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_NODE
                             || (parentMotionType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == 0;
            bool isMaterialMotion = (parentMotionType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;

            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_VECTOR)
            {
                bool isTrans = isNodeMotion && (subMotion.Type & SubMotionType.NND_SMOTTYPE_TRANSLATION_MASK) != 0;
                bool isScale = isNodeMotion && (subMotion.Type & SubMotionType.NND_SMOTTYPE_SCALING_MASK) != 0;

                string prefix = isTrans ? "localPosition" : (isScale ? "localScale" : "");

                PropertyKey keyX, keyY, keyZ;
                if (isNodeMotion)
                {
                    if (string.IsNullOrEmpty(prefix)) prefix = "localPosition";
                    keyX = new PropertyKey(targetPath, typeof(Transform), $"{prefix}.x");
                    keyY = new PropertyKey(targetPath, typeof(Transform), $"{prefix}.y");
                    keyZ = new PropertyKey(targetPath, typeof(Transform), $"{prefix}.z");
                }
                else
                {
                    keyX = new PropertyKey(targetPath, typeof(Renderer), "material._Color.r");
                    keyY = new PropertyKey(targetPath, typeof(Renderer), "material._Color.g");
                    keyZ = new PropertyKey(targetPath, typeof(Renderer), "material._Color.b");
                }

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_VECTOR)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;
                    Vector3 val = kf.Value;

                    if (isTrans)
                    {
                        val.x *= -1f * scale;
                        val.y *= scale;
                        val.z *= scale;
                    }

                    AddKeyframeToSegment(propertySegments, keyX, subMotion.InterpolationType, new Keyframe(time, val.x));
                    AddKeyframeToSegment(propertySegments, keyY, subMotion.InterpolationType, new Keyframe(time, val.y));
                    AddKeyframeToSegment(propertySegments, keyZ, subMotion.InterpolationType, new Keyframe(time, val.z));
                }
                return;
            }

            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)
            {
                PropertyKey keyX = new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x");
                PropertyKey keyY = new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y");
                PropertyKey keyZ = new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z");

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;

                    float rawX = BamsToDegrees(kf.Value1);
                    float rawY = -BamsToDegrees(kf.Value2);
                    float rawZ = -BamsToDegrees(kf.Value3);

                    AddKeyframeToSegment(propertySegments, keyX, subMotion.InterpolationType, new Keyframe(time, rawX));
                    AddKeyframeToSegment(propertySegments, keyY, subMotion.InterpolationType, new Keyframe(time, rawY));
                    AddKeyframeToSegment(propertySegments, keyZ, subMotion.InterpolationType, new Keyframe(time, rawZ));
                }
                return;
            }

            List<PropertyKey> targetKeys = GetTargetPropertyKeys(subMotion.Type, parentMotionType, targetPath);
            if (targetKeys == null || targetKeys.Count == 0) return;

            foreach (var kf in subMotion.Keyframes)
            {
                float time = 0f, rawVal = 0f;

                if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32Kf)
                {
                    time = (s32Kf.Frame / 60.0f) * timeScale;
                    rawVal = ((subMotion.Type & SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE32) != 0) ? Bams32ToDegrees(s32Kf.Value) : s32Kf.Value;
                }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT fKf)
                {
                    time = (fKf.Frame / 60.0f) * timeScale;
                    rawVal = ((subMotion.Type & SubMotionType.NND_SMOTTYPE_ANGLE_RADIAN) != 0) ? RadiansToDegrees(fKf.Value) : fKf.Value;
                }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16Kf)
                {
                    time = (s16Kf.Frame / 60.0f) * timeScale;
                    rawVal = BamsToDegrees(s16Kf.Value);
                }

                foreach (PropertyKey key in targetKeys)
                {
                    float val = rawVal;
                    if (key.PropertyName.Contains("Position")) val *= scale;
                    if (key.PropertyName.Contains("localPosition.x") || key.PropertyName.Contains("localEulerAnglesRaw.y") || key.PropertyName.Contains("localEulerAnglesRaw.z")) val *= -1f;

                    AddKeyframeToSegment(propertySegments, key, subMotion.InterpolationType, new Keyframe(time, val));
                }
            }
        }

        private static List<PropertyKey> GetTargetPropertyKeys(SubMotionType subType, MotionType parentMotionType, string targetPath)
        {
            List<PropertyKey> keys = new List<PropertyKey>();

            bool isNodeMotion = (parentMotionType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_NODE
                             || (parentMotionType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == 0;
            bool isMaterialMotion = (parentMotionType & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;

            if (isNodeMotion)
            {
                uint rawVal = (uint)subType;

                // Check Translation Channels
                uint trans = rawVal & 0x700U;
                if (trans == 0x100U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.x"));
                else if (trans == 0x200U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.y"));
                else if (trans == 0x400U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.z"));
                else if (trans == 0x700U)
                {
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.x"));
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.y"));
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.z"));
                }

                // Check Rotation Channels
                uint rot = rawVal & 0x7800U;
                if (rot == 0x800U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x"));
                else if (rot == 0x1000U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y"));
                else if (rot == 0x2000U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z"));
                else if (rot == 0x3800U)
                {
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x"));
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y"));
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z"));
                }

                // Check Scaling Channels
                uint scl = rawVal & 0x38000U;
                if (scl == 0x8000U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.x"));
                else if (scl == 0x10000U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.y"));
                else if (scl == 0x20000U) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.z"));
                else if (scl == 0x38000U)
                {
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.x"));
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.y"));
                    keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.z"));
                }
            }
            else if (isMaterialMotion)
            {
                uint rawVal = (uint)subType;

                uint diff = rawVal & 0xE00U;
                if (diff == 0x200U) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.r"));
                else if (diff == 0x400U) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.g"));
                else if (diff == 0x800U) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.b"));
                else if (diff == 0xE00U)
                {
                    keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.r"));
                    keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.g"));
                    keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.b"));
                }

                if ((rawVal & 0x1000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.a"));

                uint uv = rawVal & 0x1800000U;
                if (uv == 0x800000U) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._MainTex_ST.z"));
                else if (uv == 0x1000000U) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._MainTex_ST.w"));
                else if (uv == 0x1800000U)
                {
                    keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._MainTex_ST.z"));
                    keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._MainTex_ST.w"));
                }
            }

            return keys;
        }

        private static AnimationCurve BuildMergedCurve(List<SubMotionSegment> segments)
        {
            if (segments == null || segments.Count == 0) return null;

            List<Keyframe> allKfs = new List<Keyframe>();

            foreach (var seg in segments)
            {
                if (seg.Keyframes == null || seg.Keyframes.Count == 0) continue;

                SubMotionInterpolationType interp = seg.InterpolationType;
                bool isConstant = interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT);
                bool isLinear = interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_LINEAR);

                Keyframe[] segKeys = seg.Keyframes.ToArray();

                if (isConstant)
                {
                    for (int i = 0; i < segKeys.Length; i++)
                    {
                        segKeys[i].inTangent = float.PositiveInfinity;
                        segKeys[i].outTangent = float.PositiveInfinity;
                    }
                }
                else if (isLinear && segKeys.Length >= 2)
                {
                    for (int i = 0; i < segKeys.Length - 1; i++)
                    {
                        float dt = segKeys[i + 1].time - segKeys[i].time;
                        if (dt > 0.00001f)
                        {
                            float slope = (segKeys[i + 1].value - segKeys[i].value) / dt;
                            segKeys[i].outTangent = slope;
                            segKeys[i + 1].inTangent = slope;
                        }
                    }
                    segKeys[0].inTangent = segKeys[0].outTangent;
                    segKeys[segKeys.Length - 1].outTangent = segKeys[segKeys.Length - 1].inTangent;
                }

                allKfs.AddRange(segKeys);
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
                        if (Mathf.Abs(prev.value - kf.value) < 0.0001f)
                        {
                            prev.outTangent = kf.outTangent;
                            uniqueKfs[uniqueKfs.Count - 1] = prev;
                            continue;
                        }
                        else
                        {
                            kf.time = prev.time + 0.0001f;
                        }
                    }
                }
                uniqueKfs.Add(kf);
            }

            AnimationCurve curve = new AnimationCurve(uniqueKfs.ToArray());

            for (int i = 0; i < curve.keys.Length; i++)
            {
                Keyframe kf = curve.keys[i];
                if (float.IsInfinity(kf.inTangent) || float.IsInfinity(kf.outTangent))
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                }
                else
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
            }

            SubMotionInterpolationType firstInterp = segments[0].InterpolationType;
            WrapMode mode = WrapMode.Default;
            if (firstInterp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_NOREPEAT)) mode = WrapMode.ClampForever;
            if (firstInterp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTREPEAT) || firstInterp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT)) mode = WrapMode.Loop;
            if (firstInterp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_MIRROR)) mode = WrapMode.PingPong;

            curve.preWrapMode = mode;
            curve.postWrapMode = mode;

            return curve;
        }
    }
}