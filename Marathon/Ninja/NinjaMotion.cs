// File: Marathon/Ninja/NinjaMotion.cs
using System.Collections.Generic;
using Marathon.IO;
using System;

namespace Marathon.Formats.Mesh.Ninja
{
    /// <summary>
    /// Structure of the main Ninja Motion data.
    /// </summary>
    public class NinjaMotion
    {
        /// <summary>
        /// NinjaMotion is used by a lot of things, so we store the Chunk ID so we know what to write back.
        /// </summary>
        public string ChunkID { get; set; }

        public MotionType Type { get; set; }

        public float StartFrame { get; set; }

        public float EndFrame { get; set; }

        public List<NinjaSubMotion> SubMotions { get; set; } = new List<NinjaSubMotion>();

        public float Framerate { get; set; }

        public uint Reserved0 { get; set; }

        public uint Reserved1 { get; set; }

        /// <summary>
        /// Reads the Ninja Motion data from a file.
        /// </summary>
        /// <param name="reader">The binary reader for this SegaNN file.</param>
        public void Read(BinaryReaderEx reader)
        {
            // Read the offset to the actual Ninja Motion data.
            uint dataOffset = reader.ReadUInt32();

            // Jump to the actual Ninja Motion data.
            reader.JumpTo(dataOffset, true);

            // Read all of the data from the Ninja Motion data.
            Type = (MotionType)reader.ReadUInt32();
            StartFrame = reader.ReadSingle();
            EndFrame = reader.ReadSingle();
            uint SubMotionCount = reader.ReadUInt32();
            uint SubMotionsOffset = reader.ReadUInt32();
            Framerate = reader.ReadSingle();
            Reserved0 = reader.ReadUInt32();
            Reserved1 = reader.ReadUInt32();

            // Jump to the offset for this motion data's sub motions.
            reader.JumpTo(SubMotionsOffset, true);

            // Loop through and read all sub motions with parent motion type context.
            for (int i = 0; i < SubMotionCount; i++)
            {
                NinjaSubMotion subMotion = new NinjaSubMotion();
                subMotion.Read(reader, Type);
                SubMotions.Add(subMotion);
            }
        }

