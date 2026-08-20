using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marathon.Formats.Particle
{
    public enum TypdBehaviorType : int
    {
        AmbientLight   = -20, // TYPDParamAmbientLight
        LensFlare      = -19, // TYPDParamLensflare
        AlphaTest      = -18, // TYPDParamAlphaTest
        Boid           = -17, // TYPDParamBoid
        ShadowVolume   = -16, // TYPDParamShadowVolume
        DepthOfField   = -15, // TYPDParamDepthOfField
        ScreenFog      = -14, // TYPDParamScreenFog
        Blur           = -13, // TYPDParamBlur
        PlayInfo       = -12, // TYPDParamPlayInfo
        Thunder        = -11, // TYPDParamThunder
        Script         = -10, // TYPDParamScript
        SpotLight      = -9,  // TYPDParamSpotLight
        Player         = -8,  // TYPDParamPlayer
        SoundEffect    = -7,  // TYPDParamSE
        AdxStream      = -6,  // TYPDParamADXStreamSound
        Stream         = -5,  // TYPDParamStream
        Text           = -4,  // TYPDParamText
        Light          = -3,  // TYPDParamLight
        PlayAnimation  = -2,  // Play Animation
        GenerateParticle = 0, // Particle Generator
        ApplyModel     = 1    // Apply 3D Model
    }

    public class TypdParticleSubKeyframe
    {
        public int KeyIndex { get; set; }
        public float StartSize { get; set; }
        public float EndSize { get; set; }
        public float Lifetime { get; set; }
        public float Velocity { get; set; }
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }
        public float[] CurveParameters { get; set; } = new float[20];
        public int FlagA { get; set; }
        public int FlagB { get; set; }
    }

    public class ParticleGeneratorHeader
    {
        public int Value0 { get; set; }
        public Vector3 SpawnArea { get; set; }
        public float VelocityScale { get; set; }
        public float Radius { get; set; }
        public int BlendMode { get; set; } // 1: Additive, 2: AlphaBlend, 3: Replace
        public int DrawFlags { get; set; }
        public int Bitflags { get; set; }
        public float ParticleLife { get; set; }
        public float InitialSpeed { get; set; }
        public float Gravity { get; set; }
        public float Drag { get; set; }
        public int SubRecordCount { get; set; }
    }

    public class ParticleBehaviorBlock
    {
        public int TypeId { get; set; }
        public List<int> Parameters { get; set; } = new List<int>();

        public TypdBehaviorType BehaviorType => (TypdBehaviorType)TypeId;

        public string TypeName => Enum.IsDefined(typeof(TypdBehaviorType), TypeId)
            ? $"TYPDParam{BehaviorType}"
            : $"UnknownTYPD_{TypeId}";

        public ParticleGeneratorHeader GeneratorHeader { get; private set; }
        public List<TypdParticleSubKeyframe> ParticleSubKeyframes { get; } = new List<TypdParticleSubKeyframe>();

        public float GetFloat(int index)
        {
            if (index < 0 || index >= Parameters.Count) return 0f;
            return BitConverter.ToSingle(BitConverter.GetBytes(Parameters[index]), 0);
        }

        public void DecodeStructuredData()
        {
            // Type 0: Particle Simulation Generator
            if (BehaviorType == TypdBehaviorType.GenerateParticle && Parameters.Count >= 46)
            {
                GeneratorHeader = new ParticleGeneratorHeader
                {
                    Value0 = Parameters[0],
                    SpawnArea = new Vector3(GetFloat(2), GetFloat(4), GetFloat(5)),
                    VelocityScale = GetFloat(1),
                    Radius = GetFloat(3),
                    BlendMode = Parameters[9],
                    DrawFlags = Parameters[10],
                    Bitflags = Parameters[11],
                    ParticleLife = GetFloat(12),
                    InitialSpeed = GetFloat(16),
                    Gravity = GetFloat(37),
                    Drag = GetFloat(38),
                    SubRecordCount = Parameters[39]
                };

                int subCount = GeneratorHeader.SubRecordCount;
                int cursor = 46;

                for (int i = 0; i < subCount && cursor + 31 <= Parameters.Count; i++)
                {
                    var kf = new TypdParticleSubKeyframe
                    {
                        KeyIndex = Parameters[cursor],
                        StartSize = GetFloat(cursor + 1),
                        EndSize = GetFloat(cursor + 2),
                        Lifetime = GetFloat(cursor + 3),
                        Velocity = GetFloat(cursor + 4),
                        StartColor = new Color(GetFloat(cursor + 5), GetFloat(cursor + 6), GetFloat(cursor + 7), GetFloat(cursor + 8)),
                        EndColor = new Color(GetFloat(cursor + 9), GetFloat(cursor + 10), GetFloat(cursor + 11), GetFloat(cursor + 12)),
                        FlagA = Parameters[cursor + 29],
                        FlagB = Parameters[cursor + 30]
                    };

                    for (int c = 0; c < 20; c++)
                        kf.CurveParameters[c] = GetFloat(cursor + 9 + c);

                    ParticleSubKeyframes.Add(kf);
                    cursor += 31;
                }
            }
        }
    }
}