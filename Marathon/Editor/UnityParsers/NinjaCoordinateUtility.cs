// File: Marathon/Editor/UnityParsers/NinjaCoordinateUtility.cs
using UnityEngine;
using System;

namespace SilentTools
{
    /// <summary>
    /// Centralized coordinate space and math transformations between Sega NN (Direct3D/right-handed)
    /// and Unity (left-handed) coordinate systems.
    /// </summary>
    public static class NinjaCoordinateUtility
    {
        /// <summary>
        /// Transforms a 3D position vector from Ninja space to Unity space with scaling.
        /// Inverts the X axis (X -> -X).
        /// </summary>
        public static Vector3 ToUnityPosition(Vector3 position, float scale = 1.0f)
        {
            float x = float.IsNaN(position.x) || float.IsInfinity(position.x) ? 0f : -position.x * scale;
            float y = float.IsNaN(position.y) || float.IsInfinity(position.y) ? 0f : position.y * scale;
            float z = float.IsNaN(position.z) || float.IsInfinity(position.z) ? 0f : position.z * scale;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Transforms Euler rotation angles (in degrees) from Ninja space to Unity space.
        /// Inverts Y and Z rotation axes (Y -> -Y, Z -> -Z).
        /// </summary>
        public static Vector3 ToUnityEuler(Vector3 eulerDegrees)
        {
            float x = float.IsNaN(eulerDegrees.x) || float.IsInfinity(eulerDegrees.x) ? 0f : eulerDegrees.x;
            float y = float.IsNaN(eulerDegrees.y) || float.IsInfinity(eulerDegrees.y) ? 0f : -eulerDegrees.y;
            float z = float.IsNaN(eulerDegrees.z) || float.IsInfinity(eulerDegrees.z) ? 0f : -eulerDegrees.z;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Transforms a normal vector from Ninja space to Unity space (X -> -X).
        /// </summary>
        public static Vector3 ToUnityNormal(Vector3 normal)
        {
            float x = float.IsNaN(normal.x) || float.IsInfinity(normal.x) ? 0f : -normal.x;
            float y = float.IsNaN(normal.y) || float.IsInfinity(normal.y) ? 1f : normal.y;
            float z = float.IsNaN(normal.z) || float.IsInfinity(normal.z) ? 0f : normal.z;
            return new Vector3(x, y, z).normalized;
        }

        /// <summary>
        /// Transforms a tangent vector from Ninja space to Unity space (X -> -X).
        /// </summary>
        public static Vector4 ToUnityTangent(Vector3 tangent, float w = 1.0f)
        {
            float x = float.IsNaN(tangent.x) || float.IsInfinity(tangent.x) ? 1f : -tangent.x;
            float y = float.IsNaN(tangent.y) || float.IsInfinity(tangent.y) ? 0f : tangent.y;
            float z = float.IsNaN(tangent.z) || float.IsInfinity(tangent.z) ? 0f : tangent.z;
            Vector3 tanScaled = new Vector3(x, y, z).normalized;
            return new Vector4(tanScaled.x, tanScaled.y, tanScaled.z, w);
        }

        /// <summary>
        /// Transforms texture coordinates from Direct3D top-left origin (0,0) to Unity bottom-left origin (0,0).
        /// (U -> U, V -> 1.0 - V).
        /// </summary>
        public static Vector2 ToUnityUV(Vector2 uv)
        {
            float u = float.IsNaN(uv.x) || float.IsInfinity(uv.x) ? 0f : uv.x;
            float v = float.IsNaN(uv.y) || float.IsInfinity(uv.y) ? 0f : 1.0f - uv.y;
            return new Vector2(u, v);
        }

        /// <summary>
        /// Transforms UV offset values for Unity shader properties.
        /// Inverts V offset (U -> U, V -> -V).
        /// </summary>
        public static Vector2 ToUnityUVOffset(Vector2 offset)
        {
            float u = float.IsNaN(offset.x) || float.IsInfinity(offset.x) ? 0f : offset.x;
            float v = float.IsNaN(offset.y) || float.IsInfinity(offset.y) ? 0f : -offset.y;
            return new Vector2(u, v);
        }
    }
}