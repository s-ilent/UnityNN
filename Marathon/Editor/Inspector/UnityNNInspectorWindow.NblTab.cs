// File: Marathon/Editor/Inspector/UnityNNInspectorWindow.NblTab.cs
using UnityNN;
using UnityEngine;
using UnityEditor;
using System.IO;
using Marathon.Formats.Archive;

namespace UnityNN.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        private int m_NblEntriesPage = 0;
        private string m_NblSearchFilter = "";

        private void DrawNblTab()
        {
            if (!m_Context.IsNblAsset) return;

            EnsureStyles();
            var nbl = m_Context.NblData;

            EditorGUILayout.LabelField($"NBL Archive Structure ({nbl.Chunks.Count} Chunks, {nbl.Entries.Count} Total Files)", EditorStyles.boldLabel);

            // 1. Chunk Metadata Overview Cards
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Archive Chunks Breakdown", EditorStyles.boldLabel);
            for (int c = 0; c < nbl.Chunks.Count; c++)
            {
                var ch = nbl.Chunks[c];
                string encStr = ch.IsEncrypted ? $"Key: 0x{ch.DecryptKey:X8}" : "Unencrypted";
                string compStr = ch.IsCompressed ? $"Compressed ({ch.CompressedSize / 1024f:F1} KB -> {ch.UncompressedSize / 1024f:F1} KB)" : "Uncompressed";

                EditorGUILayout.LabelField($"• Chunk [{c}] '{ch.ChunkID}' (v0x{ch.FileVersion:X4}) - {ch.NumFiles} files | {encStr} | {compStr} | {ch.PointerCount} pointers");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 2. Toolbar & Extraction Actions
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("File Entries", EditorStyles.miniBoldLabel, GUILayout.Width(80));
            m_NblSearchFilter = EditorGUILayout.TextField(m_NblSearchFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45))) m_NblSearchFilter = "";

            if (GUILayout.Button("Extract All to Folder...", EditorStyles.toolbarButton, GUILayout.Width(150)))
            {
                NblExporter.ExtractNblToDirectory(m_Context.AssetPath);
            }
            EditorGUILayout.EndHorizontal();

            // Filter entries
            var matchingEntries = nbl.Entries.FindAll(e =>
                string.IsNullOrEmpty(m_NblSearchFilter) ||
                (e.Header.FileName != null && e.Header.FileName.IndexOf(m_NblSearchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                (e.Header.Identifier != null && e.Header.Identifier.IndexOf(m_NblSearchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
            );

            DrawPaginationControls(ref m_NblEntriesPage, matchingEntries.Count, 25);

            // 3. Entries Table Header
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Index", EditorStyles.miniBoldLabel, GUILayout.Width(45));
            GUILayout.Label("Chunk", EditorStyles.miniBoldLabel, GUILayout.Width(55));
            GUILayout.Label("ID", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            GUILayout.Label("File Name", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
            GUILayout.Label("File Size", EditorStyles.miniBoldLabel, GUILayout.Width(80));
            GUILayout.Label("Pointers", EditorStyles.miniBoldLabel, GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();

            int start = m_NblEntriesPage * 25;
            int end = Mathf.Min(matchingEntries.Count, (m_NblEntriesPage + 1) * 25);

            for (int i = start; i < end; i++)
            {
                var entry = matchingEntries[i];
                GUIStyle rowBg = (i % 2 == 0) ? evenStyle : oddStyle;

                EditorGUILayout.BeginHorizontal(rowBg, GUILayout.Height(18));
                GUILayout.Label($"[{i:000}]", EditorStyles.miniBoldLabel, GUILayout.Width(45));
                GUILayout.Label(entry.ChunkID, EditorStyles.label, GUILayout.Width(55));
                GUILayout.Label(entry.Header.Identifier ?? "STD", EditorStyles.miniLabel, GUILayout.Width(50));
                GUILayout.Label(entry.Header.FileName ?? "<unnamed>", EditorStyles.label, GUILayout.ExpandWidth(true));
                GUILayout.Label(FormatFileSize(entry.Header.FileSize), EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label($"{entry.Header.PointerSize / 4}", EditorStyles.miniLabel, GUILayout.Width(65));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private static string FormatFileSize(uint bytes)
        {
            if (bytes >= 1024 * 1024) return $"{(bytes / (1024f * 1024f)):F2} MB";
            if (bytes >= 1024) return $"{(bytes / 1024f):F1} KB";
            return $"{bytes} B";
        }
    }
}