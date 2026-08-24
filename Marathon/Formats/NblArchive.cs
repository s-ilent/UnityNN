// File: Marathon/Formats/NblArchive.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Marathon.IO;
using Marathon.Formats.Mesh.Ninja;

namespace Marathon.Formats.Archive
{
    public class NblArchive
    {
        public struct FileEntryHeader
        {
            public string Identifier;
            public uint ChunkSize;
            public uint Unknown1;
            public uint Unknown2;
            public string FileName;
            public uint FilePosition;
            public uint FileSize;
            public uint PointerPosition;
            public uint PointerSize;
            public byte[] SubHeader;
        }

        public class NblFileEntry
        {
            public FileEntryHeader Header;
            public byte[] RawData;
            public string ChunkID = "NMLL";
            public List<int> Pointers = new List<int>();

            public override string ToString() => Header.FileName ?? $"[{ChunkID}] {Header.Identifier}";
        }

        public class NblChunkInfo
        {
            public string ChunkID = "NMLL";
            public ushort FileVersion;
            public ushort ChunkFilenameLength;
            public int HeaderSize;
            public int NumFiles;
            public uint UncompressedSize;
            public uint CompressedSize;
            public uint PointerCount;
            public uint DecryptKey;
            public bool IsEncrypted;
            public bool IsCompressed;
            public uint DataOffset;
            public uint PointerOffset;
            public List<NblFileEntry> Entries = new List<NblFileEntry>();
        }

        public List<NblChunkInfo> Chunks { get; set; } = new List<NblChunkInfo>();
        public List<NblFileEntry> Entries { get; set; } = new List<NblFileEntry>();
        public List<string> DiagnosticLogs { get; set; } = new List<string>();

        private uint m_TmllHeaderLoc = 0;
        private BlewFish m_Decryptor = null;
        private uint m_DecryptKey = 0;
        private bool m_IsBigEndian = false;

        public static NblArchive Load(Stream stream)
        {
            NblArchive archive = new NblArchive();
            archive.Parse(stream);
            return archive;
        }

        public static NblArchive Load(byte[] fileBytes)
        {
            using (MemoryStream ms = new MemoryStream(fileBytes))
            {
                return Load(ms);
            }
        }

        private void Parse(Stream stream)
        {
            if (stream == null || stream.Length < 16) return;

            stream.Seek(0x3, SeekOrigin.Begin);
            int endian = stream.ReadByte();
            m_IsBigEndian = (endian == 0x42 || endian == (byte)'B');
            stream.Seek(0, SeekOrigin.Begin);

            BinaryReaderEx reader = new BinaryReaderEx(stream, m_IsBigEndian);

            // 1. Primary NMLL Chunk
            NblChunkInfo firstChunk = LoadGroup(stream, reader);
            if (firstChunk != null)
            {
                Chunks.Add(firstChunk);
                Entries.AddRange(firstChunk.Entries);
            }

            // 2. Linked TMLL Chunk (inherits decryptKey and decryptor from NMLL)
            if (m_TmllHeaderLoc != 0 && m_TmllHeaderLoc < stream.Length)
            {
                stream.Seek(m_TmllHeaderLoc, SeekOrigin.Begin);
                NblChunkInfo tmllChunk = LoadGroup(stream, reader);
                if (tmllChunk != null)
                {
                    Chunks.Add(tmllChunk);
                    Entries.AddRange(tmllChunk.Entries);
                }
            }
        }

