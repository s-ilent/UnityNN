using Marathon.IO;

namespace SilentTools
{
    public static class LndEnemyLightParser
    {
        public static LndEnemyLightData Parse(BinaryReaderEx reader, uint baseAddr)
        {
            LndEnemyLightData data = new LndEnemyLightData();
            uint l1 = (uint)(reader.ReadInt32() - baseAddr);
            uint l2 = (uint)(reader.ReadInt32() - baseAddr);
            uint l3 = (uint)(reader.ReadInt32() - baseAddr);

            reader.JumpTo(l1); data.Light1 = LndEffectParser.ReadLight(reader);
            reader.JumpTo(l2); data.Light2 = LndEffectParser.ReadLight(reader);
            reader.JumpTo(l3); data.LightAmbient = LndEffectParser.ReadLight(reader);
            return data;
        }
    }
}