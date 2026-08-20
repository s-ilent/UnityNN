// File: Marathon/NinjaIconResolver.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SilentTools
{
    public static class NinjaIconResolver
    {
        private static readonly Dictionary<string, Texture2D> s_IconCache = new Dictionary<string, Texture2D>();

        public static Texture2D GetIconForExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return null;

            string extKey = extension.ToLowerInvariant();
            if (!extKey.StartsWith('.')) extKey = "." + extKey;

            if (s_IconCache.TryGetValue(extKey, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            string labelStr = extKey.TrimStart('.').ToUpperInvariant();
            Texture2D icon = CreateCompositeIcon(extKey, labelStr);
            
            if (icon != null)
            {
                s_IconCache[extKey] = icon;
            }

            return icon;
        }

        private static Texture2D CreateCompositeIcon(string extension, string labelStr)
        {
            const int Width = 128;
            const int Height = 128;

            Texture2D baseIcon = FetchBuiltInTexture(GetBuiltInIconName(extension));

            var canvas = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[Width * Height];

            if (baseIcon != null)
            {
                Texture2D readableBase = MakeReadable(baseIcon, Width, Height);
                Array.Copy(readableBase.GetPixels32(), pixels, pixels.Length);
                UnityEngine.Object.DestroyImmediate(readableBase);
            }
            else
            {
                Array.Fill(pixels, new Color32(38, 42, 48, 255));
            }

            Texture2D nnBadge = LoadBadge("NN");
            if (nnBadge != null)
            {
                OverlayBadge(pixels, Width, Height, nnBadge, 4, Height - nnBadge.height - 4);
                UnityEngine.Object.DestroyImmediate(nnBadge);
            }

            Texture2D typeBadge = LoadBadge(labelStr);
            if (typeBadge != null)
            {
                int badgeX = Width - typeBadge.width - 4;
                OverlayBadge(pixels, Width, Height, typeBadge, badgeX, 4);
                UnityEngine.Object.DestroyImmediate(typeBadge);
            }

            canvas.SetPixels32(pixels);
            canvas.Apply();
            return canvas;
        }

        private static Texture2D LoadBadge(string badgeName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);

            // Correctly resolve base path whether inside Packages or Assets
            string baseDir = packageInfo != null ? packageInfo.assetPath : "Assets";
            string assetPath = $"{baseDir}/Badges/Badge_{badgeName}.png";

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            return tex != null ? MakeReadable(tex, tex.width, tex.height) : null;
        }

        private static string GetBuiltInIconName(string ext) => ext switch
        {
            ".xno" or ".gno" or ".zno" or ".xna" or ".gna" => "d_Prefab Icon",
            ".xnj" or ".gnj"                               => "d_PrefabVariant Icon",
            ".xnm" or ".gnm" or ".znm" or ".xnv" or ".gnv" => "d_AnimationClip Icon",
            ".xnt" or ".gnt" or ".znt"                     => "d_Texture2D Icon",
            ".xnn" or ".gnn" or ".znn"                     => "d_TextAsset Icon",
            ".rel" or ".xnr" or ".gnr" or ".znr"           => "d_Assembly Icon",
            ".dat"                                         => "d_ParticleSystem Icon",
            ".xnc" or ".gnc"                               => "d_Camera Icon",
            ".xnl" or ".gnl"                               => "d_Light Icon",
            _                                              => "d_DefaultAsset Icon"
        };

        private static Texture2D FetchBuiltInTexture(string name)
        {
            return EditorGUIUtility.FindTexture(name) 
                ?? EditorGUIUtility.IconContent(name)?.image as Texture2D;
        }

        private static Texture2D MakeReadable(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var readable = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                readable.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                readable.Apply();
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void OverlayBadge(Color32[] canvasPixels, int canvasW, int canvasH, Texture2D badgeTex, int startX, int startY)
        {
            Color32[] badgePixels = badgeTex.GetPixels32();
            int bw = badgeTex.width;
            int bh = badgeTex.height;

            for (int py = 0; py < bh; py++)
            {
                int cy = startY + py;
                if (cy < 0 || cy >= canvasH) continue;

                for (int px = 0; px < bw; px++)
                {
                    int cx = startX + px;
                    if (cx < 0 || cx >= canvasW) continue;

                    Color32 src = badgePixels[py * bw + px];
                    int dstIdx = cy * canvasW + cx;
                    canvasPixels[dstIdx] = AlphaBlend(canvasPixels[dstIdx], src);
                }
            }
        }

        private static Color32 AlphaBlend(Color32 dst, Color32 src)
        {
            if (src.a == 255) return src;
            if (src.a == 0) return dst;

            float sa = src.a / 255f;
            float da = dst.a / 255f;
            float outA = sa + da * (1f - sa);
            if (outA <= 0) return new Color32(0, 0, 0, 0);

            byte r = (byte)((src.r * sa + dst.r * da * (1f - sa)) / outA);
            byte g = (byte)((src.g * sa + dst.g * da * (1f - sa)) / outA);
            byte b = (byte)((src.b * sa + dst.b * da * (1f - sa)) / outA);
            byte a = (byte)(outA * 255f);

            return new Color32(r, g, b, a);
        }
    }
}
#endif