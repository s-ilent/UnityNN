// File: Marathon/Ninja/NinjaNode.cs
using UnityEngine;
using System.Collections.Generic;
using Marathon.IO;

namespace Marathon.Formats.Mesh.Ninja
{
    /// <summary>
    /// Structure of a Ninja Object Node entry.
    /// </summary>
    public class NinjaNode
    {
        /// <summary>
        /// Not actually part of the Node, used purely so we don't keep having to go back
        /// and forth between the Node Name List and the Nodes themselves.
        /// </summary>
        public string Name { get; set; }

        public NodeType Type { get; set; }

        public short MatrixIndex { get; set; } = -1;

        public short ParentIndex { get; set; } = -1;

        public short ChildIndex { get; set; } = -1;

        public short SiblingIndex { get; set; } = -1;

        public Vector3 Translation { get; set; }

        /// <summary>
        /// Euler rotation angles stored in degrees (converted from 32-bit BAMS).
        /// </summary>
        public Vector3 Rotation { get; set; }

        public Vector3 Scaling { get; set; }

        public Matrix4x4 InvInitMatrix { get; set; }

        public Vector3 Center { get; set; }

        public float Radius { get; set; }

        public uint UserDefined { get; set; }

        public Vector3 BoundingBox { get; set; }

        public override string ToString() => Name;

        /// <summary>
        /// Reads a Ninja Object Node from a file.
        /// </summary>
        /// <param name="reader">The binary reader for this SegaNN file.</param>
        public void Read(BinaryReaderEx reader)
        {
            Type = (NodeType)reader.ReadUInt32();
            MatrixIndex = reader.ReadInt16();
            ParentIndex = reader.ReadInt16();
            ChildIndex = reader.ReadInt16();
            SiblingIndex = reader.ReadInt16();
            Translation = reader.ReadVector3();

            // Sega NN NNS_NODE stores Rotation as 3 x s32 BAMS angles (65536 = 360 deg)
            int rx = reader.ReadInt32();
            int ry = reader.ReadInt32();
            int rz = reader.ReadInt32();
            Rotation = new Vector3(
                rx * (360.0f / 65536.0f),
                ry * (360.0f / 65536.0f),
                rz * (360.0f / 65536.0f)
            );

            Scaling = reader.ReadVector3();
            InvInitMatrix = reader.ReadMatrix();
            Center = reader.ReadVector3();
            Radius = reader.ReadSingle();
            UserDefined = reader.ReadUInt32();
            BoundingBox = reader.ReadVector3();
        }
    }
}