using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Marathon.IO;

namespace Marathon.Formats.Particle
{
    /// <summary>
    /// Native loader for Sega YPD0 Particle & Effect definition streams (.dat).
    /// </summary>
    public class ParticleEffectFile : FileBase
    {
        public override string Signature { get; } = "YPD0";
        public override string Extension { get; } = ".dat";

        public int ParticleType { get; set; }
        public int Sentinel { get; set; } = 0x12C;

        public bool IsValid => Emitters.Count > 0 || Behaviors.Count > 0 || ResourceFiles.Count > 0 || SequenceCues.Count > 0;

        public List<string> ExternalBones { get; set; } = new List<string>();
        public List<string> ResourceFiles { get; set; } = new List<string>();
        public List<ParticleEmitter> Emitters { get; set; } = new List<ParticleEmitter>();
        public List<ParticleBehaviorBlock> Behaviors { get; set; } = new List<ParticleBehaviorBlock>();
        public List<ParticleSequenceCue> SequenceCues { get; set; } = new List<ParticleSequenceCue>();

        public override void Load(Stream stream)
        {
            if (stream == null || stream.Length < 16) return;

            BinaryReaderEx reader = new BinaryReaderEx(stream);
            uint payloadStart = 0;
            long payloadEnd = stream.Length;

            // 1. Resolve container wrapper (STD\0)
            if (stream.Length >= 0x64)
            {
                reader.JumpTo(0);
                string containerSig = new string(reader.ReadChars(3));
                if (containerSig == "STD")
                {
                    reader.JumpTo(0x04);
                    uint chunkSize = reader.ReadUInt32();
                    payloadStart = (chunkSize > 0 && chunkSize < stream.Length) ? chunkSize : 0x60;

                    reader.JumpTo(0x34);
                    uint payloadSize = reader.ReadUInt32();
                    if (payloadSize > 0 && payloadStart + payloadSize <= stream.Length)
                    {
                        payloadEnd = payloadStart + payloadSize;
                    }
                }
            }

            if (payloadStart + 16 > payloadEnd) return;

            // 2. Validate YPD0 magic & 0x12C Sentinel
            reader.JumpTo(payloadStart);
            string magic = new string(reader.ReadChars(4));
            if (magic != Signature && magic != "YPD\0") return;

            ParticleType = reader.ReadInt32();

            reader.JumpTo(payloadStart + 0x0C);
            Sentinel = reader.ReadInt32();
            if (Sentinel != 0x12C) return;

            // 3. String Tables (External Bone References & Resource Filenames)
            ExternalBones = ReadStringTable(reader, payloadEnd);
            ResourceFiles = ReadStringTable(reader, payloadEnd);

            // 4. Group 1: Emitters (Sprite & Mesh Generators)
            if (reader.BaseStream.Position + 4 > payloadEnd) return;
            int emitterCount = reader.ReadInt32();
            if (emitterCount < 0 || emitterCount > 5000) return;

            for (int i = 0; i < emitterCount; i++)
            {
                if (reader.BaseStream.Position + 8 > payloadEnd) break;

                int type = reader.ReadInt32();
                int byteLen = reader.ReadInt32();
                if (byteLen < 0 || reader.BaseStream.Position + byteLen > payloadEnd) break;

                int wordCount = byteLen / 4;
                var emitter = new ParticleEmitter { Type = (EmitterType)type };

                for (int w = 0; w < wordCount; w++)
                    emitter.Parameters.Add(reader.ReadInt32());

                if (emitter.Parameters.Count > 0) emitter.ResourceIndex = emitter.Parameters[0];
                if (emitter.Parameters.Count > 1) emitter.Flags = emitter.Parameters[1];

                // Decode mesh emitter sub-files
                if (emitter.Type == EmitterType.Mesh && emitter.Parameters.Count >= 3)
                {
                    int coreCount = emitter.Parameters[1];
                    int animCount = emitter.Parameters[2];
                    int cursor = 3;

                    for (int c = 0; c < coreCount && cursor < emitter.Parameters.Count; c++)
                        emitter.CoreFileIndices.Add(emitter.Parameters[cursor++]);
                    for (int a = 0; a < animCount && cursor < emitter.Parameters.Count; a++)
                        emitter.AnimationIndices.Add(emitter.Parameters[cursor++]);
                }

                emitter.DecodeSubRecords();
                Emitters.Add(emitter);
            }

            // 5. Group 2: Simulation Parameter Blocks (TYPD Blocks)
            if (reader.BaseStream.Position + 4 > payloadEnd) return;
            int behaviorCount = reader.ReadInt32();
            if (behaviorCount < 0 || behaviorCount > 5000) return;

            for (int i = 0; i < behaviorCount; i++)
            {
                if (reader.BaseStream.Position + 8 > payloadEnd) break;

                int typeId = reader.ReadInt32();
                int byteLen = reader.ReadInt32();
                if (byteLen < 0 || reader.BaseStream.Position + byteLen > payloadEnd) break;

                int wordCount = byteLen / 4;
                var behavior = new ParticleBehaviorBlock { TypeId = typeId };

                for (int w = 0; w < wordCount; w++)
                    behavior.Parameters.Add(reader.ReadInt32());

                behavior.DecodeStructuredData();
                Behaviors.Add(behavior);
            }

            // 6. Group 3: Sequence Timeline Cues (64 bytes each)
            if (reader.BaseStream.Position + 4 <= payloadEnd)
            {
                int cueCount = reader.ReadInt32();
                if (cueCount > 0 && cueCount <= 5000)
                {
                    for (int i = 0; i < cueCount; i++)
                    {
                        if (reader.BaseStream.Position + 64 > payloadEnd) break;

                        var cue = new ParticleSequenceCue
                        {
                            NextEntryTop = reader.ReadInt32(),
                            NextEntryBottom = reader.ReadInt32(),
                            EffectId = reader.ReadInt32(),
                            TargetId = reader.ReadInt32(),
                            StartTime = reader.ReadInt32(),
                            EndTime = reader.ReadInt32(),
                            Translation = reader.ReadVector3(),
                            Rotation = reader.ReadVector3(),
                            UserData1 = reader.ReadInt32(),
                            UserData2 = reader.ReadInt32(),
                            UserData3 = reader.ReadInt32(),
                            UserData4 = reader.ReadInt32()
                        };

                        SequenceCues.Add(cue);
                    }
                }
            }
        }

        private static List<string> ReadStringTable(BinaryReaderEx reader, long streamEnd)
        {
            List<string> list = new List<string>();
            if (reader.BaseStream.Position + 4 > streamEnd) return list;

            int count = reader.ReadInt32();
            if (count <= 0)
            {
                // Terminal offset marker (0x00000000) must be consumed even when count == 0
                if (reader.BaseStream.Position + 4 <= streamEnd)
                    reader.ReadInt32();
                return list;
            }

            if (reader.BaseStream.Position + (count + 1) * 4 > streamEnd) return list;

            int[] offsets = new int[count + 1];
            for (int i = 0; i <= count; i++)
                offsets[i] = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                int len = offsets[i + 1] - offsets[i];
                if (len > 0 && reader.BaseStream.Position + len <= streamEnd)
                {
                    byte[] bytes = reader.ReadBytes(len);
                    list.Add(Encoding.ASCII.GetString(bytes).TrimEnd('\0'));
                }
                else
                {
                    list.Add(string.Empty);
                }
            }

            return list;
        }
    }
}