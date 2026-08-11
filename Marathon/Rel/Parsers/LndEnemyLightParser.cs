using Marathon.IO;

namespace SilentTools
{
    public static class LndEnemyLightParser
    {
        public static LndEnemyLightData Parse(BinaryReaderEx reader, uint fileSize)
        {
            LndEnemyLightData data = new LndEnemyLightData();
            uint l1 = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);
            uint l2 = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);
            uint l3 = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, reader.Offset);

            if (l1 > 0 && l1 < fileSize) { reader.JumpTo(l1); data.Light1 = LndEffectParser.ReadLight(reader); }
            if (l2 > 0 && l2 < fileSize) { reader.JumpTo(l2); data.Light2 = LndEffectParser.ReadLight(reader); }
            if (l3 > 0 && l3 < fileSize) { reader.JumpTo(l3); data.LightAmbient = LndEffectParser.ReadLight(reader); }
            return data;
        }
    }
}