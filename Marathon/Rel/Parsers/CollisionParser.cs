using UnityEngine;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class CollisionParser
    {
        public static CollisionMeshData Parse(BinaryReaderEx reader, uint fileSize, uint headerLoc)
        {
            CollisionMeshData data = new CollisionMeshData();

            if (headerLoc + 16 > fileSize) return data;

            // 1. Read Header Pointers & Counts
            reader.JumpTo(headerLoc);
            uint val0 = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);
            uint val1 = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);
            uint val2 = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);
            uint val3 = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);

            // Dynamically identify if this represents a global vertex-indexed mesh
            uint vtxLoc = 0;
            int vtxCount = 0;
            int polyCount = 0;

            List<uint> candidateOffsets = new List<uint> { val0, val1, val2, val3 };
            List<int> candidateCounts = new List<int> { (int)val0, (int)val1, (int)val2, (int)val3 };

            // Find the most valid global vertex array among the offsets (if any)
            foreach (uint opt in candidateOffsets)
            {
                foreach (int count in candidateCounts)
                {
                    if (count > 4 && count < 100000 && IsValidVertexArray(reader, opt, count, fileSize))
                    {
                        vtxLoc = opt;
                        vtxCount = count;
                        break;
                    }
                }
                if (vtxLoc != 0) break;
            }

            // Detect polygon count for global mesh
            if (vtxLoc != 0)
            {
                foreach (int count in candidateCounts)
                {
                    if (count > 0 && count != vtxCount && count < vtxCount)
                    {
                        polyCount = count;
                        break;
                    }
                }

                uint polyLoc = 0x10; // The polygon array always starts immediately after the 16-byte chunk header
                int calculatedPolyCount = (int)((vtxLoc - 0x10) / 28);
                if (polyCount <= 0 || polyCount > calculatedPolyCount)
                {
                    polyCount = calculatedPolyCount;
                }

                // Read Polygons (28 bytes each)
                if (polyCount > 0 && polyLoc < fileSize)
                {
                    reader.JumpTo(polyLoc);
                    for (int i = 0; i < polyCount; i++)
                    {
                        if (reader.BaseStream.Position + 28 > fileSize) break;

                        CollisionPolygon poly = new CollisionPolygon();
                        poly.Flags = reader.ReadUInt32();
                        poly.VertexIndices[0] = reader.ReadUInt16();
                        poly.VertexIndices[1] = reader.ReadUInt16();
                        poly.VertexIndices[2] = reader.ReadUInt16();
                        poly.VertexIndices[3] = reader.ReadUInt16();
                        poly.Plane = reader.ReadVector4();

                        data.Polygons.Add(poly);
                    }
                }

                // Read Vertices (Vector3: 12 bytes each)
                if (vtxCount > 0 && vtxLoc > 0 && vtxLoc < fileSize)
                {
                    reader.JumpTo(vtxLoc);
                    for (int i = 0; i < vtxCount; i++)
                    {
                        if (reader.BaseStream.Position + 12 > fileSize) break;
                        data.Vertices.Add(reader.ReadVector3());
                    }
                }
            }

            // 2b. Primitive-Based Collider Parser (Scan for 'qua\0' or 'tri\0' chunks)
            if (data.Vertices.Count == 0)
            {
                reader.JumpTo(0);
                byte[] payload = reader.ReadBytes((int)fileSize);

                int idx = 0;
                while (idx < payload.Length - 4)
                {
                    // Scan for quad primitive ("qua\0")
                    if (payload[idx] == 0x71 && payload[idx + 1] == 0x75 && payload[idx + 2] == 0x61 && payload[idx + 3] == 0x00)
                    {
                        int vtxOffset = idx + 8;
                        if (vtxOffset + 48 <= payload.Length)
                        {
                            ushort startVtxIdx = (ushort)data.Vertices.Count;

                            for (int j = 0; j < 4; j++)
                            {
                                float vx = System.BitConverter.ToSingle(payload, vtxOffset + j * 12);
                                float vy = System.BitConverter.ToSingle(payload, vtxOffset + j * 12 + 4);
                                float vz = System.BitConverter.ToSingle(payload, vtxOffset + j * 12 + 8);
                                data.Vertices.Add(new Vector3(vx, vy, vz));
                            }

                            CollisionPolygon poly = new CollisionPolygon();
                            poly.Flags = 0x01010101;
                            poly.VertexIndices[0] = startVtxIdx;
                            poly.VertexIndices[1] = (ushort)(startVtxIdx + 1);
                            poly.VertexIndices[2] = (ushort)(startVtxIdx + 2);
                            poly.VertexIndices[3] = (ushort)(startVtxIdx + 3);
                            data.Polygons.Add(poly);
                        }
                    }
                    // Scan for triangle primitive ("tri\0")
                    else if (payload[idx] == 0x74 && payload[idx + 1] == 0x72 && payload[idx + 2] == 0x69 && payload[idx + 3] == 0x00)
                    {
                        int vtxOffset = idx + 8;
                        if (vtxOffset + 36 <= payload.Length)
                        {
                            ushort startVtxIdx = (ushort)data.Vertices.Count;

                            for (int j = 0; j < 3; j++)
                            {
                                float vx = System.BitConverter.ToSingle(payload, vtxOffset + j * 12);
                                float vy = System.BitConverter.ToSingle(payload, vtxOffset + j * 12 + 4);
                                float vz = System.BitConverter.ToSingle(payload, vtxOffset + j * 12 + 8);
                                data.Vertices.Add(new Vector3(vx, vy, vz));
                            }

                            CollisionPolygon poly = new CollisionPolygon();
                            poly.Flags = 0x01010101;
                            poly.VertexIndices[0] = startVtxIdx;
                            poly.VertexIndices[1] = (ushort)(startVtxIdx + 1);
                            poly.VertexIndices[2] = (ushort)(startVtxIdx + 2);
                            poly.VertexIndices[3] = (ushort)(startVtxIdx + 2);
                            data.Polygons.Add(poly);
                        }
                    }
                    idx++;
                }
            }

            return data;
        }

        private static bool IsValidVertexArray(BinaryReaderEx reader, uint offset, int count, uint fileSize)
        {
            if (offset < 0x10 || offset >= fileSize) return false;
            long origPos = reader.BaseStream.Position;
            try
            {
                reader.JumpTo(offset);
                for (int i = 0; i < Mathf.Min(count, 5); i++)
                {
                    if (reader.BaseStream.Position + 12 > fileSize) break;
                    Vector3 v = reader.ReadVector3();
                    if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                        float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                    {
                        return false;
                    }
                    if ((v.x != 0 && Mathf.Abs(v.x) < 1e-10f) || 
                        (v.y != 0 && Mathf.Abs(v.y) < 1e-10f) || 
                        (v.z != 0 && Mathf.Abs(v.z) < 1e-10f))
                    {
                        return false;
                    }
                }
                return true;
            }
            finally
            {
                reader.JumpTo(origPos);
            }
        }

        public static Mesh CreateUnityMesh(CollisionMeshData colData, float scale, string name)
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
            foreach (var poly in colData.Polygons)
            {
                if (poly == null || poly.VertexIndices == null || poly.VertexIndices.Length < 3) continue;

                ushort v0 = poly.VertexIndices[0];
                ushort v1 = poly.VertexIndices[1];
                ushort v2 = poly.VertexIndices[2];
                ushort v3 = poly.VertexIndices.Length > 3 ? poly.VertexIndices[3] : v2;

                if (v0 < positions.Length && v1 < positions.Length && v2 < positions.Length)
                {
                    if (v0 != v1 && v1 != v2 && v0 != v2)
                    {
                        triangles.Add(v0);
                        triangles.Add(v2);
                        triangles.Add(v1);
                    }

                    if (v3 < positions.Length && v3 != v0 && v3 != v1 && v3 != v2)
                    {
                        triangles.Add(v0);
                        triangles.Add(v3);
                        triangles.Add(v2);
                    }
                }
            }

            if (triangles.Count < 3) return null;

            Mesh mesh = new Mesh { name = name };
            mesh.vertices = positions;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}