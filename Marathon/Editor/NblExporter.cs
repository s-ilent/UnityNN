// File: Marathon/Editor/NblExporter.cs
using UnityNN;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Marathon.Formats.Archive;

namespace UnityNN.Editor
{
    public static class NblExporter
    {
        private const string MENU_EXTRACT = "Assets/UnityNN/Extract NBL Archive to Folder...";
        private const string MENU_EXTRACT_RAW = "Assets/UnityNN/Extract NBL Archive (Include Raw XVR)...";

        #region Context Menu & Validation
        [MenuItem(MENU_EXTRACT, false, 20)]
        public static void ExtractSelectedNbls()
        {
            ExtractSelectedArchives(convertXvrToPngOnly: true);
        }

        [MenuItem(MENU_EXTRACT, true)]
        public static bool ValidateExtractSelectedNbls()
        {
            return HasValidNblSelection();
        }

        [MenuItem(MENU_EXTRACT_RAW, false, 21)]
        public static void ExtractSelectedNblsRaw()
        {
            ExtractSelectedArchives(convertXvrToPngOnly: false);
        }

        [MenuItem(MENU_EXTRACT_RAW, true)]
        public static bool ValidateExtractSelectedNblsRaw()
        {
            return HasValidNblSelection();
        }

        private static bool HasValidNblSelection()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
                return false;

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (IsNblPath(path))
                    return true;
            }

            return false;
        }

        private static bool IsNblPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".nbl" or ".gbl" or ".zbl";
        }
        #endregion

        #region Multi-Extraction Execution
        public static void ExtractSelectedArchives(bool convertXvrToPngOnly = true)
        {
            List<string> nblPaths = new List<string>();

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (IsNblPath(path) && !nblPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    nblPaths.Add(path);
                }
            }

            if (nblPaths.Count == 0) return;

            int totalArchives = nblPaths.Count;
            int totalExtractedFiles = 0;
            int totalPngsConverted = 0;
            int totalWarnings = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < totalArchives; i++)
                {
                    string assetPath = nblPaths[i];
                    string archiveName = Path.GetFileName(assetPath);

                    EditorUtility.DisplayProgressBar(
                        "Extracting NBL Archives",
                        $"Processing {archiveName} ({i + 1}/{totalArchives})...",
                        (float)i / totalArchives
                    );

                    ExtractNblInternal(
                        assetPath,
                        convertXvrToPngOnly,
                        out int extractedFiles,
                        out int pngConverted,
                        out int warnings
                    );

                    totalExtractedFiles += extractedFiles;
                    totalPngsConverted += pngConverted;
                    totalWarnings += warnings;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog(
                "NBL Extraction Complete",
                $"Successfully extracted {totalArchives} archive(s):\n" +
                $"• Total Files Extracted: {totalExtractedFiles}\n" +
                $"• XVR Textures Converted to PNG: {totalPngsConverted}\n" +
                $"• Warnings/Errors: {totalWarnings}",
                "OK"
            );
        }

        public static void ExtractNblToDirectory(string assetPath, bool convertXvrToPngOnly = true)
        {
            if (!IsNblPath(assetPath)) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                ExtractNblInternal(assetPath, convertXvrToPngOnly, out int extracted, out int pngs, out int warnings);
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();

                string folderName = Path.GetFileNameWithoutExtension(assetPath) + "_extracted";
                EditorUtility.DisplayDialog(
                    "NBL Extraction Complete",
                    $"Successfully extracted archive:\n• Destination: {folderName}\n• Total Files: {extracted}\n• Converted PNGs: {pngs}\n• Warnings: {warnings}",
                    "OK"
                );
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static void ExtractNblInternal(
            string assetPath,
            bool convertXvrToPngOnly,
            out int extractedFiles,
            out int pngConverted,
            out int warningCount)
        {
            extractedFiles = 0;
            pngConverted = 0;
            warningCount = 0;

            string absolutePath = ResolveAbsoluteAssetPath(assetPath);
            if (!File.Exists(absolutePath))
            {
                warningCount++;
                Debug.LogWarning($"[NblExporter] Could not find file on disk: {absolutePath}");
                return;
            }

            string folderName = Path.GetFileNameWithoutExtension(assetPath) + "_extracted";
            string targetDir = Path.Combine(Path.GetDirectoryName(absolutePath), folderName).Replace('\\', '/');

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            try
            {
                using (FileStream fs = File.OpenRead(absolutePath))
                {
                    NblArchive nbl = NblArchive.Load(fs);

                    foreach (var entry in nbl.Entries)
                    {
                        try
                        {
                            string fileName = entry.Header.FileName;
                            if (string.IsNullOrEmpty(fileName))
                            {
                                fileName = $"file_{entry.Header.Identifier}.bin";
                            }

                            bool isXvr = fileName.EndsWith(".xvr", StringComparison.OrdinalIgnoreCase) || entry.ChunkID == "TMLL";

                            if (isXvr && convertXvrToPngOnly)
                            {
                                // Direct decode to PNG only (avoids cluttering project with raw XVR)
                                Texture2D tex = null;
                                try
                                {
                                    tex = XvrDecoder.DecodeXvrToTexture2D(entry.Header.SubHeader, entry.RawData);
                                    if (tex != null)
                                    {
                                        byte[] pngBytes = tex.EncodeToPNG();
                                        string pngPath = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(fileName) + ".png");
                                        File.WriteAllBytes(pngPath, pngBytes);
                                        pngConverted++;
                                        extractedFiles++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    warningCount++;
                                    Debug.LogWarning($"[NblExporter] PNG decode failed for '{fileName}': {ex.Message}");
                                }
                                finally
                                {
                                    if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                                }

                                // Fallback to saving raw XVR if texture conversion produced nothing
                                if (tex == null)
                                {
                                    string outFilePath = Path.Combine(targetDir, fileName);
                                    File.WriteAllBytes(outFilePath, entry.RawData);
                                    extractedFiles++;
                                }
                            }
                            else
                            {
                                // Write raw file
                                string outFilePath = Path.Combine(targetDir, fileName);
                                File.WriteAllBytes(outFilePath, entry.RawData);
                                extractedFiles++;

                                // Also output converted PNG alongside XVR if requested
                                if (isXvr)
                                {
                                    Texture2D tex = null;
                                    try
                                    {
                                        tex = XvrDecoder.DecodeXvrToTexture2D(entry.Header.SubHeader, entry.RawData);
                                        if (tex != null)
                                        {
                                            byte[] pngBytes = tex.EncodeToPNG();
                                            string pngPath = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(fileName) + ".png");
                                            File.WriteAllBytes(pngPath, pngBytes);
                                            pngConverted++;
                                        }
                                    }
                                    catch { }
                                    finally
                                    {
                                        if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            warningCount++;
                            Debug.LogWarning($"[NblExporter] Failed extracting entry '{entry.Header.FileName}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                warningCount++;
                Debug.LogError($"[NblExporter] Failed to extract archive {assetPath}:\n{ex}");
            }
        }

        private static string ResolveAbsoluteAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "";
            if (Path.IsPathRooted(assetPath)) return assetPath.Replace('\\', '/');

            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }
        #endregion
    }
}