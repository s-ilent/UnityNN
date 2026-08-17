using UnityEngine;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public class CollisionSurfaceComponent : MonoBehaviour
    {
        [Tooltip("Surface Material / Feature summary from collision file.")]
        public int vertexCount;
        public int triangleCount;
        public Vector3 boundingBoxMin;
        public Vector3 boundingBoxMax;
    }

    public static class CollisionParser
    {
        public static CollisionMeshData Parse(BinaryReaderEx reader, uint fileSize, uint headerLoc)
        {
            CollisionMeshData data = new CollisionMeshData();

            if (fileSize < 20) return data;

            // 1. Container detection: check for NXR\0 magic at 0x60 (XNR wrapper) or 0x00 (raw NXR)
            uint payloadStart = 0;
            if (fileSize >= 0x64)
            {
                reader.JumpTo(0x60);
                if (reader.ReadByte() == (byte)'N' && reader.ReadByte() == (byte)'X' && 
                    reader.ReadByte() == (byte)'R' && reader.ReadByte() == 0)
                {
                    payloadStart = 0x60;
                }
            }

            if (payloadStart == 0)
            {
                reader.JumpTo(0);
                if (!(reader.ReadByte() == (byte)'N' && reader.ReadByte() == (byte)'X' && 
                      reader.ReadByte() == (byte)'R' && reader.ReadByte() == 0))
                {
                    // Fallback scan in first 0x100 bytes for NXR\0
                    long scanLimit = System.Math.Min((long)fileSize - 4, 0x100);
                    for (long s = 0; s <= scanLimit; s += 4)
                    {
                        reader.JumpTo(s);
                        if (reader.ReadByte() == (byte)'N' && reader.ReadByte() == (byte)'X' && 
                            reader.ReadByte() == (byte)'R' && reader.ReadByte() == 0)
                        {
                            payloadStart = (uint)s;
                            break;
                        }
                    }
                }
            }

            uint n = fileSize - payloadStart;
            if (n < 20) return data;

            // 2. Resolve NXR Descriptor at payload offset 0x08 (desc_off = descriptor_ptr - 4)
            reader.JumpTo(payloadStart + 0x08);
            uint rawPtr = reader.ReadUInt32();
            uint descOff = rawPtr >= 4 ? rawPtr - 4 : 0;

            if (descOff == 0 || descOff + 20 > n)
            {
                return data;
            }

            // 3. Read 5 x uint32 descriptor
            reader.JumpTo(payloadStart + descOff);
            uint quaPtr = reader.ReadUInt32();
            uint vertexCount = reader.ReadUInt32();
            uint vertexPtr = reader.ReadUInt32();
            uint faceCount = reader.ReadUInt32();
            uint facePtr = reader.ReadUInt32();

            // 4. Find 'qua\0' (0x00617571) anchor marker
            long quaIdx = FindQuaMarkerOffset(reader, payloadStart, n);
            if (quaIdx < 0)
            {
                return data;
            }

            // 5. Rebase pointers
            uint baseAddr = quaPtr - (uint)quaIdx;
            uint vertexOff = vertexPtr - baseAddr;
            uint faceOff = facePtr - baseAddr;

            if (vertexOff >= faceOff || faceOff > n)
            {
                return data;
            }

            // 6. Read Bounding Box extents if present
            if (descOff + 48 <= n)
            {
                reader.JumpTo(payloadStart + descOff + 20);
                data.BoundingBoxMin = reader.ReadVector3();
                data.BoundingBoxMax = reader.ReadVector3();
            }

            // 7. Decode packed vertex array (12 bytes per vertex)
            reader.JumpTo(payloadStart + vertexOff);
            for (uint i = 0; i < vertexCount; i++)
            {
                if (reader.BaseStream.Position + 12 > payloadStart + faceOff) break;

                Vector3 pos = reader.ReadVector3();
                if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
                    float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z) ||
                    Mathf.Abs(pos.x) > 1e6f || Mathf.Abs(pos.y) > 1e6f || Mathf.Abs(pos.z) > 1e6f)
                {
                    break;
                }

                data.Vertices.Add(pos);
            }

            // 8. Decode 16-byte triangle face array (8 x uint16 per face)
            reader.JumpTo(payloadStart + faceOff);
            int actualVtxCount = data.Vertices.Count;

            for (uint i = 0; i < faceCount; i++)
            {
                if (reader.BaseStream.Position + 16 > payloadStart + n) break;

                ushort v0 = reader.ReadUInt16();
                ushort v1 = reader.ReadUInt16();
                ushort v2 = reader.ReadUInt16();
                ushort reserved = reader.ReadUInt16();
                ushort adj0 = reader.ReadUInt16();
                ushort materialId = reader.ReadUInt16();
                ushort adj1 = reader.ReadUInt16();
                ushort adj2 = reader.ReadUInt16();

                if (v0 < actualVtxCount && v1 < actualVtxCount && v2 < actualVtxCount &&
                    v0 != v1 && v1 != v2 && v0 != v2)
                {
                    data.Triangles.Add(new CollisionTriangle
                    {
                        VertexIndex0 = v0,
                        VertexIndex1 = v1,
                        VertexIndex2 = v2,
                        MaterialID = materialId,
                        Adjacency0 = adj0,
                        Adjacency1 = adj1,
                        Adjacency2 = adj2
                    });
                }
                else
                {
                    break;
                }
            }

            return data;
        }

        private static long FindQuaMarkerOffset(BinaryReaderEx reader, uint payloadStart, uint payloadLength)
        {
            long origPos = reader.BaseStream.Position;
            try
            {
                reader.JumpTo(payloadStart);
                byte[] bytes = reader.ReadBytes((int)payloadLength);

                for (int i = 0; i < bytes.Length - 4; i++)
                {
                    if (bytes[i] == 0x71 && bytes[i + 1] == 0x75 && 
                        bytes[i + 2] == 0x61 && bytes[i + 3] == 0x00)
                    {
                        return i;
                    }
                }
            }
            finally
            {
                reader.JumpTo(origPos);
            }

            return -1;
        }

        public static Mesh CreateUnityMesh(CollisionMeshData colData, float scale, string name)
        {
            if (colData == null || colData.Vertices == null || colData.Vertices.Count == 0 ||
                colData.Triangles == null || colData.Triangles.Count == 0)
            {
                return null;
            }

            Vector3[] positions = new Vector3[colData.Vertices.Count];
            for (int i = 0; i < colData.Vertices.Count; i++)
            {
                Vector3 pos = colData.Vertices[i];
                pos.x *= -1f * scale;
                pos.y *= scale;
                pos.z *= scale;
                positions[i] = pos;
            }

            List<int> triangles = new List<int>(colData.Triangles.Count * 3);
            for (int i = 0; i < colData.Triangles.Count; i++)
            {
                var tri = colData.Triangles[i];
                // Invert triangle winding (v0, v2, v1) for left-handed Unity coordinate conversion (x = -x)
                triangles.Add(tri.VertexIndex0);
                triangles.Add(tri.VertexIndex2);
                triangles.Add(tri.VertexIndex1);
            }

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

        public static Mesh CreateUnityMeshAndColliders(CollisionMeshData colData, float scale, string name, GameObject parentGO)
        {
            Mesh mesh = CreateUnityMesh(colData, scale, name);

            if (mesh != null && parentGO != null)
            {
                CollisionSurfaceComponent surfComp = parentGO.AddComponent<CollisionSurfaceComponent>();
                surfComp.vertexCount = colData.Vertices.Count;
                surfComp.triangleCount = colData.Triangles.Count;
                if (colData.BoundingBoxMin.HasValue) surfComp.boundingBoxMin = colData.BoundingBoxMin.Value * scale;
                if (colData.BoundingBoxMax.HasValue) surfComp.boundingBoxMax = colData.BoundingBoxMax.Value * scale;
            }

            return mesh;
        }
    }
}