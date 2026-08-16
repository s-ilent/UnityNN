// File: Marathon/UnityParsers/NinjaMotionResolver.cs
using UnityEngine;
using UnityEditor;
using System;
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
                if (candidatePath.Equals(assetPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;

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
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Could not load linked node motion {candidatePath}:\n{ex}");
                    }
                }
            }

            foreach (string ext in MaterialMotionExtensions)
            {
                string candidatePath = Path.Combine(baseDirectory, baseFileName + ext).Replace('\\', '/');
                if (candidatePath.Equals(assetPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;

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
                    catch (Exception ex)
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
                if (candidatePath.Equals(assetPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) continue;

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
                    catch (Exception ex)
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
            MeshImportMode importMode = MeshImportMode.CombinedByNode)
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

            return ResolveMotionInternal(motionData, clipName, scale, nodeHierarchyPaths, nodeTransforms, importMode);
        }

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            MeshImportMode importMode = MeshImportMode.CombinedByNode)
        {
            return ResolveMotionInternal(motionData, clipName, scale, nodeHierarchyTargets, null, importMode);
        }

        private static AnimationClip ResolveMotionInternal(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets,
            List<Transform> nodeTransforms,
            MeshImportMode importMode)
        {
            if (motionData == null) return null;

            var clip = new AnimationClip { name = clipName };
            float framerate = motionData.Framerate <= 0 ? 60.0f : motionData.Framerate;
            float timeScale = 60.0f / framerate;
            float maxClipTime = (motionData.EndFrame / 60.0f) * timeScale;

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

                // In SingleSkinnedMesh mode, material animations target the root renderer
                bool isMaterialMotion = (motionData.Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;
                if (isMaterialMotion && importMode == MeshImportMode.SingleSkinnedMesh)
                {
                    targetPath = "";
                }

                CollectSubMotionSegments(subMotion, targetPath, propertySegments, timeScale, scale, motionData.Type);
            }

            // Fill un-animated companion channels with constant rest-pose values so Unity does not zero them out
            FillMissingTransformChannels(propertySegments, nodeTransforms, nodeHierarchyTargets, maxClipTime);

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

        private static void FillMissingTransformChannels(
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            List<Transform> nodeTransforms,
            string[] nodeHierarchyTargets,
            float maxTime)
        {
            HashSet<string> targetPaths = new HashSet<string>();
            foreach (var key in propertySegments.Keys)
            {
                if (key.ComponentType == typeof(Transform))
                {
                    targetPaths.Add(key.TargetPath);
                }
            }

            foreach (string path in targetPaths)
            {
                Transform nodeTr = FindNodeTransformByPath(path, nodeTransforms, nodeHierarchyTargets);

                Vector3 restPos = nodeTr != null ? nodeTr.localPosition : Vector3.zero;
                Vector3 restRot = nodeTr != null ? nodeTr.localEulerAngles : Vector3.zero;
                Vector3 restScale = nodeTr != null ? nodeTr.localScale : Vector3.one;

                // 1. Position Channels
                PropertyKey posXKey = new PropertyKey(path, typeof(Transform), "localPosition.x");
                PropertyKey posYKey = new PropertyKey(path, typeof(Transform), "localPosition.y");
                PropertyKey posZKey = new PropertyKey(path, typeof(Transform), "localPosition.z");

                bool hasPosX = propertySegments.ContainsKey(posXKey);
                bool hasPosY = propertySegments.ContainsKey(posYKey);
                bool hasPosZ = propertySegments.ContainsKey(posZKey);

                if (hasPosX || hasPosY || hasPosZ)
                {
                    if (!hasPosX) AddConstantChannel(propertySegments, posXKey, restPos.x, maxTime);
                    if (!hasPosY) AddConstantChannel(propertySegments, posYKey, restPos.y, maxTime);
                    if (!hasPosZ) AddConstantChannel(propertySegments, posZKey, restPos.z, maxTime);
                }

                // 2. Rotation Channels
                PropertyKey rotXKey = new PropertyKey(path, typeof(Transform), "localEulerAnglesRaw.x");
                PropertyKey rotYKey = new PropertyKey(path, typeof(Transform), "localEulerAnglesRaw.y");
                PropertyKey rotZKey = new PropertyKey(path, typeof(Transform), "localEulerAnglesRaw.z");

                bool hasRotX = propertySegments.ContainsKey(rotXKey);
                bool hasRotY = propertySegments.ContainsKey(rotYKey);
                bool hasRotZ = propertySegments.ContainsKey(rotZKey);

                if (hasRotX || hasRotY || hasRotZ)
                {
                    if (!hasRotX) AddConstantChannel(propertySegments, rotXKey, restRot.x, maxTime);
                    if (!hasRotY) AddConstantChannel(propertySegments, rotYKey, restRot.y, maxTime);
                    if (!hasRotZ) AddConstantChannel(propertySegments, rotZKey, restRot.z, maxTime);
                }

                // 3. Scaling Channels
                PropertyKey sclXKey = new PropertyKey(path, typeof(Transform), "localScale.x");
                PropertyKey sclYKey = new PropertyKey(path, typeof(Transform), "localScale.y");
                PropertyKey sclZKey = new PropertyKey(path, typeof(Transform), "localScale.z");

                bool hasSclX = propertySegments.ContainsKey(sclXKey);
                bool hasSclY = propertySegments.ContainsKey(sclYKey);
                bool hasSclZ = propertySegments.ContainsKey(sclZKey);

                if (hasSclX || hasSclY || hasSclZ)
                {
                    if (!hasSclX) AddConstantChannel(propertySegments, sclXKey, restScale.x, maxTime);
                    if (!hasSclY) AddConstantChannel(propertySegments, sclYKey, restScale.y, maxTime);
                    if (!hasSclZ) AddConstantChannel(propertySegments, sclZKey, restScale.z, maxTime);
                }
            }
        }

        private static void AddConstantChannel(
            Dictionary<PropertyKey, List<SubMotionSegment>> propertySegments,
            PropertyKey key,
            float constantValue,
            float maxTime)
        {
            var seg = new SubMotionSegment { InterpolationType = SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT };
            seg.Keyframes.Add(new Keyframe(0f, constantValue, float.PositiveInfinity, float.PositiveInfinity));
            if (maxTime > 0.001f)
            {
                seg.Keyframes.Add(new Keyframe(maxTime, constantValue, float.PositiveInfinity, float.PositiveInfinity));
            }

            propertySegments[key] = new List<SubMotionSegment> { seg };
        }

        private static Transform FindNodeTransformByPath(string path, List<Transform> nodeTransforms, string[] nodeHierarchyTargets)
        {
            if (nodeTransforms == null || nodeTransforms.Count == 0) return null;

            for (int i = 0; i < nodeTransforms.Count; i++)
            {
                if (i < nodeHierarchyTargets.Length && nodeHierarchyTargets[i] == path)
                {
                    return nodeTransforms[i];
                }
            }

            foreach (var tr in nodeTransforms)
            {
                if (tr != null && tr.name == path) return tr;
            }

            return null;
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

            uint smTypeFlags = (uint)subMotion.Type;

            // 1. Vector Keyframes (3-axis Translation / Scaling / Colors)
            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_VECTOR)
            {
                bool hasTransX = isNodeMotion && (smTypeFlags & 0x100U) != 0;
                bool hasTransY = isNodeMotion && (smTypeFlags & 0x200U) != 0;
                bool hasTransZ = isNodeMotion && (smTypeFlags & 0x400U) != 0;

                bool hasScaleX = isNodeMotion && (smTypeFlags & 0x8000U) != 0;
                bool hasScaleY = isNodeMotion && (smTypeFlags & 0x10000U) != 0;
                bool hasScaleZ = isNodeMotion && (smTypeFlags & 0x20000U) != 0;

                bool hasDiffR = isMaterialMotion && (smTypeFlags & 0x200U) != 0;
                bool hasDiffG = isMaterialMotion && (smTypeFlags & 0x400U) != 0;
                bool hasDiffB = isMaterialMotion && (smTypeFlags & 0x800U) != 0;

                PropertyKey keyX = default, keyY = default, keyZ = default;

                if (hasTransX || hasTransY || hasTransZ)
                {
                    if (hasTransX) keyX = new PropertyKey(targetPath, typeof(Transform), "localPosition.x");
                    if (hasTransY) keyY = new PropertyKey(targetPath, typeof(Transform), "localPosition.y");
                    if (hasTransZ) keyZ = new PropertyKey(targetPath, typeof(Transform), "localPosition.z");
                }
                else if (hasScaleX || hasScaleY || hasScaleZ)
                {
                    if (hasScaleX) keyX = new PropertyKey(targetPath, typeof(Transform), "localScale.x");
                    if (hasScaleY) keyY = new PropertyKey(targetPath, typeof(Transform), "localScale.y");
                    if (hasScaleZ) keyZ = new PropertyKey(targetPath, typeof(Transform), "localScale.z");
                }
                else if (hasDiffR || hasDiffG || hasDiffB)
                {
                    if (hasDiffR) keyX = new PropertyKey(targetPath, typeof(Renderer), "material._Color.r");
                    if (hasDiffG) keyY = new PropertyKey(targetPath, typeof(Renderer), "material._Color.g");
                    if (hasDiffB) keyZ = new PropertyKey(targetPath, typeof(Renderer), "material._Color.b");
                }

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_VECTOR)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;
                    Vector3 val = kf.Value;

                    if (hasTransX || hasTransY || hasTransZ)
                    {
                        val.x *= -1f * scale;
                        val.y *= scale;
                        val.z *= scale;
                    }

                    if (hasTransX || hasScaleX || hasDiffR) AddKeyframeToSegment(propertySegments, keyX, subMotion.InterpolationType, new Keyframe(time, val.x));
                    if (hasTransY || hasScaleY || hasDiffG) AddKeyframeToSegment(propertySegments, keyY, subMotion.InterpolationType, new Keyframe(time, val.y));
                    if (hasTransZ || hasScaleZ || hasDiffB) AddKeyframeToSegment(propertySegments, keyZ, subMotion.InterpolationType, new Keyframe(time, val.z));
                }
                return;
            }

            // 2. 3-Axis BAMS Rotation Keyframes
            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)
            {
                bool hasRotX = (smTypeFlags & 0x800U) != 0;
                bool hasRotY = (smTypeFlags & 0x1000U) != 0;
                bool hasRotZ = (smTypeFlags & 0x2000U) != 0;

                PropertyKey keyX = hasRotX ? new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x") : default;
                PropertyKey keyY = hasRotY ? new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y") : default;
                PropertyKey keyZ = hasRotZ ? new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z") : default;

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;

                    if (hasRotX) AddKeyframeToSegment(propertySegments, keyX, subMotion.InterpolationType, new Keyframe(time, BamsToDegrees(kf.Value1)));
                    if (hasRotY) AddKeyframeToSegment(propertySegments, keyY, subMotion.InterpolationType, new Keyframe(time, -BamsToDegrees(kf.Value2)));
                    if (hasRotZ) AddKeyframeToSegment(propertySegments, keyZ, subMotion.InterpolationType, new Keyframe(time, -BamsToDegrees(kf.Value3)));
                }
                return;
            }

            // 3. Scalar Keyframes (Float, Sint32, Sint16)
            List<PropertyKey> targetKeys = GetTargetPropertyKeys(subMotion.Type, parentMotionType, targetPath);
            if (targetKeys == null || targetKeys.Count == 0) return;

            foreach (var kf in subMotion.Keyframes)
            {
                float time = 0f, scalarVal = 0f;

                if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT32 s32Kf)
                {
                    time = (s32Kf.Frame / 60.0f) * timeScale;
                    scalarVal = ((subMotion.Type & SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE32) != 0) ? Bams32ToDegrees(s32Kf.Value) : s32Kf.Value;
                }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT fKf)
                {
                    time = (fKf.Frame / 60.0f) * timeScale;
                    scalarVal = ((subMotion.Type & SubMotionType.NND_SMOTTYPE_ANGLE_RADIAN) != 0) ? RadiansToDegrees(fKf.Value) : fKf.Value;
                }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16Kf)
                {
                    time = (s16Kf.Frame / 60.0f) * timeScale;
                    scalarVal = BamsToDegrees(s16Kf.Value);
                }

                foreach (PropertyKey key in targetKeys)
                {
                    float val = scalarVal;
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

            uint rawVal = (uint)subType;

            if (isNodeMotion)
            {
                // Translation Channels
                if ((rawVal & 0x100U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.x"));
                if ((rawVal & 0x200U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.y"));
                if ((rawVal & 0x400U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localPosition.z"));

                // Rotation Channels
                if ((rawVal & 0x800U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.x"));
                if ((rawVal & 0x1000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.y"));
                if ((rawVal & 0x2000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localEulerAnglesRaw.z"));

                // Scaling Channels
                if ((rawVal & 0x8000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.x"));
                if ((rawVal & 0x10000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.y"));
                if ((rawVal & 0x20000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Transform), "localScale.z"));
            }
            else if (isMaterialMotion)
            {
                // Material Diffuse Color Channels
                if ((rawVal & 0x200U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.r"));
                if ((rawVal & 0x400U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.g"));
                if ((rawVal & 0x800U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.b"));
                if ((rawVal & 0x1000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._Color.a"));

                // Material UV Offset Channels
                if ((rawVal & 0x800000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._MainTex_ST.z"));
                if ((rawVal & 0x1000000U) != 0) keys.Add(new PropertyKey(targetPath, typeof(Renderer), "material._MainTex_ST.w"));
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