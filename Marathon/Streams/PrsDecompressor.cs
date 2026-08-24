// File: Marathon/Streams/PrsDecompressor.cs
using System;

namespace Marathon.IO
{
    public class PrsDecompressor
    {
        private byte[] _decompBuffer;
        private int _currDecompPos = 0;
        private byte _ctrlByte = 0;
        private int _ctrlByteCounter = 1;

        private bool GetCtrlBit()
        {
            _ctrlByteCounter--;
            if (_ctrlByteCounter == 0)
            {
                if (_currDecompPos >= _decompBuffer.Length) return false;
                _ctrlByte = _decompBuffer[_currDecompPos++];
                _ctrlByteCounter = 8;
            }
            bool temp = (_ctrlByte & 1) != 0;
            _ctrlByte >>= 1;
            return temp;
        }

        public static byte[] Decompress(byte[] input, uint outCount)
        {
            PrsDecompressor decomp = new PrsDecompressor();
            decomp._decompBuffer = input;
            decomp._currDecompPos = 0;
            decomp._ctrlByte = 0;
            decomp._ctrlByteCounter = 1;

            byte[] output = new byte[outCount];
            int destPos = 0;

            try
            {
                while (destPos < output.Length && decomp._currDecompPos < input.Length)
                {
                    while (decomp.GetCtrlBit())
                    {
                        if (destPos >= output.Length || decomp._currDecompPos >= input.Length) break;
                        output[destPos++] = decomp._decompBuffer[decomp._currDecompPos++];
                    }

                    int tempPos = 0;
                    int tempCount = 0;

                    if (decomp.GetCtrlBit())
                    {
                        if (decomp._currDecompPos >= decomp._decompBuffer.Length)
                            break;
                        int origCount = decomp._decompBuffer[decomp._currDecompPos++];
                        int origPos = decomp._decompBuffer[decomp._currDecompPos++];
                        tempCount = origCount;
                        tempPos = origPos;
                        if (tempCount == 0 && tempPos == 0)
                            break;
                        tempPos = (tempPos << 5) + (tempCount >> 3) - 0x2000;
                        tempCount &= 7;

                        if (tempCount == 0)
                        {
                            if (decomp._currDecompPos >= decomp._decompBuffer.Length) break;
                            tempCount = decomp._decompBuffer[decomp._currDecompPos++] + 1;
                        }
                        else
                            tempCount += 2;
                    }
                    else
                    {
                        tempCount = 2;
                        if (decomp.GetCtrlBit())
                            tempCount += 2;
                        if (decomp.GetCtrlBit())
                            tempCount++;
                        if (decomp._currDecompPos >= decomp._decompBuffer.Length) break;
                        tempPos = decomp._decompBuffer[decomp._currDecompPos++] - 0x100;
                    }

                    int lookbackPos = destPos + tempPos;
                    if (lookbackPos >= 0)
                    {
                        for (int i = 0; i < tempCount && destPos < output.Length; i++)
                        {
                            output[destPos++] = output[lookbackPos++];
                        }
                    }
                }
            }
            catch
            {
                // Graceful exit on truncated input
            }
            return output;
        }
    }
}