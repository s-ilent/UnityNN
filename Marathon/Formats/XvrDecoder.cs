// File: Marathon/Formats/XvrDecoder.cs
using System;
using UnityEngine;

namespace UnityNN
{
    public static class XvrDecoder
    {
        public static Texture2D DecodeXvrFile(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length < 0x20) return null;

            byte[] header = new byte[0x20];
            Array.Copy(fileBytes, 0, header, 0, 0x20);

            return DecodeXvrToTexture2D(header, fileBytes);
        }

        public static Texture2D DecodeXvrToTexture2D(byte[] header, byte[] rawData)
        {
            if (header == null || header.Length < 0x20 || rawData == null || rawData.Length == 0) return null;

            byte pixelFormat = header[0x18];
            byte pixelFlags = header[0x19];
            int width = BitConverter.ToInt16(header, 0x1C);
            int height = BitConverter.ToInt16(header, 0x1E);

            // Guard against endian flipped dimensions
            if (width < 0 || height < 0)
            {
                width = (short)((header[0x1C] << 8) | header[0x1D]);
                height = (short)((header[0x1E] << 8) | header[0x1F]);
            }

            if (width <= 0 || height <= 0 || width > 4096 || height > 4096) return null;

            int startingOffset = 0;

            // Check if rawData starts with 0x00840001 descriptor (NBL sub-entry payload)
            if (rawData.Length >= 4 && BitConverter.ToInt32(rawData, 0) == 0x840001)
            {
                startingOffset = 0x20;
            }
            // Check if rawData starts with GBIX / PVRT (standalone file)
            else if (rawData.Length >= 0x40 &&
                     (header[0] == 'G' && header[1] == 'B' && header[2] == 'I' && header[3] == 'X') ||
                     (header[0x10] == 'P' && header[0x11] == 'V' && header[0x12] == 'R' && header[0x13] == 'T'))
            {
                startingOffset = (BitConverter.ToInt32(rawData, 0x20) == 0x840001) ? 0x40 : 0x20;
            }

            if (startingOffset >= rawData.Length)
            {
                startingOffset = 0;
            }

            int dataLen = rawData.Length - startingOffset;
            if (dataLen <= 0) return null;

            byte[] pixelBytes = new byte[dataLen];
            Array.Copy(rawData, startingOffset, pixelBytes, 0, dataLen);

            try
            {
                byte[] rgbaPixels = null;

                // DXT1 Compression (0x73, 0x74)
                if (pixelFlags == 0x73 || pixelFlags == 0x74)
                {
                    rgbaPixels = DecodeDxt1(pixelBytes, width, height);
                }
                // DXT3 / DXT5 Compression (0x77, 0x78, 0x75, 0x76, 0x7B, 0x7C, 0x79, 0x7A)
                else if (pixelFlags == 0x77 || pixelFlags == 0x78 || pixelFlags == 0x75 || pixelFlags == 0x76 ||
                         pixelFlags == 0x7B || pixelFlags == 0x7C || pixelFlags == 0x79 || pixelFlags == 0x7A)
                {
                    rgbaPixels = DecodeDxt5(pixelBytes, width, height);
                }
                // Unswizzle Morton-order Raster Formats
                else
                {
                    rgbaPixels = UnswizzleRaster(pixelBytes, width, height, pixelFormat);
                }

                if (rgbaPixels != null)
                {
                    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(rgbaPixels);
                    tex.Apply();
                    return tex;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[XvrDecoder] Texture decode failed ({width}x{height}, flags: 0x{pixelFlags:X2}): {ex.Message}");
            }

            return null;
        }

        private static byte[] DecodeDxt1(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blocksY = Math.Max(1, (height + 3) / 4);
            int offset = 0;

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    if (offset + 8 > data.Length) break;

                    ushort c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
                    ushort c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));
                    uint code = (uint)(data[offset + 4] | (data[offset + 5] << 8) | (data[offset + 6] << 16) | (data[offset + 7] << 24));
                    offset += 8;

                    UnpackRgb565(c0, out byte r0, out byte g0, out byte b0);
                    UnpackRgb565(c1, out byte r1, out byte g1, out byte b1);

                    byte[] palR = new byte[4];
                    byte[] palG = new byte[4];
                    byte[] palB = new byte[4];
                    byte[] palA = new byte[4];

