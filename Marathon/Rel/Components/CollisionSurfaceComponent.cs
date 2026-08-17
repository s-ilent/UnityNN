using UnityEngine;
using System;

namespace SilentTools
{
    [Flags]
    public enum CollisionSurfaceFlags : ushort
    {
        Default   = 0,        // 0: Wall, Dirt / Concrete, Carpet, Hole (壁, 土 / コンクリート, 絨毯, 穴)
        Grass     = 1 << 0,   // 1 (0x0001): Grass (草)
        Water     = 1 << 1,   // 2 (0x0002): Water (水)
        Sand      = 1 << 2,   // 4 (0x0004): Sand (砂)
        Footprint = 1 << 3,   // 8 (0x0008): Footprint (足跡)
        Metal     = 1 << 4,   // 16 (0x0010): Metal / Iron (鉄)
        R8Y       = 1 << 6,   // 64 (0x0040): R8Y
        R9Y       = 1 << 7,   // 128 (0x0080): R9Y
        R10Y      = 1 << 8,   // 256 (0x0100): R10Y
        R11Y      = 1 << 9,   // 512 (0x0200): R11Y
        R13Y      = 1 << 10,  // 1024 (0x0400): R13Y
        R14Y      = 1 << 11,  // 2048 (0x0800): R14Y
        R15Y      = 1 << 13,  // 8192 (0x2000): R15Y
        R16Y      = 1 << 14   // 16384 (0x4000): R16Y
    }

    [DisallowMultipleComponent]
    public class CollisionSurfaceComponent : MonoBehaviour
    {
        [Tooltip("Total vertex count in collision mesh.")]
        public int vertexCount;

        [Tooltip("Total triangle count in collision mesh.")]
        public int triangleCount;

        public Vector3 boundingBoxMin;
        public Vector3 boundingBoxMax;

        [Tooltip("Surface Material / Flag IDs mapped to each mesh submesh index.")]
        public ushort[] subMeshMaterialIDs = new ushort[0];

        [Tooltip("Per-triangle Surface Material / Flag ID array for O(1) RaycastHit.triangleIndex lookup.")]
        public ushort[] triangleMaterialIDs = new ushort[0];

        /// <summary>
        /// Retrieves the raw surface bitmask value from a RaycastHit.triangleIndex.
        /// </summary>
        public ushort GetSurfaceMaterialID(int triangleIndex)
        {
            if (triangleMaterialIDs != null && triangleIndex >= 0 && triangleIndex < triangleMaterialIDs.Length)
            {
                return triangleMaterialIDs[triangleIndex];
            }
            return 0;
        }

        /// <summary>
        /// Retrieves the typed CollisionSurfaceFlags enum from a RaycastHit.triangleIndex.
        /// </summary>
        public CollisionSurfaceFlags GetSurfaceFlags(int triangleIndex)
        {
            return (CollisionSurfaceFlags)GetSurfaceMaterialID(triangleIndex);
        }

        /// <summary>
        /// Checks if a hit triangle has a specific surface flag set.
        /// </summary>
        public bool HasSurfaceFlag(int triangleIndex, CollisionSurfaceFlags flag)
        {
            ushort raw = GetSurfaceMaterialID(triangleIndex);
            if (flag == CollisionSurfaceFlags.Default) return raw == 0;
            return (raw & (ushort)flag) != 0;
        }

        /// <summary>
        /// Retrieves the raw surface bitmask value for a given submesh index.
        /// </summary>
        public ushort GetSubMeshMaterialID(int subMeshIndex)
        {
            if (subMeshMaterialIDs != null && subMeshIndex >= 0 && subMeshIndex < subMeshMaterialIDs.Length)
            {
                return subMeshMaterialIDs[subMeshIndex];
            }
            return 0;
        }

        /// <summary>
        /// Retrieves the typed CollisionSurfaceFlags enum for a submesh index.
        /// </summary>
        public CollisionSurfaceFlags GetSubMeshSurfaceFlags(int subMeshIndex)
        {
            return (CollisionSurfaceFlags)GetSubMeshMaterialID(subMeshIndex);
        }

        /// <summary>
        /// Human-readable description of the surface flags for a triangle.
        /// </summary>
        public string GetSurfaceDescription(int triangleIndex)
        {
            CollisionSurfaceFlags flags = GetSurfaceFlags(triangleIndex);
            if (flags == CollisionSurfaceFlags.Default) return "Default (Wall / Dirt / Concrete / Hole)";
            return flags.ToString();
        }
    }
}