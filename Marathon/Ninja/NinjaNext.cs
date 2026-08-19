// File: Marathon/Ninja/NinjaNext.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Marathon.IO;

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

        private static readonly Dictionary<string, Action<BinaryReaderEx, string, FormatData>> ChunkDispatchTable = new(StringComparer.OrdinalIgnoreCase)
        {
            ["TL"] = (r, id, d) => { d.TextureList = new NinjaTextureList(); d.TextureList.Read(r); },
            ["EF"] = (r, id, d) => { d.EffectList = new NinjaEffectList(); d.EffectList.Read(r); },
            ["NN"] = (r, id, d) => { d.NodeNameList = new NinjaNodeNameList(); d.NodeNameList.Read(r); },
            ["TN"] = (r, id, d) => { d.NodeNameList = new NinjaNodeNameList(); d.NodeNameList.Read(r); }, // Morph Target Names
            ["OB"] = (r, id, d) => { d.Object = new NinjaObject(); d.Object.Read(r); },
            ["LI"] = (r, id, d) => { d.Light = new NinjaLight(); d.Light.Read(r); },
            ["CA"] = (r, id, d) => { d.Camera = new NinjaCamera(); d.Camera.Read(r); },
            ["MA"] = ReadMotionChunk,
            ["MO"] = ReadMotionChunk,
            ["MC"] = ReadMotionChunk,
            ["ML"] = ReadMotionChunk,
            ["MM"] = ReadMotionChunk,
            ["MV"] = ReadMotionChunk,
            ["NV"] = ReadMotionChunk
        };

        private static void ReadMotionChunk(BinaryReaderEx reader, string chunkID, FormatData data)
        {
            NinjaMotion motion = new NinjaMotion { ChunkID = chunkID };
            motion.Read(reader);
            if (motion.Type.HasFlag(MotionType.NND_MOTIONTYPE_MATERIAL) || chunkID.EndsWith("NV") || chunkID.EndsWith("MV") || chunkID.EndsWith("MA"))
            {
                data.MaterialMotion = motion;
            }
            else
            {
                data.Motion = motion;
            }
        }

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

            // Case A: Standalone Bare Chunk (No NXIF container header)
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

                ReadChunkPayload(reader, chunkID);
                return;
            }

            // Case B: Multi-Chunk NXIF Container Header
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

            reader.Offset = (uint)(headerStartPos + dataOffset);

            reader.JumpTo(headerStartPos + dataOffset);
            for (int i = 0; i < dataChunkCount; i++)
            {
                if (reader.BaseStream.Position + 8 > reader.BaseStream.Length) break;

                long chunkPos = reader.BaseStream.Position;
                string chunkID = new string(reader.ReadChars(4));
                uint chunkSize = reader.ReadUInt32();

                long targetPosition = chunkPos + 8 + chunkSize;

                ReadChunkPayload(reader, chunkID);

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
            if (string.IsNullOrEmpty(chunkID) || chunkID.Length < 4) return;

            if (chunkID[0] == 'N')
            {
                string tag = chunkID.Substring(2, 2).ToUpperInvariant();
                if (ChunkDispatchTable.TryGetValue(tag, out var handler))
                {
                    handler(reader, chunkID, Data);
                }
            }
        }
    }
}