        /// <summary>
        /// Write the Ninja Motion data to a file.
        /// </summary>
        /// <param name="writer">The binary writer for this SegaNN file.</param>
        public void Write(BinaryWriterEx writer)
        {
            // Set up a list of offsets for earlier points in the chunk.
            Dictionary<string, uint> MotionOffsets = new Dictionary<string, uint>();

            // Write chunk header.
            writer.Write(ChunkID);
            writer.Write("SIZE"); // Temporary entry, is filled in later once we know this chunk's size.
            writer.Write("SIZE");
            long HeaderSizePosition = writer.BaseStream.Position;
            writer.AddOffset("dataOffset");
            writer.FixPadding(0x10);

            bool isNodeMotion = (Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_NODE || (Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == 0;
            bool isMaterialMotion = (Type & MotionType.NND_MOTIONTYPE_CATEGORY_MASK) == MotionType.NND_MOTIONTYPE_MATERIAL;

            // Keyframes.
            for (int i = 0; i < SubMotions.Count; i++)
            {
                MotionOffsets.Add($"SubMotion{i}KeyframesOffset", (uint)writer.BaseStream.Position);

                var smType = SubMotions[i].Type;
                for (int k = 0; k < SubMotions[i].Keyframes.Count; k++)
                {
                    if
                    (
                        (isNodeMotion && ((smType & SubMotionType.NND_SMOTTYPE_TRANSLATION_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_SCALING_MASK) != 0)) ||
                        (isMaterialMotion && ((smType & SubMotionType.NND_SMOTTYPE_DIFFUSE_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_SPECULAR_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_AMBIENT_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_OFFSET_MASK) != 0)) ||
                        (smType & SubMotionType.NND_SMOTTYPE_LIGHT_COLOR_MASK) != 0
                    )
                    {
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_VECTOR).Frame);
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_VECTOR).Value);
                    }
                    else if (isNodeMotion && (smType & SubMotionType.NND_SMOTTYPE_ROTATION_XYZ) != 0)
                    {
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16).Frame);
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16).Value1);
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16).Value2);
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16).Value3);
                    }
                    else if ((smType & SubMotionType.NND_SMOTTYPE_FRAME_FLOAT) != 0)
                    {
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_FLOAT).Frame);
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_FLOAT).Value);
                    }
                    else if ((smType & SubMotionType.NND_SMOTTYPE_FRAME_SINT16) != 0)
                    {
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_SINT16).Frame);
                        writer.Write((SubMotions[i].Keyframes[k] as NinjaKeyframe.NNS_MOTION_KEY_SINT16).Value);
                    }
                    else
                    {
                        // If none of those flags are found, error out.
                        throw new NotImplementedException();
                    }
                }
            }

            /* Write sub motions. */
            MotionOffsets.Add($"SubMotionTable", (uint)writer.BaseStream.Position);
            for (int i = 0; i < SubMotions.Count; i++)
            {
                var smType = SubMotions[i].Type;
                writer.Write((uint)smType);
                writer.Write((uint)SubMotions[i].InterpolationType);
                writer.Write(SubMotions[i].NodeIndex);
                writer.Write(SubMotions[i].StartFrame);
                writer.Write(SubMotions[i].EndFrame);
                writer.Write(SubMotions[i].StartKeyframe);
                writer.Write(SubMotions[i].EndKeyframe);
                writer.Write(SubMotions[i].Keyframes.Count);

                if
                (
                    (isNodeMotion && ((smType & SubMotionType.NND_SMOTTYPE_TRANSLATION_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_SCALING_MASK) != 0)) ||
                    (isMaterialMotion && ((smType & SubMotionType.NND_SMOTTYPE_DIFFUSE_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_SPECULAR_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_AMBIENT_MASK) != 0 || (smType & SubMotionType.NND_SMOTTYPE_OFFSET_MASK) != 0)) ||
                    (smType & SubMotionType.NND_SMOTTYPE_LIGHT_COLOR_MASK) != 0
                )
                {
                    writer.Write(16);
                }
                else if (isNodeMotion && (smType & SubMotionType.NND_SMOTTYPE_ROTATION_XYZ) != 0)
                {
                    writer.Write(8);
                }
                else if ((smType & SubMotionType.NND_SMOTTYPE_FRAME_FLOAT) != 0)
                {
                    writer.Write(8);
                }
                else if ((smType & SubMotionType.NND_SMOTTYPE_FRAME_SINT16) != 0)
                {
                    writer.Write(4);
                }
                else
                {
                    throw new NotImplementedException();
                }

                writer.AddOffset($"SubMotion{i}KeyframesOffset", 0);
                writer.Write(MotionOffsets[$"SubMotion{i}KeyframesOffset"] - writer.Offset);
            }

            // Write chunk data.
            writer.FillOffset("dataOffset", true);
            writer.Write((uint)Type);
            writer.Write(StartFrame);
            writer.Write(EndFrame);
            writer.Write(SubMotions.Count);
            writer.AddOffset($"SubMotionTable", 0);
            writer.Write(MotionOffsets[$"SubMotionTable"] - writer.Offset);
            writer.Write(Framerate);
            writer.Write(Reserved0);
            writer.Write(Reserved1);

            // Alignment.
            writer.FixPadding(0x10);

            // Write chunk size.
            long ChunkEndPosition = writer.BaseStream.Position;
            uint ChunkSize = (uint)(ChunkEndPosition - HeaderSizePosition);
            writer.BaseStream.Position = HeaderSizePosition - 4;
            writer.Write(ChunkSize);
            writer.BaseStream.Position = ChunkEndPosition;
        }
    }
}