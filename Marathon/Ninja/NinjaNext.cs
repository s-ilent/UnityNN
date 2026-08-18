// File: Marathon/Ninja/NinjaNext.cs
using UnityEngine;
using System.Collections.Generic;
using Marathon.IO;
using System.IO;

namespace Marathon.Formats.Mesh.Ninja
{
    public class NinjaNext : FileBase
    {
        public NinjaNext() { }

        public NinjaNext(string file, bool serialise = false)
        {
            Load(file);
        }

        public override string Signature { get; } = "NXIF";

        public class FormatData
        {
            public NinjaTextureList TextureList { get; set; }
            public NinjaEffectList EffectList { get; set; }
            public NinjaNodeNameList NodeNameList { get; set; }
            public NinjaObject Object { get; set; }
            public NinjaLight Light { get; set; }
            public NinjaCamera Camera { get; set; }
            public NinjaMotion Motion { get; set; }
            public NinjaMotion MaterialMotion { get; set; }
        }

        public FormatData Data { get; set; } = new FormatData();

        public override void Load(Stream stream)
        {
            BinaryReaderEx reader = new BinaryReaderEx(stream);
            if (stream.Length < 16) return;

            long headerStartPos = 0;
            string headerSig = new string(reader.ReadChars(4));

            // Check if file starts with 0x40-byte outer wrapper header (XNJ, XNM, XNA, XNO, XNC, etc.)
            if (headerSig != "NXIF" && !headerSig.EndsWith("IF"))
            {
                if (stream.Length >= 0x60)
                {
                    reader.JumpTo(0x40);
                    string innerSig = new string(reader.ReadChars(4));

                    if (innerSig == "NXIF" || innerSig.EndsWith("IF"))
                    {
                        headerStartPos = 0x40;
                        headerSig = innerSig;
                    }
                }
            }

            // -----------------------------------------------------------------
            // Case A: Standalone Bare Chunk (No NXIF container header)
            // -----------------------------------------------------------------
            if (headerSig != "NXIF" && !headerSig.EndsWith("IF"))
            {
                reader.JumpTo(0);
                reader.Offset = 0;

                string chunkID = new string(reader.ReadChars(4));
                uint chunkSize = reader.ReadUInt32();

                if ((chunkSize & 0xFF000000) != 0)
                {
                    reader.IsBigEndian = true;
                    reader.JumpTo(4);
                    chunkSize = reader.ReadUInt32();
                }

                // Stream position is at byte 8 of the chunk, ready for chunk parsing
                ReadChunkPayload(reader, chunkID);
                return;
            }

            // -----------------------------------------------------------------
            // Case B: Multi-Chunk NXIF Container Header
            // -----------------------------------------------------------------
            reader.JumpTo(headerStartPos + 4);
            uint chunkSizeNXIF = reader.ReadUInt32();
            uint dataChunkCount = reader.ReadUInt32();

            // Auto-detect Big Endian vs Little Endian
            if ((chunkSizeNXIF & 0xFF000000) != 0 || dataChunkCount > 0xFFFF)
            {
                reader.IsBigEndian = true;
                reader.JumpTo(headerStartPos + 4);
                chunkSizeNXIF = reader.ReadUInt32();
                dataChunkCount = reader.ReadUInt32();
            }

            uint dataOffset = reader.ReadUInt32();
            uint dataSize = reader.ReadUInt32();
            uint NOF0Offset = reader.ReadUInt32();
            uint NOF0Size = reader.ReadUInt32();
            uint version = reader.ReadUInt32();

            // Set reader offset relative to inner container header start position
            reader.Offset = (uint)(headerStartPos + dataOffset);

            // Read data chunks sequentially
            reader.JumpTo(headerStartPos + dataOffset);
            for (int i = 0; i < dataChunkCount; i++)
            {
                if (reader.BaseStream.Position + 8 > reader.BaseStream.Length) break;

                long chunkPos = reader.BaseStream.Position;
                string chunkID = new string(reader.ReadChars(4));
                uint chunkSize = reader.ReadUInt32();

                long targetPosition = chunkPos + 8 + chunkSize;

                // Stream position is at byte 8 of this chunk, ready for chunk parsing
                ReadChunkPayload(reader, chunkID);

                // Jump to the start of the next chunk
                if (targetPosition <= reader.BaseStream.Length)
                {
                    reader.JumpTo(targetPosition);
                }
            }

            // Assign node/bone names if both Object and NodeNameList exist
            if (Data.Object != null && Data.NodeNameList != null)
            {
                if (Data.Object.Nodes.Count == Data.NodeNameList.NinjaNodeNames.Count)
                {
                    for (int i = 0; i < Data.Object.Nodes.Count; i++)
                        Data.Object.Nodes[i].Name = Data.NodeNameList.NinjaNodeNames[i];
                }
            }
        }

        private void ReadChunkPayload(BinaryReaderEx reader, string chunkID)
        {
            switch (chunkID)
            {
                // Texture Lists
                case "NXTL": case "NGTL": case "NZTL": case "NCTL": case "NETL": case "NITL": case "NLTL": case "NSTL": case "NUTL":
                    Data.TextureList = new NinjaTextureList();
                    Data.TextureList.Read(reader);
                    break;

                // Effect Lists
                case "NXEF": case "NGEF": case "NZEF": case "NCEF": case "NEEF": case "NIEF": case "NLEF": case "NSEF": case "NUEF":
                    Data.EffectList = new NinjaEffectList();
                    Data.EffectList.Read(reader);
                    break;

                // Node Name Lists
                case "NXNN": case "NGNN": case "NZNN": case "NCNN": case "NENN": case "NINN": case "NLNN": case "NSNN": case "NUNN":
                    Data.NodeNameList = new NinjaNodeNameList();
                    Data.NodeNameList.Read(reader);
                    break;

                // Objects / Meshes
                case "NXOB": case "NGOB": case "NZOB": case "NCOB": case "NEOB": case "NIOB": case "NLOB": case "NSOB": case "NUOB":
                    Data.Object = new NinjaObject();
                    Data.Object.Read(reader);
                    break;

                // Lights
                case "NXLI": case "NGLI": case "NZLI": case "NCLI": case "NELI":
                    Data.Light = new NinjaLight();
                    Data.Light.Read(reader);
                    break;

                // Cameras
                case "NXCA": case "NGCA": case "NZCA": case "NCCA": case "NECA":
                    Data.Camera = new NinjaCamera();
                    Data.Camera.Read(reader);
                    break;

                // Motions / Animations
                case "NXMA": case "NXMC": case "NXML": case "NXMM": case "NXMO":
                case "NGMA": case "NGMC": case "NGML": case "NGMM": case "NGMO":
                case "NZMA": case "NZMC": case "NZML": case "NZMM": case "NZMO":
                case "NXNV": case "NXMV": case "NGNV": case "NZNV":
                    NinjaMotion motion = new NinjaMotion();
                    motion.ChunkID = chunkID;
                    motion.Read(reader);
                    if (motion.Type.HasFlag(MotionType.NND_MOTIONTYPE_MATERIAL) || chunkID.EndsWith("NV") || chunkID.EndsWith("MV"))
                    {
                        Data.MaterialMotion = motion;
                    }
                    else
                    {
                        Data.Motion = motion;
                    }
                    break;

                // Metadata & Unknown Chunks
                case "NOF0":
                case "NFN0":
                case "NEND":
                default:
                    break;
            }
        }
    }
}