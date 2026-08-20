using System.Collections.Generic;
using UnityEngine;

namespace SilentTools
{
    public enum EffectEmitterType
    {
        TextureSprite = 0,
        MeshModel = 1
    }

    public enum TypdParameterType
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

    public class EffectEmitterEntry
    {
        public EffectEmitterType Type { get; set; }
        public List<int> Fields { get; set; } = new List<int>();

        public string GetSummary(List<string> filenames)
        {
            if (Fields.Count == 0) return $"Emitter ({Type})";
            string resource = (filenames != null && Fields[0] >= 0 && Fields[0] < filenames.Count)
                ? filenames[Fields[0]] : $"Resource_{Fields[0]}";

            if (Type == EffectEmitterType.TextureSprite)
                return $"Sprite: {resource} (Flags: 0x{Fields[1]:X2}, Extra: {Fields[2]})";

            return $"Model: {resource} (Core: {Fields[1]}, Anims: {Fields[2]})";
        }
    }

    public class EffectParameterBlock
    {
        public TypdParameterType Type { get; set; }
        public int RawType { get; set; }
        public List<int> Fields { get; set; } = new List<int>();

        public string TypeName => System.Enum.IsDefined(typeof(TypdParameterType), RawType)
            ? $"TYPDParam{Type}"
            : $"UnknownTYPD_{RawType}";
    }

    public class EffectSequenceCue
    {
        public int NextEntryTop { get; set; }
        public int NextEntryBottom { get; set; }
        public int EffectId { get; set; }
        public int UnknownId { get; set; }
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public Vector3 Translation { get; set; }
        public Vector3 Rotation { get; set; }
        public int[] Flags { get; set; } = new int[4];
    }

    public class ParticleEffectData
    {
        public int ParticleType { get; set; }
        public List<string> ExternalBones { get; set; } = new List<string>();
        public List<string> ResourceFiles { get; set; } = new List<string>();
        public List<EffectEmitterEntry> Emitters { get; set; } = new List<EffectEmitterEntry>();
        public List<EffectParameterBlock> ParameterBlocks { get; set; } = new List<EffectParameterBlock>();
        public List<EffectSequenceCue> SequenceCues { get; set; } = new List<EffectSequenceCue>();
    }
}