// File: Marathon/Editor/UnityParsers/NinjaMotionCompressor.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityNN.Editor
{
    [Serializable]
    public class NinjaMotionCompressionSettings
    {
        [Header("Transform Tolerances")]
        [Tooltip("Max error for translation curves in meters (Unity units).")]
        public float PositionTolerance = 0.005f;

        [Tooltip("Max error for Euler rotations in degrees.")]
        public float EulerRotationToleranceDeg = 0.05f;

        [Tooltip("Max error for scale curves.")]
        public float ScaleTolerance = 0.005f;

        [Header("Material Curve Tolerances")]
        [Tooltip("Max error for UV Offset / Tiling curves (in UV space).")]
        public float UvTolerance = 0.001f;

        [Tooltip("Max error for Material Colors & Alpha (0.0 to 1.0 range).")]
        public float ColorTolerance = 0.005f;

        [Header("Optimizations")]
        [Tooltip("Strip static 1-keyframe default curves (e.g., unmoving specular/rotation).")]
        public bool StripConstantCurves = true;

        [Tooltip("Sample rate to evaluate dense imported curves (FPS).")]
        public float SampleRate = 60f;
    }

    public static class NinjaMotionCompressor
    {
        public static AnimationClip CompressNinjaClip(AnimationClip sourceClip, NinjaMotionCompressionSettings settings)
        {
            if (sourceClip == null) return null;
            settings ??= new NinjaMotionCompressionSettings();

            var compressedClip = new AnimationClip
            {
                name = sourceClip.name,
                frameRate = sourceClip.frameRate,
                legacy = sourceClip.legacy,
                wrapMode = sourceClip.wrapMode
            };

            float duration = sourceClip.length;
            if (duration <= 0f) return UnityEngine.Object.Instantiate(sourceClip);

            float fps = settings.SampleRate > 0 ? settings.SampleRate : (sourceClip.frameRate > 0 ? sourceClip.frameRate : 60f);
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(duration * fps) + 1);

            float[] times = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                times[i] = Mathf.Min(i / fps, duration);

            var indexPool = new List<int>(sampleCount);
            var bindings = AnimationUtility.GetCurveBindings(sourceClip);

            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                if (curve == null || curve.length == 0) continue;

                // 1. Check if track is constant
                if (settings.StripConstantCurves && IsCurveConstant(curve, out float constVal))
                {
                    if (IsSignificantConstant(binding.propertyName, constVal))
                    {
                        var singleKeyCurve = new AnimationCurve(new Keyframe(0f, constVal));
                        compressedClip.SetCurve(binding.path, binding.type, binding.propertyName, singleKeyCurve);
                    }
                    continue;
                }

                // 2. Compress dynamic curves based on type
                if (binding.propertyName.StartsWith("localEulerAngles"))
                {
                    CompressEulerCurve(sourceClip, compressedClip, binding, times, settings.EulerRotationToleranceDeg, indexPool);
                }
                else
                {
                    float tol = GetToleranceForProperty(binding.propertyName, settings);
                    CompressScalarCurve(sourceClip, compressedClip, binding, times, tol, indexPool);
                }
            }

            // Copy Animation Events and settings
            AnimationUtility.SetAnimationEvents(compressedClip, AnimationUtility.GetAnimationEvents(sourceClip));
            var srcSettings = AnimationUtility.GetAnimationClipSettings(sourceClip);
            if (srcSettings != null)
            {
                AnimationUtility.SetAnimationClipSettings(compressedClip, srcSettings);
            }

            return compressedClip;
        }

        #region Scalar & Material Compression
        private static void CompressScalarCurve(AnimationClip src, AnimationClip dst, EditorCurveBinding binding, float[] times, float tolerance, List<int> indexPool)
        {
            var origCurve = AnimationUtility.GetEditorCurve(src, binding);
            int n = times.Length;

            var values = new float[n];
            for (int i = 0; i < n; i++) values[i] = origCurve.Evaluate(times[i]);

            indexPool.Clear();
            indexPool.Add(0);
            indexPool.Add(n - 1);
            FitScalarRDP(values, times, 0, n - 1, tolerance, indexPool);
            indexPool.Sort();

            var newKeys = new Keyframe[indexPool.Count];
            for (int i = 0; i < indexPool.Count; i++)
            {
                int idx = indexPool[i];
                newKeys[i] = new Keyframe(times[idx], values[idx]);
            }

            var outCurve = new AnimationCurve(newKeys);
            ApplyClampedTangents(outCurve);
            dst.SetCurve(binding.path, binding.type, binding.propertyName, outCurve);
        }

        private static void FitScalarRDP(float[] values, float[] times, int first, int last, float tolerance, List<int> keys)
        {
            if (last <= first + 1) return;

            float tA = times[first];
            float tB = times[last];
            float vA = values[first];
            float vB = values[last];
            float dt = tB - tA;

            float maxErr = 0f;
            int splitIdx = first;

            for (int i = first + 1; i < last; i++)
            {
                float s = (times[i] - tA) / dt;
                float eval = Mathf.Lerp(vA, vB, s);
                float err = Mathf.Abs(values[i] - eval);

                if (err > maxErr)
                {
                    maxErr = err;
                    splitIdx = i;
                }
            }

            if (maxErr > tolerance)
            {
                keys.Add(splitIdx);
                FitScalarRDP(values, times, first, splitIdx, tolerance, keys);
                FitScalarRDP(values, times, splitIdx, last, tolerance, keys);
            }
        }
        #endregion

        #region Euler Angles (with 360-degree unwrapping)
        private static void CompressEulerCurve(AnimationClip src, AnimationClip dst, EditorCurveBinding binding, float[] times, float toleranceDeg, List<int> indexPool)
        {
            var origCurve = AnimationUtility.GetEditorCurve(src, binding);
            int n = times.Length;

            var unwrappedAngles = new float[n];
            float prevAngle = origCurve.Evaluate(times[0]);
            unwrappedAngles[0] = prevAngle;

            for (int i = 1; i < n; i++)
            {
                float rawAngle = origCurve.Evaluate(times[i]);
                float delta = Mathf.DeltaAngle(prevAngle, rawAngle);
                float unwrapped = prevAngle + delta;
                unwrappedAngles[i] = unwrapped;
                prevAngle = unwrapped;
            }

            indexPool.Clear();
            indexPool.Add(0);
            indexPool.Add(n - 1);
            FitScalarRDP(unwrappedAngles, times, 0, n - 1, toleranceDeg, indexPool);
            indexPool.Sort();

            var newKeys = new Keyframe[indexPool.Count];
            for (int i = 0; i < indexPool.Count; i++)
            {
                int idx = indexPool[i];
                newKeys[i] = new Keyframe(times[idx], unwrappedAngles[idx]);
            }

            var outCurve = new AnimationCurve(newKeys);
            ApplyClampedTangents(outCurve);
            dst.SetCurve(binding.path, binding.type, binding.propertyName, outCurve);
        }
        #endregion

        #region Helpers
        private static bool IsCurveConstant(AnimationCurve curve, out float value)
        {
            value = 0f;
            if (curve == null || curve.length == 0) return true;

            value = curve.keys[0].value;
            for (int i = 1; i < curve.length; i++)
            {
                if (Mathf.Abs(curve.keys[i].value - value) > 1e-5f)
                    return false;
            }
            return true;
        }

        private static bool IsSignificantConstant(string prop, float val)
        {
            if (prop.EndsWith(".x") || prop.EndsWith(".y"))
            {
                if (prop.Contains("_MainTex_ST") || prop.Contains("_MainTex2_ST") || prop.Contains("_MainTex3_ST"))
                    return Mathf.Abs(val - 1f) > 1e-4f;
            }

            if (prop.EndsWith(".z") || prop.EndsWith(".w"))
            {
                if (prop.Contains("_MainTex_ST") || prop.Contains("_MainTex2_ST") || prop.Contains("_MainTex3_ST"))
                    return Mathf.Abs(val) > 1e-4f;
            }

            if (prop.Contains("localScale") || prop.Contains("m_LocalScale"))
                return Mathf.Abs(val - 1f) > 1e-4f;

            if (prop.Contains("localPosition") || prop.Contains("m_LocalPosition"))
                return Mathf.Abs(val) > 1e-4f;

            if (prop.Contains("_Color.a"))
                return Mathf.Abs(val - 1f) > 1e-4f;

            return Mathf.Abs(val) > 1e-5f;
        }

        private static float GetToleranceForProperty(string prop, NinjaMotionCompressionSettings settings)
        {
            if (prop.Contains("_MainTex_ST") || prop.Contains("_MainTex2_ST") || prop.Contains("_MainTex3_ST"))
                return settings.UvTolerance;

            if (prop.Contains("_Color") || prop.Contains("_SpecColor") || prop.Contains("_AmbientColor") || prop.Contains("_EmissionColor"))
                return settings.ColorTolerance;

            if (prop.Contains("localPosition") || prop.Contains("m_LocalPosition"))
                return settings.PositionTolerance;

            if (prop.Contains("localScale") || prop.Contains("m_LocalScale"))
                return settings.ScaleTolerance;

            return settings.PositionTolerance;
        }

        private static void ApplyClampedTangents(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }
        #endregion
    }
}