using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace SilentTools.Editor
{
    public partial class UnityNNInspectorWindow : EditorWindow
    {
        private const int ITEMS_PER_PAGE = 50;

        private Dictionary<int, bool> m_NodeFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_SubObjectFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_VertexListFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, int> m_VertexListPages = new Dictionary<int, int>();
        private Dictionary<int, bool> m_PrimitiveListFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_MaterialFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_MaterialColourFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_MaterialLogicFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_TextureMapFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> m_SubMotionFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, int> m_SubMotionPages = new Dictionary<int, int>();
        private Dictionary<int, bool> m_MatSubMotionFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, int> m_MatSubMotionPages = new Dictionary<int, int>();

        private string m_NodeSearchFilter = "";
        private bool m_ExpandAllNodes = false;

        private GUIStyle evenStyle;
        private GUIStyle oddStyle;

        private void ClearState()
        {
            m_NodeFoldouts.Clear();
            m_SubObjectFoldouts.Clear();
            m_VertexListFoldouts.Clear();
            m_VertexListPages.Clear();
            m_PrimitiveListFoldouts.Clear();
            m_MaterialFoldouts.Clear();
            m_MaterialColourFoldouts.Clear();
            m_MaterialLogicFoldouts.Clear();
            m_TextureMapFoldouts.Clear();
            m_SubMotionFoldouts.Clear();
            m_SubMotionPages.Clear();
            m_MatSubMotionFoldouts.Clear();
            m_MatSubMotionPages.Clear();
        }

        public static string CleanEnumString(object enumValue)
        {
            if (enumValue == null) return "None";
            string raw = enumValue.ToString();
            return raw
                .Replace("NND_NODETYPE_", "")
                .Replace("NND_SMOTTYPE_", "")
                .Replace("NND_MATTYPE_", "")
                .Replace("NND_VTXTYPE_XB_", "")
                .Replace("NND_VTXTYPE_", "")
                .Replace("NND_D3DFVF_", "")
                .Replace("NND_PRIMTYPE_", "")
                .Replace("NND_MOTIONTYPE_", "")
                .Replace("NND_SMOTIPTYPE_", "")
                .Replace("NNE_NODENAME_SORTTYPE_", "")
                .Replace("NNE_BLENDMODE_", "")
                .Replace("NNE_BLENDOP_", "")
                .Replace("NNE_CMPFUNC_", "");
        }

        public static void DrawCleanFlagsLabel(object enumValue, string labelPrefix = "Flags:")
        {
            string cleaned = CleanEnumString(enumValue);
            EditorGUILayout.LabelField(labelPrefix, cleaned, EditorStyles.wordWrappedLabel);
        }

        private static void DrawPaginationControls(ref int currentPage, int totalItems, int itemsPerPage)
        {
            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / itemsPerPage));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Prev", GUILayout.Width(50)) && currentPage > 0) currentPage--;
            EditorGUILayout.LabelField($"Page {currentPage + 1}/{totalPages} ({currentPage * itemsPerPage}-{Mathf.Min(totalItems, (currentPage + 1) * itemsPerPage) - 1})");
            if (GUILayout.Button("Next", GUILayout.Width(50)) && currentPage < totalPages - 1) currentPage++;
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawMatrix4x4ReadOnly(Matrix4x4 mat)
        {
            EditorGUILayout.LabelField($"R0: {mat.m00:F4}, {mat.m01:F4}, {mat.m02:F4}, {mat.m03:F4}");
            EditorGUILayout.LabelField($"R1: {mat.m10:F4}, {mat.m11:F4}, {mat.m12:F4}, {mat.m13:F4}");
            EditorGUILayout.LabelField($"R2: {mat.m20:F4}, {mat.m21:F4}, {mat.m22:F4}, {mat.m23:F4}");
            EditorGUILayout.LabelField($"R3: {mat.m30:F4}, {mat.m31:F4}, {mat.m32:F4}, {mat.m33:F4}");
        }

        private void EnsureStyles()
        {
            if (evenStyle == null)
            {
                evenStyle = new GUIStyle();
                evenStyle.normal.background = MakeTexture(2, 2, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.35f) : new Color(0.9f, 0.9f, 0.35f, 0.35f));
                evenStyle.margin = new RectOffset(0, 0, 0, 0);
                evenStyle.padding = new RectOffset(4, 4, 1, 1);
            }
            if (oddStyle == null)
            {
                oddStyle = new GUIStyle();
                oddStyle.normal.background = MakeTexture(2, 2, EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f, 0.1f) : new Color(0.95f, 0.95f, 0.95f, 0.1f));
                oddStyle.margin = new RectOffset(0, 0, 0, 0);
                oddStyle.padding = new RectOffset(4, 4, 1, 1);
            }
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }
    }
}