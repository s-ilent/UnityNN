// File: Marathon/Ninja/NinjaSubMotion.cs
using System.Collections.Generic;
using Marathon.IO;
using System;

namespace Marathon.Formats.Mesh.Ninja
{
    /// <summary>
    /// Structure of a Ninja Sub Motion entry.
    /// </summary>
    public class NinjaSubMotion
    {
        public SubMotionType Type { get; set; }

        public SubMotionInterpolationType InterpolationType { get; set; }

        public int NodeIndex { get; set; }

        public float StartFrame { get; set; }

        public float EndFrame { get; set; }

        public float StartKeyframe { get; set; }

        public float EndKeyframe { get; set; }

        public List<object> Keyframes { get; set; } = new List<object>();

        /// <summary>
        /// Reads a Ninja Sub Motion entry from a file.
        /// </summary>
        /// <param name="reader">The binary reader for this SegaNN file.</param>
        /// <param name="parentMotionType">The motion category of the parent motion chunk.</param>
        public void Read(BinaryReaderEx reader, MotionType parentMotionType = MotionType.NND_MOTIONTYPE_NODE)
        {
            // Read the main data for this Sub Motion.
            Type = (SubMotionType)reader.ReadUInt32();
            InterpolationType = (SubMotionInterpolationType)reader.ReadUInt32();
            NodeIndex = reader.ReadInt32();
            StartFrame = reader.ReadSingle();
            EndFrame = reader.ReadSingle();
            StartKeyframe = reader.ReadSingle();
            EndKeyframe = reader.ReadSingle();
            uint KeyFrameCount = reader.ReadUInt32();
            uint KeyFrameSize = reader.ReadUInt32();
            uint KeyFrameOffset = reader.ReadUInt32();

            // Save our current position so we can jump back afterwards.
            long pos = reader.BaseStream.Position;

            // Jump to the list of Keyframes for this sub motion.
            reader.JumpTo(KeyFrameOffset, true);

            bool is3AxisRotation = (Type & SubMotionType.NND_SMOTTYPE_ROTATION_XYZ) == SubMotionType.NND_SMOTTYPE_ROTATION_XYZ;

            // Loop through and read keyframes according to KeyFrameSize and submotion type.
            for (int i = 0; i < KeyFrameCount; i++)
            {
                long kfStart = reader.BaseStream.Position;

                if (KeyFrameSize == 16)
                {
                    NinjaKeyframe.NNS_MOTION_KEY_VECTOR Keyframe = new NinjaKeyframe.NNS_MOTION_KEY_VECTOR();
                    Keyframe.Read(reader);
                    Keyframes.Add(Keyframe);
                }
                else if (KeyFrameSize == 8)
                {
                    if (is3AxisRotation)
                    {
                        NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16 Keyframe = new NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16();
                        Keyframe.Read(reader);
                        Keyframes.Add(Keyframe);
                    }
                    else if ((Type & SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE32) != 0)
                    {
                        NinjaKeyframe.NNS_MOTION_KEY_SINT32 Keyframe = new NinjaKeyframe.NNS_MOTION_KEY_SINT32();
                        Keyframe.Read(reader);
                        Keyframes.Add(Keyframe);
                    }
                    else
                    {
                        NinjaKeyframe.NNS_MOTION_KEY_FLOAT Keyframe = new NinjaKeyframe.NNS_MOTION_KEY_FLOAT();
                        Keyframe.Read(reader);
                        Keyframes.Add(Keyframe);
                    }
                }
                else if (KeyFrameSize == 4)
                {
                    NinjaKeyframe.NNS_MOTION_KEY_SINT16 Keyframe = new NinjaKeyframe.NNS_MOTION_KEY_SINT16();
                    Keyframe.Read(reader);
                    Keyframes.Add(Keyframe);
                }
                else if (KeyFrameSize > 0)
                {
                    reader.JumpAhead(KeyFrameSize);
                }

                // Stride safeguard per keyframe
                if (KeyFrameSize > 0)
                    reader.JumpTo(kfStart + KeyFrameSize);
            }

            // Jump back to where we were.
            reader.JumpTo(pos);
        }
    }
}