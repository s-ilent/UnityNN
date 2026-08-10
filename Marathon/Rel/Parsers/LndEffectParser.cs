using UnityEngine;
using Marathon.IO;

namespace SilentTools
{
    public static class LndEffectParser
    {
        public static LndEffectData Parse(BinaryReaderEx reader, uint baseAddr)
        {
            LndEffectData data = new LndEffectData();
            uint l1 = (uint)(reader.ReadInt32() - baseAddr);
            uint l2 = (uint)(reader.ReadInt32() - baseAddr);
            uint l3 = (uint)(reader.ReadInt32() - baseAddr);
            uint gradLoc = (uint)(reader.ReadInt32() - baseAddr);
            uint fogLoc = (uint)(reader.ReadInt32() - baseAddr);
            uint sunLoc = (uint)(reader.ReadInt32() - baseAddr);
            uint blurLoc = (uint)(reader.ReadInt32() - baseAddr);

            reader.JumpTo(l1); data.PlayerLight1 = ReadLight(reader);
            reader.JumpTo(l2); data.PlayerLight2 = ReadLight(reader);
            reader.JumpTo(l3); data.PlayerLightAmbient = ReadLight(reader);

            reader.JumpTo(gradLoc);
            data.TopGradient = ReadGradient(reader);
            data.BottomGradient = ReadGradient(reader);

            reader.JumpTo(fogLoc); data.Fog = ReadFog(reader);

            reader.JumpTo(sunLoc);
            data.SunPosition = reader.ReadVector3();
            data.SunUnknown = reader.ReadSingle();

            reader.JumpTo(blurLoc);
            data.BlurStartDistance = reader.ReadSingle();
            data.BlurUnknown = reader.ReadSingle();
            data.BlurPixelCount = reader.ReadInt32();
            data.BlurDistance = reader.ReadSingle();
            data.BlurOpacity = reader.ReadSingle();

            return data;
        }

        public static LndLightData ReadLight(BinaryReaderEx reader)
        {
            LndLightData l = new LndLightData();
            l.Direction = reader.ReadVector3();
            Vector3 c = reader.ReadVector3();
            l.LightColor = new Color(c.x, c.y, c.z, 1.0f);
            return l;
        }

        public static LndGradientData ReadGradient(BinaryReaderEx reader)
        {
            LndGradientData g = new LndGradientData();
            g.StartHeight = reader.ReadSingle();
            g.EndHeight = reader.ReadSingle();
            Vector4 c1 = reader.ReadVector4();
            Vector4 c2 = reader.ReadVector4();
            g.StartColor = new Color(c1.x, c1.y, c1.z, c1.w);
            g.EndColor = new Color(c2.x, c2.y, c2.z, c2.w);
            g.GradientMultiplier = reader.ReadSingle();
            g.DestinationMultiplier = reader.ReadSingle();
            return g;
        }

        public static LndFogData ReadFog(BinaryReaderEx reader)
        {
            LndFogData f = new LndFogData();
            f.NearPlane = reader.ReadSingle();
            f.FarPlane = reader.ReadSingle();
            f.InitialIntensity = reader.ReadSingle();
            f.RampUp = reader.ReadSingle();
            Vector3 c = reader.ReadVector3();
            f.FogColor = new Color(c.x, c.y, c.z, 1.0f);
            return f;
        }
    }
}