using UnityEngine;
using System.Collections.Generic;
using Marathon.IO;
using System.IO;
using System;

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
        }

        public FormatData Data { get; set; } = new FormatData();

        public override void Load(Stream stream)
        {
            BinaryReaderEx reader = new BinaryReaderEx(stream);

            long headerStartPos = 0;
            string headerSig = new string(reader.ReadChars(4));

            // Check if file starts with 0x40-byte outer wrapper header (XNJ, XNM, XNA, XNO, XNC, etc.)
            if (headerSig != "NXIF" && !headerSig.EndsWith("IF"))
            {
                if (stream.Length >= 0x60)
                {
                    reader.JumpTo(0x40);
                    headerStartPos = 0x40;
                    string innerSig = new string(reader.ReadChars(4));

                    if (innerSig != "NXIF" && !innerSig.EndsWith("IF"))
                    {
                        // Fallback reset to offset 0 if 0x40 is not a valid N*IF container header
                        reader.JumpTo(0);
                        headerStartPos = 0;
                        reader.ReadChars(4);
                    }
                }
            }

            uint chunkSize = reader.ReadUInt32();
            uint dataChunkCount = reader.ReadUInt32();
            uint dataOffset = reader.ReadUInt32();
            uint dataSize = reader.ReadUInt32();
            uint NOF0Offset = reader.ReadUInt32();
            uint NOF0Size = reader.ReadUInt32();
            uint version = reader.ReadUInt32();

            // Set reader offset relative to inner container header start position
            reader.Offset = (uint)(headerStartPos + dataOffset);

            // Read data chunks
            for (int i = 0; i < dataChunkCount; i++)
            {
                // Read the chunk's ID and size.
                string chunkID = new string(reader.ReadChars(4));
                chunkSize = reader.ReadUInt32();

                // Calculate where the next chunk begins so we can jump to it.
                long targetPosition = reader.BaseStream.Position + chunkSize;

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
                        Data.Motion = new NinjaMotion();
                        Data.Motion.ChunkID = chunkID;
                        Data.Motion.Read(reader);
                        break;

                    // Metadata & Unknown Chunks
                    case "NOF0":
                    case "NFN0":
                    case "NEND":
                    default:
                        break;
                }

                // Jump to the position of the next chunk to make sure the reader's in the right place.
                reader.JumpTo(targetPosition);
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
    }
}