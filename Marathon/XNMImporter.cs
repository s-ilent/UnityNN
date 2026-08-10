using UnityEngine;
using UnityEditor;

using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "xnm")]
    public class XNMImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        [Header("Import Settings")]
        public float m_Scale = 0.05f;

        [Header("Motion Summary")]
        [SerializeField] private MotionType m_Type;
        [SerializeField] private float m_StartFrame;
        [SerializeField] private float m_EndFrame;
        [SerializeField] private int m_SubMotionCount;
        [SerializeField] private float m_Framerate;

        public string[] m_nodeHierarchyTarget;

        public static float BamsToDegrees(int bamAngle)
        {
            return (float)((double)bamAngle * (180.0 / 32768.0));
        }

        public static float RadiansToDegrees(float radAngle)
        {
            return radAngle * Mathf.Rad2Deg;
        }

        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            var clip = new AnimationClip();
            string shortName = Path.GetFileNameWithoutExtension(ctx.assetPath);

            if (m_nodeHierarchyTarget == null) m_nodeHierarchyTarget = new string[0];

            NinjaNext loader = new NinjaNext();
            try
            {
                loader.Load(ctx.assetPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{shortName}: Failed to load motion asset: {ex.Message}");
                return;
            }

            if (loader.Data.Motion == null) return;

            m_Type = loader.Data.Motion.Type;
            m_StartFrame = loader.Data.Motion.StartFrame;
            m_EndFrame = loader.Data.Motion.EndFrame;
            m_SubMotionCount = loader.Data.Motion.SubMotions.Count;
            m_Framerate = loader.Data.Motion.Framerate <= 0 ? 60.0f : loader.Data.Motion.Framerate;

            float timeScale = 60.0f / m_Framerate;

            foreach (NinjaSubMotion subMotion in loader.Data.Motion.SubMotions)
            {
                string targetPath = subMotion.NodeIndex.ToString("0000");
                if (subMotion.NodeIndex >= 0 && subMotion.NodeIndex < m_nodeHierarchyTarget.Length &&
                    !string.IsNullOrEmpty(m_nodeHierarchyTarget[subMotion.NodeIndex]))
                {
                    targetPath = m_nodeHierarchyTarget[subMotion.NodeIndex];
                }

                ImportSubMotionCurves(subMotion, targetPath, clip, timeScale);
            }

            clip.EnsureQuaternionContinuity();

            // Set Loop / Repeat Flags
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_NOREPEAT))
            {
                clipSettings.loopTime = false;
                clipSettings.loopBlend = false;
            }
            else if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_CONSTREPEAT) || m_Type.HasFlag(MotionType.NND_MOTIONTYPE_REPEAT))
            {
                clipSettings.loopTime = true;
                clipSettings.loopBlend = m_Type.HasFlag(MotionType.NND_MOTIONTYPE_REPEAT);
            }
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            ctx.AddObjectToAsset("main", clip);
            ctx.SetMainObject(clip);
        }

        private void ImportSubMotionCurves(NinjaSubMotion subMotion, string targetPath, AnimationClip clip, float timeScale)
        {
            if (subMotion.Keyframes.Count == 0) return;

            // 1. Vector Keyframes (Positions / Scales)
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
                        val.x *= -1f * m_Scale; // Right-hand to Left-hand flip
                        val.y *= m_Scale;
                        val.z *= m_Scale;
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

            // 2. 16-Bit BAMS Rotation Keyframes with Continuous Unwrapping
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
                    float rawY = -BamsToDegrees(kf.Value2); // Y-axis flip
                    float rawZ = BamsToDegrees(kf.Value3);

                    // Continuous Euler unwrapping
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

            // 3. Single-Axis Track Import
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

                if (targetProp.Contains("Position")) val *= m_Scale;
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

        private float UnwrapAngle(float prev, float current)
        {
            float diff = Mathf.Repeat(current - prev + 180f, 360f) - 180f;
            return prev + diff;
        }

        private string GetTargetProperty(SubMotionType type)
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

        private void ApplyCurveSettings(AnimationCurve curve, SubMotionInterpolationType interp)
        {
            WrapMode mode = WrapMode.Default;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_NOREPEAT)) mode = WrapMode.ClampForever;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTREPEAT) || interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT)) mode = WrapMode.Loop;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_MIRROR)) mode = WrapMode.PingPong;

            curve.preWrapMode = mode;
            curve.postWrapMode = mode;

            AnimationUtility.TangentMode tMode = AnimationUtility.TangentMode.ClampedAuto;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_LINEAR)) tMode = AnimationUtility.TangentMode.Linear;
            if (interp.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT)) tMode = AnimationUtility.TangentMode.Constant;

            for (int i = 0; i < curve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, tMode);
                AnimationUtility.SetKeyRightTangentMode(curve, i, tMode);
            }
        }
    }
}