        private NblChunkInfo LoadGroup(Stream stream, BinaryReaderEx reader)
        {
            long offset = stream.Position;
            if (offset >= stream.Length) return null;

            string formatName = new string(reader.ReadChars(4));
            ushort fileVersion = reader.ReadUInt16();

            int paddingAmount = fileVersion == 0x1002 ? 0x3F : 0x7FF;
            uint mask = fileVersion == 0x1002 ? 0xFFFFFFC0 : 0xFFFFF800;

            ushort chunkFilenameLength = reader.ReadUInt16();
            int headerSize = reader.ReadInt32();
            int numFiles = reader.ReadInt32();
            uint uncompressedSize = reader.ReadUInt32();
            uint compressedSize = reader.ReadUInt32();
            uint pointerLength = reader.ReadUInt32() / 4;

            if (formatName.StartsWith("NML", StringComparison.OrdinalIgnoreCase))
            {
                m_DecryptKey = reader.ReadUInt32();
            }
            else
            {
                reader.ReadUInt32();
            }

            uint size = compressedSize == 0 ? uncompressedSize : compressedSize;
            uint nmllDataLoc = (uint)((headerSize + paddingAmount) & mask);
            uint pointerLoc = (uint)(nmllDataLoc + size + paddingAmount) & mask;
            uint mainHeaderSize = 0x20;

            if (formatName.StartsWith("NML", StringComparison.OrdinalIgnoreCase))
            {
                mainHeaderSize = 0x30;
                reader.JumpTo(offset + 0x20);
                uint tmllHeaderSize = reader.ReadUInt32();
                uint tmllDataSizeUncomp = reader.ReadUInt32();
                uint tmllDataSizeComp = reader.ReadUInt32();
                uint tmllCount = reader.ReadUInt32();
                if (tmllCount > 0)
                {
                    m_TmllHeaderLoc = (uint)(pointerLoc + pointerLength * 4 + paddingAmount) & mask;
                }
            }

            if (m_DecryptKey != 0)
            {
                m_Decryptor = new BlewFish(m_DecryptKey, m_IsBigEndian);
            }

            NblChunkInfo chunkInfo = new NblChunkInfo
            {
                ChunkID = formatName,
                FileVersion = fileVersion,
                ChunkFilenameLength = chunkFilenameLength,
                HeaderSize = headerSize,
                NumFiles = numFiles,
                UncompressedSize = uncompressedSize,
                CompressedSize = compressedSize,
                PointerCount = pointerLength,
                DecryptKey = m_DecryptKey,
                IsEncrypted = m_DecryptKey != 0,
                IsCompressed = compressedSize != 0,
                DataOffset = (uint)(offset + nmllDataLoc),
                PointerOffset = (uint)(offset + pointerLoc)
            };

            FileEntryHeader[] groupHeaders = new FileEntryHeader[numFiles];
            for (int i = 0; i < numFiles; i++)
            {
                stream.Seek(mainHeaderSize + 0x60 * i + offset, SeekOrigin.Begin);
                groupHeaders[i] = ReadFileEntryHeader(reader.ReadBytes(0x60));
            }

            if (nmllDataLoc + offset >= stream.Length) return chunkInfo;

            stream.Seek(nmllDataLoc + offset, SeekOrigin.Begin);

            int encryptedSectionSize;
            if (fileVersion == 0x1002)
            {
                int rawEncryptedSectionSize = (int)((((compressedSize >> 0xB) ^ compressedSize) & 0xE0) + 0x20);
                encryptedSectionSize = Math.Min(rawEncryptedSectionSize, (int)compressedSize);
            }
            else
            {
                encryptedSectionSize = (int)size;
            }

            if (encryptedSectionSize % 8 != 0)
            {
                encryptedSectionSize -= (encryptedSectionSize % 8);
            }

            byte[] decryptedFiles;
            if (m_DecryptKey != 0 && formatName.StartsWith("NML", StringComparison.OrdinalIgnoreCase))
            {
                byte[] encryptedFiles = reader.ReadBytes((int)size);
                decryptedFiles = m_Decryptor.decryptBlock(encryptedFiles, encryptedSectionSize);
            }
            else
            {
                int readLength = Math.Min((int)size + 7, (int)(stream.Length - (nmllDataLoc + offset)));
                decryptedFiles = reader.ReadBytes(readLength);
            }

            byte[] decompressedFiles;
            if (compressedSize != 0)
            {
                if (fileVersion == 0x1002)
                {
                    try
                    {
                        using (MemoryStream compStream = new MemoryStream(decryptedFiles))
                        using (DeflateStream ds = new DeflateStream(compStream, CompressionMode.Decompress))
                        using (MemoryStream decompStream = new MemoryStream((int)uncompressedSize))
                        {
                            ds.CopyTo(decompStream);
                            decompressedFiles = decompStream.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLogs.Add($"[Warning] Deflate decompression fallback to PRS for '{formatName}': {ex.Message}");
                        decompressedFiles = PrsDecompressor.Decompress(decryptedFiles, uncompressedSize);
                    }
                }
                else
                {
                    decompressedFiles = PrsDecompressor.Decompress(decryptedFiles, uncompressedSize);
                }
            }
            else
            {
                decompressedFiles = decryptedFiles;
            }

            List<int> pointers = new List<int>((int)pointerLength);
            if (pointerLength > 0 && pointerLoc + offset < stream.Length)
            {
                stream.Seek(pointerLoc + offset, SeekOrigin.Begin);
                for (int i = 0; i < pointerLength; i++)
                {
                    pointers.Add(reader.ReadInt32());
                }
            }

            for (int i = 0; i < numFiles; i++)
            {
                FileEntryHeader fh = groupHeaders[i];
                if (fh.FilePosition + fh.FileSize > decompressedFiles.Length)
                {
                    DiagnosticLogs.Add($"[Warning] Entry '{fh.FileName}' offset 0x{fh.FilePosition:X8} + size {fh.FileSize} exceeds decompressed payload size ({decompressedFiles.Length} bytes).");
                    continue;
                }

                byte[] subBytes = new byte[fh.FileSize];
                Array.Copy(decompressedFiles, fh.FilePosition, subBytes, 0, fh.FileSize);

                List<int> entryPointers = new List<int>();

                if (fh.PointerSize > 0 && pointers.Count > 0)
                {
                    int startIdx = (int)fh.PointerPosition / 4;
                    int ptrCount = (int)fh.PointerSize / 4;

                    if (startIdx >= 0 && startIdx + ptrCount <= pointers.Count)
                    {
                        entryPointers = pointers.GetRange(startIdx, ptrCount);

                        for (int j = 0; j < ptrCount; j++)
                        {
                            int pLoc = pointers[startIdx + j];
                            int localPLoc = pLoc - (int)fh.FilePosition;

                            if (localPLoc >= 0 && localPLoc <= subBytes.Length - 4)
                            {
                                uint v;
                                if (m_IsBigEndian)
                                {
                                    v = (uint)(subBytes[localPLoc] << 24 | subBytes[localPLoc + 1] << 16 | subBytes[localPLoc + 2] << 8 | subBytes[localPLoc + 3]);
                                }
                                else
                                {
                                    v = (uint)(subBytes[localPLoc] | subBytes[localPLoc + 1] << 8 | subBytes[localPLoc + 2] << 16 | subBytes[localPLoc + 3] << 24);
                                }

                                uint rebasedV = v - fh.FilePosition;

                                if (m_IsBigEndian)
                                {
                                    subBytes[localPLoc] = (byte)(rebasedV >> 24);
                                    subBytes[localPLoc + 1] = (byte)(rebasedV >> 16);
                                    subBytes[localPLoc + 2] = (byte)(rebasedV >> 8);
                                    subBytes[localPLoc + 3] = (byte)rebasedV;
                                }
                                else
                                {
                                    subBytes[localPLoc] = (byte)rebasedV;
                                    subBytes[localPLoc + 1] = (byte)(rebasedV >> 8);
                                    subBytes[localPLoc + 2] = (byte)(rebasedV >> 16);
                                    subBytes[localPLoc + 3] = (byte)(rebasedV >> 24);
                                }
                            }
                        }
                    }
                }

                NblFileEntry entry = new NblFileEntry
                {
                    Header = fh,
                    RawData = subBytes,
                    ChunkID = formatName,
                    Pointers = entryPointers
                };

                chunkInfo.Entries.Add(entry);
            }

            return chunkInfo;
        }

        private FileEntryHeader ReadFileEntryHeader(byte[] headerData)
        {
            FileEntryHeader toReturn = new FileEntryHeader();
            using (MemoryStream headerStream = new MemoryStream(headerData))
            {
                BinaryReaderEx headerReader = new BinaryReaderEx(headerStream, m_IsBigEndian);
                toReturn.Identifier = new string(headerReader.ReadChars(4));
                toReturn.ChunkSize = headerReader.ReadUInt32();
                toReturn.Unknown1 = headerReader.ReadUInt32();
                toReturn.Unknown2 = headerReader.ReadUInt32();

                byte[] rawEncryptedHeader = headerReader.ReadBytes(0x30);
                byte[] decryptHeader = (m_DecryptKey != 0 && m_Decryptor != null)
                    ? m_Decryptor.decryptBlock(rawEncryptedHeader)
                    : rawEncryptedHeader;

                using (MemoryStream alpha = new MemoryStream(decryptHeader))
                {
                    BinaryReaderEx beta = new BinaryReaderEx(alpha, m_IsBigEndian);
                    byte[] rawFilename = beta.ReadBytes(0x20);

                    try
                    {
                        toReturn.FileName = Encoding.GetEncoding("shift-jis").GetString(rawFilename).TrimEnd('\0');
                    }
                    catch
                    {
                        toReturn.FileName = Encoding.ASCII.GetString(rawFilename).TrimEnd('\0');
                    }

                    toReturn.FilePosition = beta.ReadUInt32();
                    toReturn.FileSize = beta.ReadUInt32();
                    toReturn.PointerPosition = beta.ReadUInt32();
                    toReturn.PointerSize = beta.ReadUInt32();
                }

                toReturn.SubHeader = headerReader.ReadBytes(0x20);
            }

            return toReturn;
        }

        /// <summary>
        /// Slices sub-files in the NBL archive into a NinjaNext.FormatData container
        /// for inspector preview and material survey tools.
        /// </summary>
        public NinjaNext.FormatData ToFormatData()
        {
            NinjaNext.FormatData data = new NinjaNext.FormatData();

            foreach (var entry in Entries)
            {
                if (entry.RawData == null || entry.RawData.Length < 16) continue;

                using (MemoryStream ms = new MemoryStream(entry.RawData))
                {
                    NinjaNext entryLoader = new NinjaNext();
                    try
                    {
                        entryLoader.Load(ms);

                        if (entryLoader.Data.Object != null && data.Object == null)
                            data.Object = entryLoader.Data.Object;
                        if (entryLoader.Data.TextureList != null && data.TextureList == null)
                            data.TextureList = entryLoader.Data.TextureList;
                        if (entryLoader.Data.NodeNameList != null && data.NodeNameList == null)
                            data.NodeNameList = entryLoader.Data.NodeNameList;
                        if (entryLoader.Data.Motion != null && data.Motion == null)
                            data.Motion = entryLoader.Data.Motion;
                        if (entryLoader.Data.MaterialMotion != null && data.MaterialMotion == null)
                            data.MaterialMotion = entryLoader.Data.MaterialMotion;
                        if (entryLoader.Data.Camera != null && data.Camera == null)
                            data.Camera = entryLoader.Data.Camera;
                        if (entryLoader.Data.Light != null && data.Light == null)
                            data.Light = entryLoader.Data.Light;
                        if (entryLoader.Data.EffectList != null && data.EffectList == null)
                            data.EffectList = entryLoader.Data.EffectList;
                    }
                    catch
                    {
                        // Fallback by entry ID/filename
                        ms.Position = 0;
                        BinaryReaderEx reader = new BinaryReaderEx(ms);
                        string id = entry.Header.Identifier;
                        string fn = entry.Header.FileName ?? "";

                        if (data.Object == null && (id == "NXOB" || id.StartsWith("NGOB") || id.StartsWith("NZOB") || fn.EndsWith(".xno", StringComparison.OrdinalIgnoreCase) || fn.EndsWith(".xnj", StringComparison.OrdinalIgnoreCase)))
                        {
                            data.Object = new NinjaObject();
                            data.Object.Read(reader);
                        }
                        else if (data.TextureList == null && (id == "NXTL" || id.StartsWith("NGTL") || id.StartsWith("NZTL") || fn.EndsWith(".xnt", StringComparison.OrdinalIgnoreCase)))
                        {
                            data.TextureList = new NinjaTextureList();
                            data.TextureList.Read(reader);
                        }
                        else if (data.NodeNameList == null && (id == "NXNN" || id.StartsWith("NGNN") || id.StartsWith("NZNN") || fn.EndsWith(".xnn", StringComparison.OrdinalIgnoreCase)))
                        {
                            data.NodeNameList = new NinjaNodeNameList();
                            data.NodeNameList.Read(reader);
                        }
                        else if ((id == "NXMA" || id == "NXMO" || id == "NXMV" || fn.EndsWith(".xnm", StringComparison.OrdinalIgnoreCase) || fn.EndsWith(".xnv", StringComparison.OrdinalIgnoreCase)))
                        {
                            NinjaMotion motion = new NinjaMotion { ChunkID = id };
                            motion.Read(reader);
                            if (motion.Type.HasFlag(MotionType.NND_MOTIONTYPE_MATERIAL) || fn.EndsWith(".xnv", StringComparison.OrdinalIgnoreCase))
                            {
                                if (data.MaterialMotion == null) data.MaterialMotion = motion;
                            }
                            else
                            {
                                if (data.Motion == null) data.Motion = motion;
                            }
                        }
                    }
                }
            }

            if (data.Object != null && data.NodeNameList != null)
            {
                if (data.Object.Nodes.Count == data.NodeNameList.NinjaNodeNames.Count)
                {
                    for (int i = 0; i < data.Object.Nodes.Count; i++)
                        data.Object.Nodes[i].Name = data.NodeNameList.NinjaNodeNames[i];
                }
            }

            return data;
        }
    }
}