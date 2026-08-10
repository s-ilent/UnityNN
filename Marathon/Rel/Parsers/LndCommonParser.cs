using Marathon.IO;

namespace SilentTools
{
    public static class LndCommonParser
    {
        public static LndCommonData Parse(BinaryReaderEx reader, uint baseAddr)
        {
            LndCommonData data = new LndCommonData();
            uint floatLoc = (uint)(reader.ReadInt32() - baseAddr);
            uint subListLoc = (uint)(reader.ReadInt32() - baseAddr);

            if (floatLoc > 0 && floatLoc < reader.BaseStream.Length)
            {
                reader.JumpTo(floatLoc);
                data.UnknownFloat = reader.ReadSingle();
            }

            if (subListLoc > 0 && subListLoc < reader.BaseStream.Length)
            {
                reader.JumpTo(subListLoc);
                uint xnt1 = (uint)(reader.ReadInt32() - baseAddr);
                uint xnt2 = (uint)(reader.ReadInt32() - baseAddr);
                uint nbl = (uint)(reader.ReadInt32() - baseAddr);

                if (xnt1 > 0) { reader.JumpTo(xnt1); data.XntFilenameFragment1 = reader.ReadNullTerminatedString(); }
                if (xnt2 > 0) { reader.JumpTo(xnt2); data.XntFilenameFragment2 = reader.ReadNullTerminatedString(); }
                if (nbl > 0) { reader.JumpTo(nbl); data.NblFilenameFragment = reader.ReadNullTerminatedString(); }
            }
            return data;
        }
    }
}