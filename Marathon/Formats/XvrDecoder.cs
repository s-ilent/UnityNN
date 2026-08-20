// File: Marathon/Formats/XvrDecoder.cs
using System;
using UnityEngine;

namespace SilentTools
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
            if (header == null || header.Length < 0x20 || rawData == null) return null;

            byte pixelFormat = header[0x18];
            byte pixelFlags = header[0x19];
            int width = BitConverter.ToInt16(header, 0x1C);
            int height = BitConverter.ToInt16(header, 0x1E);

            if (width <= 0 || height <= 0) return null;

            int startingOffset = 0;
            if (BitConverter.ToInt32(rawData, 0) == 0x840001)
            {
                startingOffset = BitConverter.ToInt32(rawData, 0x14) == 0 ? 0x7E0 : 0x20;
            }

            int dataLen = rawData.Length - startingOffset;
            byte[] pixelBytes = new byte[dataLen];
            Array.Copy(rawData, startingOffset, pixelBytes, 0, dataLen);

            // DXT1 Compression
            if (pixelFlags == 0x73 || pixelFlags == 0x74)
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.DXT1, false);
                tex.LoadRawTextureData(pixelBytes);
                tex.Apply();
                return tex;
            }
            // DXT5 Compression
            if (pixelFlags == 0x7B || pixelFlags == 0x7C)
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.DXT5, false);
                tex.LoadRawTextureData(pixelBytes);
                tex.Apply();
                return tex;
            }

            // Unswizzle Morton-order Raster Formats (ARGB8888, ARGB1555, RGB565)
            byte[] rgbaPixels = UnswizzleRaster(pixelBytes, width, height, pixelFormat);
            if (rgbaPixels != null)
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(rgbaPixels);
                tex.Apply();
                return tex;
            }

            return null;
        }

        private static byte[] UnswizzleRaster(byte[] swizzledData, int width, int height, byte pixelFormat)
        {
            int maxV = (int)Math.Log(width, 2);
            int maxU = (int)Math.Log(height, 2);

            byte[] rgba = new byte[width * height * 4];
            int bpp = (pixelFormat == 6 || pixelFormat == 7 || pixelFormat == 20) ? 4 : 2;

            for (int j = 0; (j < width * height) && (j * bpp < swizzledData.Length); j++)
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