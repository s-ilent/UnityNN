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
        public static float RadiansToDegrees(float radAngle) => radAngle * Mathf.Rad2Deg;

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

                        List<string> nodeNames = null;
                        if (loader.Data.NodeNameList != null && loader.Data.NodeNameList.NinjaNodeNames != null)
                        {
                            nodeNames = loader.Data.NodeNameList.NinjaNodeNames;
                        }
                        else if (loader.Data.Object != null && loader.Data.Object.Nodes != null)
                        {
                            nodeNames = new List<string>();
                            for (int i = 0; i < loader.Data.Object.Nodes.Count; i++)
                            {
                                string nName = loader.Data.Object.Nodes[i].Name;
                                nodeNames.Add(!string.IsNullOrEmpty(nName) ? nName : $"Node_{i:0000}");
                            }
                        }

                        if (nodeNames != null && nodeNames.Count > 0)
                        {
                            if (ctx != null) ctx.DependsOnSourceAsset(candidatePath);
                            return nodeNames.ToArray();
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

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            GameObject rootGO,
            List<Transform> nodeTransforms = null)
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

            return ResolveMotion(motionData, clipName, scale, nodeHierarchyPaths);
        }

        public static AnimationClip ResolveMotion(
            NinjaMotion motionData,
            string clipName,
            float scale,
            string[] nodeHierarchyTargets)
        {
            if (motionData == null) return null;

            var clip = new AnimationClip { name = clipName };
            float framerate = motionData.Framerate <= 0 ? 60.0f : motionData.Framerate;
            float timeScale = 60.0f / framerate;

            if (nodeHierarchyTargets == null) nodeHierarchyTargets = new string[0];

            foreach (NinjaSubMotion subMotion in motionData.SubMotions)
            {
                if (subMotion == null) continue;

                string targetPath = subMotion.NodeIndex.ToString("0000");
                if (subMotion.NodeIndex >= 0 && subMotion.NodeIndex < nodeHierarchyTargets.Length &&
                    !string.IsNullOrEmpty(nodeHierarchyTargets[subMotion.NodeIndex]))
                {
                    targetPath = nodeHierarchyTargets[subMotion.NodeIndex];
                }

                ImportSubMotionCurves(subMotion, targetPath, clip, timeScale, scale);
            }

            clip.EnsureQuaternionContinuity();

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

        private static void ImportSubMotionCurves(NinjaSubMotion subMotion, string targetPath, AnimationClip clip, float timeScale, float scale)
        {
            if (subMotion.Keyframes == null || subMotion.Keyframes.Count == 0) return;

            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_VECTOR)
            {
                AnimationCurve curveX = new AnimationCurve();
                AnimationCurve curveY = new AnimationCurve();
                AnimationCurve curveZ = new AnimationCurve();

                foreach (var objKf in subMotion.Keyframes)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_VECTOR)objKf;
                    float time = (kf.Frame / 60.0f) * timeScale;
                    Vector3 val = kf.Value;

                    if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_MASK))
                    {
                        val.x *= -1f * scale;
                        val.y *= scale;
                        val.z *= scale;
                    }

                    curveX.AddKey(new Keyframe(time, val.x));
                    curveY.AddKey(new Keyframe(time, val.y));
                    curveZ.AddKey(new Keyframe(time, val.z));
                }

                ApplyCurveSettings(curveX, subMotion.InterpolationType);
                ApplyCurveSettings(curveY, subMotion.InterpolationType);
                ApplyCurveSettings(curveZ, subMotion.InterpolationType);

                string prefix = subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_MASK) ? "localPosition" : "localScale";
                clip.SetCurve(targetPath, typeof(Transform), $"{prefix}.x", curveX);
                clip.SetCurve(targetPath, typeof(Transform), $"{prefix}.y", curveY);
                clip.SetCurve(targetPath, typeof(Transform), $"{prefix}.z", curveZ);
                return;
            }

            if (subMotion.Keyframes[0] is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)
            {
                AnimationCurve curveX = new AnimationCurve();
                AnimationCurve curveY = new AnimationCurve();
                AnimationCurve curveZ = new AnimationCurve();

                float lastX = 0f, lastY = 0f, lastZ = 0f;

                for (int i = 0; i < subMotion.Keyframes.Count; i++)
                {
                    var kf = (NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)subMotion.Keyframes[i];
                    float time = (kf.Frame / 60.0f) * timeScale;

                    float rawX = BamsToDegrees(kf.Value1);
                    float rawY = -BamsToDegrees(kf.Value2);
                    float rawZ = BamsToDegrees(kf.Value3);

                    if (i > 0)
                    {
                        rawX = UnwrapAngle(lastX, rawX);
                        rawY = UnwrapAngle(lastY, rawY);
                        rawZ = UnwrapAngle(lastZ, rawZ);
                    }

                    lastX = rawX; lastY = rawY; lastZ = rawZ;

                    curveX.AddKey(new Keyframe(time, rawX));
                    curveY.AddKey(new Keyframe(time, rawY));
                    curveZ.AddKey(new Keyframe(time, rawZ));
                }

                ApplyCurveSettings(curveX, subMotion.InterpolationType);
                ApplyCurveSettings(curveY, subMotion.InterpolationType);
                ApplyCurveSettings(curveZ, subMotion.InterpolationType);

                clip.SetCurve(targetPath, typeof(Transform), "localEulerAnglesRaw.x", curveX);
                clip.SetCurve(targetPath, typeof(Transform), "localEulerAnglesRaw.y", curveY);
                clip.SetCurve(targetPath, typeof(Transform), "localEulerAnglesRaw.z", curveZ);
                return;
            }

            string targetProp = GetTargetProperty(subMotion.Type);
            if (string.IsNullOrEmpty(targetProp)) return;

            Keyframe[] kfs = new Keyframe[subMotion.Keyframes.Count];
            float lastVal = 0f;

            for (int i = 0; i < subMotion.Keyframes.Count; i++)
            {
                var kf = subMotion.Keyframes[i];
                float time = 0f, val = 0f;

                if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT fKf)
                {
                    time = (fKf.Frame / 60.0f) * timeScale;
                    val = subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_ANGLE_RADIAN) ? RadiansToDegrees(fKf.Value) : fKf.Value;
                }
                else if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16 s16Kf)
                {
                    time = (s16Kf.Frame / 60.0f) * timeScale;
                    val = BamsToDegrees(s16Kf.Value);
                }

                if (targetProp.Contains("Position")) val *= scale;
                if (targetProp.Contains("localPosition.x") || targetProp.Contains("localEulerAnglesRaw.y")) val *= -1f;

                if (i > 0 && targetProp.Contains("localEulerAnglesRaw"))
                {
                    val = UnwrapAngle(lastVal, val);
                }
                lastVal = val;

                kfs[i] = new Keyframe(time, val);
            }

            AnimationCurve curve = new AnimationCurve(kfs);
            ApplyCurveSettings(curve, subMotion.InterpolationType);
            clip.SetCurve(targetPath, typeof(Transform), targetProp, curve);
        }

        private static float UnwrapAngle(float prev, float current)
        {
            float diff = Mathf.Repeat(current - prev + 180f, 360f) - 180f;
            return prev + diff;
        }

        private static string GetTargetProperty(SubMotionType type)
        {
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_X)) return "localPosition.x";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_Y)) return "localPosition.y";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_Z)) return "localPosition.z";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_ROTATION_X)) return "localEulerAnglesRaw.x";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_ROTATION_Y)) return "localEulerAnglesRaw.y";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_ROTATION_Z)) return "localEulerAnglesRaw.z";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_SCALING_X)) return "localScale.x";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_SCALING_Y)) return "localScale.y";
            if (type.HasFlag(SubMotionType.NND_SMOTTYPE_SCALING_Z)) return "localScale.z";
            return "";
        }

        private static void ApplyCurveSettings(AnimationCurve curve, SubMotionInterpolationType interp)
        {
            if (curve == null || curve.keys == null || curve.keys.Length == 0) return;

            WrapMode mode = WrapMode.Default;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_NOREPEAT)) mode = WrapMode.ClampForever;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTREPEAT) || interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT)) mode = WrapMode.Loop;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_MIRROR)) mode = WrapMode.PingPong;

            curve.preWrapMode = mode;
            curve.postWrapMode = mode;

            Keyframe[] keys = curve.keys;
            if (keys.Length >= 2)
            {
                if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT))
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].inTangent = float.PositiveInfinity;
                        keys[i].outTangent = float.PositiveInfinity;
                    }
                    curve.keys = keys;
                }
                else if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_LINEAR))
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
                    curve.keys = keys;
                }
                else
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        curve.SmoothTangents(i, 0f);
                    }
                }
            }
        }
    }
}