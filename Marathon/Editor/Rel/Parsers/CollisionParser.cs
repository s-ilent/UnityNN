// File: Marathon/Rel/Parsers/CollisionParser.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    /// <summary>
    /// Parser for NXR/XNR collision geometry files.
    /// Extracts vertices, bounding boxes, and 16-byte triangle face records with surface bitmasks.
    /// </summary>
    public static class CollisionParser
    {
        public static CollisionMeshData Parse(BinaryReaderEx reader, uint fileSize, uint headerLoc)
        {
            CollisionMeshData data = new CollisionMeshData();
            if (fileSize < 20) return data;

            // 1. Container detection: check for NXR\0 magic at 0x60 (XNR container) or 0x00 (raw NXR)
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
                    long scanLimit = Math.Min((long)fileSize - 4, 0x100);
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

            // 2. Find 'qua\0' (0x00617571) anchor marker
            long quaIdx = FindQuaMarkerOffset(reader, payloadStart, n);
            if (quaIdx < 0) return data;

            // 3. Resolve descriptor pointer at payload offset 0x08
            reader.JumpTo(payloadStart + 0x08);
            int rawPtr = reader.ReadInt32();

            // Try direct offset first; if out of bounds, rebase
            uint descOff = 0;
            if (rawPtr > 4 && (uint)(rawPtr - 4) <= n - 20)
            {
                descOff = (uint)(rawPtr - 4);
            }
            else
            {
                // Rebase descriptor address
                uint testBase = RelResolver.ComputeBaseAddress(reader, headerLoc, fileSize);
                if (RelResolver.TryResolveOffset(rawPtr - 4, n, testBase, out uint resDesc))
                {
                    descOff = resDesc;
                }
                else
                {
                    // Fallback: descriptor is located immediately before vertex/qua data or at headerLoc
                    descOff = (quaIdx >= 20) ? (uint)(quaIdx - 20) : 0x10;
                }
            }

            if (descOff + 20 > n) return data;

            // 4. Read 5 x uint32 descriptor
            reader.JumpTo(payloadStart + descOff);
            uint quaPtr = reader.ReadUInt32();
            uint vertexCount = reader.ReadUInt32();
            uint vertexPtr = reader.ReadUInt32();
            uint faceCount = reader.ReadUInt32();
            uint facePtr = reader.ReadUInt32();

            // 5. Rebase pointers using qua marker
            uint baseAddr = (quaPtr >= (uint)quaIdx) ? quaPtr - (uint)quaIdx : 0;
            uint vertexOff = (vertexPtr >= baseAddr && (vertexPtr - baseAddr) < n) ? vertexPtr - baseAddr : (uint)(quaIdx + 8);
            uint faceOff = (facePtr >= baseAddr && (facePtr - baseAddr) < n) ? facePtr - baseAddr : (vertexOff + vertexCount * 12);

            if (vertexOff >= faceOff || faceOff > n) return data;

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

        public static Mesh CreateUnityMesh(
            CollisionMeshData colData,
            float scale,
            string name,
            out Material[] outMaterials,
            out ushort[] outSubMeshMaterialIDs,
            out ushort[] outTriangleMaterialIDs)
        {
            outMaterials = new Material[0];
            outSubMeshMaterialIDs = new ushort[0];
            outTriangleMaterialIDs = new ushort[0];

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

            // Group triangles by MaterialID (surface bitmask flags) into distinct submeshes
            Dictionary<ushort, List<int>> materialTriangles = new Dictionary<ushort, List<int>>();
            Dictionary<ushort, List<ushort>> materialTriMatIds = new Dictionary<ushort, List<ushort>>();

            for (int i = 0; i < colData.Triangles.Count; i++)
            {
                var tri = colData.Triangles[i];
                ushort matId = tri.MaterialID;

                if (!materialTriangles.ContainsKey(matId))
                {
                    materialTriangles[matId] = new List<int>();
                    materialTriMatIds[matId] = new List<ushort>();
                }

                // Invert winding (v0, v2, v1) for left-handed Unity coordinate conversion (x = -x)
                materialTriangles[matId].Add(tri.VertexIndex0);
                materialTriangles[matId].Add(tri.VertexIndex2);
                materialTriangles[matId].Add(tri.VertexIndex1);
                materialTriMatIds[matId].Add(matId);
            }

            List<ushort> sortedMatKeys = new List<ushort>(materialTriangles.Keys);
            sortedMatKeys.Sort();

            Mesh mesh = new Mesh { name = name };
            if (positions.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = positions;
            mesh.subMeshCount = sortedMatKeys.Count;

            Material[] materials = new Material[sortedMatKeys.Count];
            ushort[] subMeshMatIds = new ushort[sortedMatKeys.Count];
            List<ushort> flattenedTriMatIds = new List<ushort>(colData.Triangles.Count);

            Shader debugShader = Shader.Find("Standard") ?? Shader.Find("Diffuse");

            for (int s = 0; s < sortedMatKeys.Count; s++)
            {
                ushort mKey = sortedMatKeys[s];
                mesh.SetTriangles(materialTriangles[mKey], s);
                subMeshMatIds[s] = mKey;
                flattenedTriMatIds.AddRange(materialTriMatIds[mKey]);

                CollisionSurfaceFlags flagEnum = (CollisionSurfaceFlags)mKey;
                string flagName = flagEnum == CollisionSurfaceFlags.Default ? "Default" : flagEnum.ToString().Replace(", ", "_");

                Material mat = new Material(debugShader) { name = $"CollisionSurface_{flagName}_0x{mKey:X4}" };
                mat.color = GetMaterialPaletteColor(mKey);
                materials[s] = mat;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            outMaterials = materials;
            outSubMeshMaterialIDs = subMeshMatIds;
            outTriangleMaterialIDs = flattenedTriMatIds.ToArray();

            return mesh;
        }

        public static Mesh CreateUnityMeshAndColliders(
            CollisionMeshData colData,
            float scale,
            string name,
            GameObject parentGO,
            UnityEditor.AssetImporters.AssetImportContext ctx = null)
        {
            Mesh mesh = CreateUnityMesh(
                colData,
                scale,
                name,
                out Material[] materials,
                out ushort[] subMeshMatIDs,
                out ushort[] triMatIDs
            );

            if (mesh != null && parentGO != null)
            {
                if (ctx != null)
                {
                    ctx.AddObjectToAsset("CollisionMesh", mesh);
                    for (int m = 0; m < materials.Length; m++)
                    {
                        if (materials[m] != null)
                        {
                            ctx.AddObjectToAsset($"ColliMat_{subMeshMatIDs[m]}", materials[m]);
                        }
                    }
                }

                MeshFilter mf = parentGO.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                MeshRenderer mr = parentGO.AddComponent<MeshRenderer>();
                mr.sharedMaterials = materials;

                MeshCollider mc = parentGO.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;

                CollisionSurfaceComponent surfComp = parentGO.AddComponent<CollisionSurfaceComponent>();
                surfComp.vertexCount = colData.Vertices.Count;
                surfComp.triangleCount = colData.Triangles.Count;
                surfComp.subMeshMaterialIDs = subMeshMatIDs;
                surfComp.triangleMaterialIDs = triMatIDs;
                if (colData.BoundingBoxMin.HasValue) surfComp.boundingBoxMin = colData.BoundingBoxMin.Value * scale;
                if (colData.BoundingBoxMax.HasValue) surfComp.boundingBoxMax = colData.BoundingBoxMax.Value * scale;
            }

            return mesh;
        }

        private static Color GetMaterialPaletteColor(ushort materialId)
        {
            if (materialId == 0) return new Color(0.7f, 0.7f, 0.7f, 0.75f); // Grey (Wall/Dirt/Concrete/Hole)

            if ((materialId & (ushort)CollisionSurfaceFlags.Grass) != 0)
                return new Color(0.2f, 0.8f, 0.2f, 0.75f); // Green (草)

            if ((materialId & (ushort)CollisionSurfaceFlags.Water) != 0)
                return new Color(0.2f, 0.5f, 0.95f, 0.75f); // Blue (水)

            if ((materialId & (ushort)CollisionSurfaceFlags.Sand) != 0)
                return new Color(0.95f, 0.85f, 0.3f, 0.75f); // Sand Yellow (砂)

            if ((materialId & (ushort)CollisionSurfaceFlags.Footprint) != 0)
                return new Color(0.95f, 0.55f, 0.15f, 0.75f); // Footprint Orange (足跡)

            if ((materialId & (ushort)CollisionSurfaceFlags.Metal) != 0)
                return new Color(0.2f, 0.85f, 0.85f, 0.75f); // Metal Cyan (鉄)

            return Color.HSVToRGB(((materialId * 53) % 360) / 360f, 0.65f, 0.85f);
        }
    }
}