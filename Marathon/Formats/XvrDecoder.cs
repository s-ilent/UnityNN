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
                if (BitConverter.ToInt32(rawData, 0x20) == 0x840001)
                {
                    startingOffset = 0x40;
                }
                else
                {
                    startingOffset = 0x20;
                }
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
                // DXT1 Compression (0x73, 0x74)
                if (pixelFlags == 0x73 || pixelFlags == 0x74)
                {
                    int minBytes = Mathf.Max(8, (width * height) / 2);
                    if (pixelBytes.Length >= minBytes)
                    {
                        Texture2D tex = new Texture2D(width, height, TextureFormat.DXT1, false);
                        byte[] dxtData = pixelBytes.Length == minBytes ? pixelBytes : SliceBytes(pixelBytes, minBytes);
                        tex.LoadRawTextureData(dxtData);
                        tex.Apply();
                        return tex;
                    }
                }

                // DXT3 Compression (0x77, 0x78, 0x75, 0x76) / DXT5 Compression (0x7B, 0x7C)
                if (pixelFlags == 0x77 || pixelFlags == 0x78 || pixelFlags == 0x75 || pixelFlags == 0x76 ||
                    pixelFlags == 0x7B || pixelFlags == 0x7C || pixelFlags == 0x79 || pixelFlags == 0x7A)
                {
                    int minBytes = Mathf.Max(16, width * height);
                    if (pixelBytes.Length >= minBytes)
                    {
                        Texture2D tex = new Texture2D(width, height, TextureFormat.DXT5, false);
                        byte[] dxtData = pixelBytes.Length == minBytes ? pixelBytes : SliceBytes(pixelBytes, minBytes);
                        tex.LoadRawTextureData(dxtData);
                        tex.Apply();
                        return tex;
                    }
                }

                // Unswizzle Morton-order Raster Formats
                byte[] rgbaPixels = UnswizzleRaster(pixelBytes, width, height, pixelFormat);
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

        private static byte[] SliceBytes(byte[] source, int length)
        {
            byte[] dest = new byte[length];
            Array.Copy(source, 0, dest, 0, Math.Min(source.Length, length));
            return dest;
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
                    int dstIdx = (u * width + v) * 4;
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