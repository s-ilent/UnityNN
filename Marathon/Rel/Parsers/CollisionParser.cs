using UnityEngine;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public class CollisionSurfaceComponent : MonoBehaviour
    {
        [Tooltip("Surface Material Flags extracted from collision file.")]
        public uint surfaceFlags;
        public Vector4 planeBounds;
    }

    public static class CollisionParser
    {
        public static CollisionMeshData Parse(BinaryReaderEx reader, uint fileSize, uint headerLoc)
        {
            CollisionMeshData data = new CollisionMeshData();

            if (fileSize < 0x20) return data;

            uint polyLoc = 0x10; // Polygon / Box Collider array start in payload (file 0x70)
            uint vtxLoc = 0;
            int vtxCountHeader = 0;

            // 1. Resolve NXR Descriptor from pointer at payload 0x08
            if (fileSize >= 0x10)
            {
                reader.JumpTo(0x08);
                uint rawPtr = reader.ReadUInt32();
                
                // Descriptor struct starts 4 bytes before rawPtr
                uint descPtr = rawPtr >= 4 ? rawPtr - 4 : 0;

                if (descPtr > 0 && descPtr <= fileSize - 20)
                {
                    reader.JumpTo(descPtr);
                    uint quaOff = reader.ReadUInt32();
                    uint polyCountField = reader.ReadUInt32();
                    uint vtxOff = reader.ReadUInt32();
                    uint vtxCountField = reader.ReadUInt32();
                    uint treeOff = reader.ReadUInt32();

                    if (vtxOff < fileSize && vtxCountField > 0 && vtxCountField < 100000)
                    {
                        vtxLoc = vtxOff;
                        vtxCountHeader = (int)vtxCountField;
                    }
                }
            }

            // Fallback vertex array offset
            if (vtxLoc == 0 || vtxLoc >= fileSize)
            {
                vtxLoc = FindVertexArrayOffset(reader, fileSize);
            }

            // 2. Read Vertices (12-byte Vector3)
            if (vtxLoc > 0 && vtxLoc < fileSize)
            {
                reader.JumpTo(vtxLoc);
                int maxPossibleVertices = (int)((fileSize - vtxLoc) / 12);

                for (int i = 0; i < maxPossibleVertices; i++)
                {
                    if (vtxCountHeader > 0 && data.Vertices.Count >= vtxCountHeader) break;
                    if (reader.BaseStream.Position + 12 > fileSize) break;

                    Vector3 pos = reader.ReadVector3();

                    if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
                        float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z))
                    {
                        break;
                    }

                    if (Mathf.Abs(pos.x) > 100000f || Mathf.Abs(pos.y) > 100000f || Mathf.Abs(pos.z) > 100000f)
                    {
                        break;
                    }

                    data.Vertices.Add(pos);
                }
            }

            // 3. Read 28-Byte Records (Polygons / Bounding Volumes)
            if (polyLoc < fileSize && data.Vertices.Count > 0)
            {
                reader.JumpTo(polyLoc);
                int maxPossiblePolys = (int)((vtxLoc - polyLoc) / 28);

                for (int i = 0; i < maxPossiblePolys; i++)
                {
                    if (reader.BaseStream.Position + 28 > fileSize) break;

                    uint flags = reader.ReadUInt32();
                    ushort v0 = reader.ReadUInt16();
                    ushort v1 = reader.ReadUInt16();
                    ushort v2 = reader.ReadUInt16();
                    ushort v3 = reader.ReadUInt16();
                    Vector4 bounds = reader.ReadVector4();

                    // Reached BVH spatial tree boundary
                    if (v0 >= data.Vertices.Count || v1 >= data.Vertices.Count || 
                        v2 >= data.Vertices.Count || v3 >= data.Vertices.Count)
                    {
                        break;
                    }

                    if (float.IsNaN(bounds.x) || float.IsNaN(bounds.y) || float.IsNaN(bounds.z) || float.IsNaN(bounds.w) ||
                        float.IsInfinity(bounds.x) || float.IsInfinity(bounds.y) || float.IsInfinity(bounds.z) || float.IsInfinity(bounds.w))
                    {
                        break;
                    }

                    CollisionPolygon poly = new CollisionPolygon();
                    poly.Flags = flags;
                    poly.VertexIndices[0] = v0;
                    poly.VertexIndices[1] = v1;
                    poly.VertexIndices[2] = v2;
                    poly.VertexIndices[3] = v3;
                    poly.Plane = bounds;

                    data.Polygons.Add(poly);
                }
            }

            return data;
        }

        private static uint FindVertexArrayOffset(BinaryReaderEx reader, uint fileSize)
        {
            long origPos = reader.BaseStream.Position;
            try
            {
                reader.JumpTo(0);
                byte[] payload = reader.ReadBytes((int)fileSize);

                for (int i = 0; i < payload.Length - 8; i++)
                {
                    if (payload[i] == 0x71 && payload[i + 1] == 0x75 && 
                        payload[i + 2] == 0x61 && payload[i + 3] == 0x00)
                    {
                        return (uint)(i + 8);
                    }
                }
            }
            finally
            {
                reader.JumpTo(origPos);
            }

            return 0x518C;
        }

        // Builds Mesh, Box Colliders for planes/volumes, and surface tags
        public static Mesh CreateUnityMeshAndColliders(CollisionMeshData colData, float scale, string name, GameObject parentGO)
        {
            if (colData == null || colData.Vertices == null || colData.Vertices.Count == 0) return null;

            Vector3[] positions = new Vector3[colData.Vertices.Count];
            for (int i = 0; i < colData.Vertices.Count; i++)
            {
                Vector3 pos = colData.Vertices[i];
                pos.x *= -1f * scale;
                pos.y *= scale;
                pos.z *= scale;
                positions[i] = pos;
            }

            List<int> triangles = new List<int>();

            // Child container for generated Box Colliders
            GameObject boxesContainer = new GameObject("BoxColliders");
            boxesContainer.transform.SetParent(parentGO.transform, false);

            for (int i = 0; i < colData.Polygons.Count; i++)
            {
                var poly = colData.Polygons[i];
                if (poly == null || poly.VertexIndices == null || poly.VertexIndices.Length < 3) continue;

                ushort v0 = poly.VertexIndices[0];
                ushort v1 = poly.VertexIndices[1];
                ushort v2 = poly.VertexIndices[2];
                ushort v3 = poly.VertexIndices.Length > 3 ? poly.VertexIndices[3] : v2;

                if (v0 < positions.Length && v1 < positions.Length && v2 < positions.Length)
                {
                    Vector3 p0 = positions[v0];
                    Vector3 p1 = positions[v1];
                    Vector3 p2 = positions[v2];
                    Vector3 p3 = v3 < positions.Length ? positions[v3] : p2;

                    // 1. Dynamic Quad Triangulation (Prevents Bowtie/Twisted Quad Connections)
                    if (v2 == v3 || v3 == v1) // Triangle
                    {
                        triangles.Add(v0);
                        triangles.Add(v2);
                        triangles.Add(v1);
                    }
                    else // Quad: Select diagonal that produces matching, co-planar triangle normals
                    {
                        Vector3 n1A = Vector3.Cross(p2 - p0, p1 - p0);
                        Vector3 n2A = Vector3.Cross(p3 - p0, p2 - p0);
                        float dotA = Vector3.Dot(n1A, n2A);

                        Vector3 n1B = Vector3.Cross(p3 - p0, p1 - p0);
                        Vector3 n2B = Vector3.Cross(p3 - p1, p2 - p1);
                        float dotB = Vector3.Dot(n1B, n2B);

                        if (dotA >= dotB)
                        {
                            triangles.Add(v0); triangles.Add(v2); triangles.Add(v1);
                            triangles.Add(v0); triangles.Add(v3); triangles.Add(v2);
                        }
                        else
                        {
                            triangles.Add(v0); triangles.Add(v3); triangles.Add(v1);
                            triangles.Add(v1); triangles.Add(v3); triangles.Add(v2);
                        }
                    }

                    // 2. Import Plane / Volume as BoxCollider
                    float pMinX = Mathf.Min(Mathf.Min(p0.x, p1.x), Mathf.Min(p2.x, p3.x));
                    float pMaxX = Mathf.Max(Mathf.Max(p0.x, p1.x), Mathf.Max(p2.x, p3.x));
                    float pMinY = Mathf.Min(Mathf.Min(p0.y, p1.y), Mathf.Min(p2.y, p3.y));
                    float pMaxY = Mathf.Max(Mathf.Max(p0.y, p1.y), Mathf.Max(p2.y, p3.y));
                    float pMinZ = Mathf.Min(Mathf.Min(p0.z, p1.z), Mathf.Min(p2.z, p3.z));
                    float pMaxZ = Mathf.Max(Mathf.Max(p0.z, p1.z), Mathf.Max(p2.z, p3.z));

                    Vector3 center = new Vector3((pMinX + pMaxX) * 0.5f, (pMinY + pMaxY) * 0.5f, (pMinZ + pMaxZ) * 0.5f);
                    Vector3 size = new Vector3(Mathf.Max(0.1f, pMaxX - pMinX), Mathf.Max(0.1f, pMaxY - pMinY), Mathf.Max(0.1f, pMaxZ - pMinZ));

                    GameObject boxGO = new GameObject($"BoxCollider_{i:000}_Flags_{poly.Flags:X}");
                    boxGO.transform.SetParent(boxesContainer.transform, false);
                    boxGO.transform.localPosition = center;

                    BoxCollider box = boxGO.AddComponent<BoxCollider>();
                    box.size = size;

                    CollisionSurfaceComponent surfComp = boxGO.AddComponent<CollisionSurfaceComponent>();
                    surfComp.surfaceFlags = poly.Flags;
                    surfComp.planeBounds = poly.Plane;
                }
            }

            if (triangles.Count < 3) return null;

            Mesh mesh = new Mesh { name = name };
            if (positions.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = positions;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}