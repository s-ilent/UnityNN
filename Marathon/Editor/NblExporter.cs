// File: Marathon/Editor/NblExporter.cs
using UnityNN;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Marathon.Formats.Archive;

namespace UnityNN.Editor
{
    public static class NblExporter
    {
        [MenuItem("Assets/UnityNN/Extract NBL Archive to Folder...")]
        public static void ExtractSelectedNbl()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null) return;

            string assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(assetPath) ||
                (!assetPath.EndsWith(".nbl", StringComparison.OrdinalIgnoreCase) &&
                 !assetPath.EndsWith(".gbl", StringComparison.OrdinalIgnoreCase) &&
                 !assetPath.EndsWith(".zbl", StringComparison.OrdinalIgnoreCase)))
            {
                EditorUtility.DisplayDialog("Invalid Asset", "Please select an .nbl, .gbl, or .zbl archive file in the Project window.", "OK");
                return;
            }

            ExtractNblToDirectory(assetPath);
        }

        public static void ExtractNblToDirectory(string assetPath)
        {
            string absolutePath = ResolveAbsoluteAssetPath(assetPath);
            if (!File.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog("File Not Found", $"Could not find archive file on disk at:\n{absolutePath}", "OK");
                return;
            }

            string folderName = Path.GetFileNameWithoutExtension(assetPath) + "_extracted";
            string targetDir = Path.Combine(Path.GetDirectoryName(absolutePath), folderName);

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            int extractedFiles = 0;
            int pngConverted = 0;
            int warningCount = 0;

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

                            string outFilePath = Path.Combine(targetDir, fileName);
                            File.WriteAllBytes(outFilePath, entry.RawData);
                            extractedFiles++;

                            // If entry is XVR texture, export as PNG as well
                            if (fileName.EndsWith(".xvr", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    Texture2D tex = XvrDecoder.DecodeXvrToTexture2D(entry.Header.SubHeader, entry.RawData);
                                    if (tex != null)
                                    {
                                        byte[] pngBytes = tex.EncodeToPNG();
                                        string pngPath = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(fileName) + ".png");
                                        File.WriteAllBytes(pngPath, pngBytes);
                                        UnityEngine.Object.DestroyImmediate(tex);
                                        pngConverted++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    warningCount++;
                                    Debug.LogWarning($"[NblExporter] Texture conversion to PNG failed for '{fileName}': {ex.Message}");
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
                EditorUtility.DisplayDialog("Extraction Error", $"An error occurred while reading the archive:\n{ex.Message}", "OK");
                return;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("NBL Extraction Complete",
                $"Successfully extracted {extractedFiles} files to:\n{targetDir}\n• {pngConverted} XVR textures converted to PNG\n• {warningCount} warnings.", "OK");
        }

        private static string ResolveAbsoluteAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return "";
            if (Path.IsPathRooted(assetPath)) return assetPath;

            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }
    }
}