                    palR[0] = r0; palG[0] = g0; palB[0] = b0; palA[0] = 255;
                    palR[1] = r1; palG[1] = g1; palB[1] = b1; palA[1] = 255;

                    if (c0 > c1)
                    {
                        palR[2] = (byte)((2 * r0 + r1) / 3);
                        palG[2] = (byte)((2 * g0 + g1) / 3);
                        palB[2] = (byte)((2 * b0 + b1) / 3);
                        palA[2] = 255;

                        palR[3] = (byte)((r0 + 2 * r1) / 3);
                        palG[3] = (byte)((g0 + 2 * g1) / 3);
                        palB[3] = (byte)((b0 + 2 * b1) / 3);
                        palA[3] = 255;
                    }
                    else
                    {
                        palR[2] = (byte)((r0 + r1) / 2);
                        palG[2] = (byte)((g0 + g1) / 2);
                        palB[2] = (byte)((b0 + b1) / 2);
                        palA[2] = 255;

                        palR[3] = 0; palG[3] = 0; palB[3] = 0; palA[3] = 0;
                    }

                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;

                            if (x < width && y < height)
                            {
                                int targetY = height - 1 - y; // Direct vertical inversion for Unity
                                int idx = (int)((code >> (2 * (py * 4 + px))) & 3);
                                int dst = (targetY * width + x) * 4;

                                rgba[dst + 0] = palR[idx];
                                rgba[dst + 1] = palG[idx];
                                rgba[dst + 2] = palB[idx];
                                rgba[dst + 3] = palA[idx];
                            }
                        }
                    }
                }
            }

            return rgba;
        }

        private static byte[] DecodeDxt5(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            int blocksX = Math.Max(1, (width + 3) / 4);
            int blocksY = Math.Max(1, (height + 3) / 4);
            int offset = 0;

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    if (offset + 16 > data.Length) break;

                    // 1. Alpha block (8 bytes)
                    byte a0 = data[offset];
                    byte a1 = data[offset + 1];
                    ulong aBits = (ulong)data[offset + 2] |
                                  ((ulong)data[offset + 3] << 8) |
                                  ((ulong)data[offset + 4] << 16) |
                                  ((ulong)data[offset + 5] << 24) |
                                  ((ulong)data[offset + 6] << 32) |
                                  ((ulong)data[offset + 7] << 40);
                    offset += 8;

                    byte[] palA = new byte[8];
                    palA[0] = a0;
                    palA[1] = a1;

                    if (a0 > a1)
                    {
                        for (int k = 1; k < 7; k++)
                            palA[k + 1] = (byte)(((7 - k) * a0 + k * a1) / 7);
                    }
                    else
                    {
                        for (int k = 1; k < 5; k++)
                            palA[k + 1] = (byte)(((5 - k) * a0 + k * a1) / 5);
                        palA[6] = 0;
                        palA[7] = 255;
                    }

                    // 2. Color block (8 bytes)
                    ushort c0 = (ushort)(data[offset] | (data[offset + 1] << 8));
                    ushort c1 = (ushort)(data[offset + 2] | (data[offset + 3] << 8));
                    uint code = (uint)(data[offset + 4] | (data[offset + 5] << 8) | (data[offset + 6] << 16) | (data[offset + 7] << 24));
                    offset += 8;

                    UnpackRgb565(c0, out byte r0, out byte g0, out byte b0);
                    UnpackRgb565(c1, out byte r1, out byte g1, out byte b1);

                    byte[] palR = new byte[4];
                    byte[] palG = new byte[4];
                    byte[] palB = new byte[4];

                    palR[0] = r0; palG[0] = g0; palB[0] = b0;
                    palR[1] = r1; palG[1] = g1; palB[1] = b1;
                    palR[2] = (byte)((2 * r0 + r1) / 3);
                    palG[2] = (byte)((2 * g0 + g1) / 3);
                    palB[2] = (byte)((2 * b0 + b1) / 3);
                    palR[3] = (byte)((r0 + 2 * r1) / 3);
                    palG[3] = (byte)((g0 + 2 * g1) / 3);
                    palB[3] = (byte)((b0 + 2 * b1) / 3);

                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;

                            if (x < width && y < height)
                            {
                                int targetY = height - 1 - y; // Direct vertical inversion for Unity
                                int pIdx = py * 4 + px;
                                int cIdx = (int)((code >> (2 * pIdx)) & 3);
                                int aIdx = (int)((aBits >> (3 * pIdx)) & 7);
                                int dst = (targetY * width + x) * 4;

                                rgba[dst + 0] = palR[cIdx];
                                rgba[dst + 1] = palG[cIdx];
                                rgba[dst + 2] = palB[cIdx];
                                rgba[dst + 3] = palA[aIdx];
                            }
                        }
                    }
                }
            }

            return rgba;
        }

        private static void UnpackRgb565(ushort c, out byte r, out byte g, out byte b)
        {
            r = (byte)(((c >> 11) & 0x1F) * 255 / 31);
            g = (byte)(((c >> 5) & 0x3F) * 255 / 63);
            b = (byte)((c & 0x1F) * 255 / 31);
        }

        private static byte[] UnswizzleRaster(byte[] swizzledData, int width, int height, byte pixelFormat)
        {
            int maxV = (int)Math.Log(width, 2);
            int maxU = (int)Math.Log(height, 2);

            byte[] rgba = new byte[width * height * 4];
            int bpp = (pixelFormat == 6 || pixelFormat == 7 || pixelFormat == 20 || pixelFormat == 21) ? 4 : 2;

            for (int j = 0; (j < width * height) && (j * bpp + bpp <= swizzledData.Length); j++)
            {
                int u = 0, v = 0;
                int origCoord = j;
                for (int k = 0; k < maxU || k < maxV; k++)
                {
                    if (k < maxV) { v |= (origCoord & 1) << k; origCoord >>= 1; }
                    if (k < maxU) { u |= (origCoord & 1) << k; origCoord >>= 1; }
                }

                if (u < height && v < width)
                {
                    int targetU = height - 1 - u; // Direct vertical inversion for Unity
                    int dstIdx = (targetU * width + v) * 4;
                    int srcIdx = j * bpp;

                    if (dstIdx + 3 >= rgba.Length) continue;

                    if (bpp == 4) // ARGB8888
                    {
                        rgba[dstIdx + 0] = swizzledData[srcIdx + 2]; // R
                        rgba[dstIdx + 1] = swizzledData[srcIdx + 1]; // G
                        rgba[dstIdx + 2] = swizzledData[srcIdx + 0]; // B
                        rgba[dstIdx + 3] = swizzledData[srcIdx + 3]; // A
                    }
                    else if (pixelFormat == 2) // ARGB1555
                    {
                        ushort color = (ushort)(swizzledData[srcIdx] | (swizzledData[srcIdx + 1] << 8));
                        rgba[dstIdx + 0] = (byte)(((color >> 10) & 0x1F) * 255 / 31);
                        rgba[dstIdx + 1] = (byte)(((color >> 5) & 0x1F) * 255 / 31);
                        rgba[dstIdx + 2] = (byte)((color & 0x1F) * 255 / 31);
                        rgba[dstIdx + 3] = (byte)(((color >> 15) & 0x1) * 255);
                    }
                    else if (pixelFormat == 4) // ARGB4444
                    {
                        byte gb = swizzledData[srcIdx + 0];
                        byte ar = swizzledData[srcIdx + 1];
                        rgba[dstIdx + 0] = (byte)((ar & 0x0F) * 255 / 15);
                        rgba[dstIdx + 1] = (byte)((gb >> 4)   * 255 / 15);
                        rgba[dstIdx + 2] = (byte)((gb & 0x0F) * 255 / 15);
                        rgba[dstIdx + 3] = (byte)((ar >> 4)   * 255 / 15);
                    }
                    else if (pixelFormat == 5) // RGB565
                    {
                        ushort color = (ushort)(swizzledData[srcIdx] | (swizzledData[srcIdx + 1] << 8));
                        rgba[dstIdx + 0] = (byte)(((color >> 11) & 0x1F) * 255 / 31);
                        rgba[dstIdx + 1] = (byte)(((color >> 5) & 0x3F) * 255 / 63);
                        rgba[dstIdx + 2] = (byte)((color & 0x1F) * 255 / 31);
                        rgba[dstIdx + 3] = 255;
                    }
                }
            }
            return rgba;
        }
    }
}