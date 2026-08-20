using System;
using System.Collections.Generic;

namespace Marathon.Formats.Particle
{
    public enum EmitterType { Sprite = 0, Mesh = 1 }

    public class SpriteEmitterSubRecord
    {
        public int SubEmitterId { get; set; }
        public int BlendMode { get; set; }
        public float Size { get; set; }
        public int Flags { get; set; }
    }

    public class ParticleEmitter
    {
        public EmitterType Type { get; set; }
        public int ResourceIndex { get; set; } = -1;
        public int Flags { get; set; }
        public List<int> Parameters { get; set; } = new List<int>();

        // Typed Sprite Sub-Records
        public List<SpriteEmitterSubRecord> SpriteSubRecords { get; set; } = new List<SpriteEmitterSubRecord>();

        // Mesh Emitter Companion Files (Core models + Animations)
        public List<int> CoreFileIndices { get; set; } = new List<int>();
        public List<int> AnimationIndices { get; set; } = new List<int>();

        public void DecodeSubRecords()
        {
            if (Type == EmitterType.Sprite && Parameters.Count >= 3)
            {
                int subCount = Parameters[2];
                int cursor = 3;
                for (int i = 0; i < subCount && cursor + 4 <= Parameters.Count; i++)
                {
                    SpriteSubRecords.Add(new SpriteEmitterSubRecord
                    {
                        SubEmitterId = Parameters[cursor],
                        BlendMode = Parameters[cursor + 1],
                        Size = BitConverter.ToSingle(BitConverter.GetBytes(Parameters[cursor + 2]), 0),
                        Flags = Parameters[cursor + 3]
                    });
                    cursor += 4;
                }
            }
        }
    }
}