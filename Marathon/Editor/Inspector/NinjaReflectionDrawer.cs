using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SilentTools.Editor
{
    public static class NinjaReflectionDrawer
    {
        private const int ITEMS_PER_PAGE = 50;
        private static Dictionary<int, int> s_PaginationPages = new Dictionary<int, int>();

        /// <summary>
        /// Programmatically reflects over and draws any object's public properties and fields.
        /// </summary>
        public static void DrawObjectReflectively(object target, string label = "", int depth = 0, int maxDepth = 6)
        {
            if (target == null)
            {
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, "null");
                }
                return;
            }

            if (depth > maxDepth)
            {
                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField(label, target.ToString());
                }
                return;
            }

            Type type = target.GetType();

            if (type.IsEnum)
            {
                EditorGUILayout.LabelField(label, UnityNNInspectorWindow.CleanEnumString(target));
                return;
            }

            if (type == typeof(string))
            {
                EditorGUILayout.LabelField(label, (string)target);
                return;
            }

            if (type.IsPrimitive || target is decimal)
            {
                EditorGUILayout.LabelField(label, Convert.ToString(target, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            if (target is Vector2 v2)
            {
                EditorGUILayout.Vector2Field(label, v2);
                return;
            }

            if (target is Vector3 v3)
            {
                EditorGUILayout.Vector3Field(label, v3);
                return;
            }

            if (target is Vector4 v4)
            {
                EditorGUILayout.Vector4Field(label, v4);
                return;
            }

            if (target is Color col)
            {
                EditorGUILayout.ColorField(label, col);
                return;
            }

            if (target is Color32 col32)
            {
                EditorGUILayout.ColorField(label, col32);
                return;
            }

            if (target is Matrix4x4 mat)
            {
                if (!string.IsNullOrEmpty(label)) EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"R0: {mat.m00:F4}, {mat.m01:F4}, {mat.m02:F4}, {mat.m03:F4}");
                EditorGUILayout.LabelField($"R1: {mat.m10:F4}, {mat.m11:F4}, {mat.m12:F4}, {mat.m13:F4}");
                EditorGUILayout.LabelField($"R2: {mat.m20:F4}, {mat.m21:F4}, {mat.m22:F4}, {mat.m23:F4}");
                EditorGUILayout.LabelField($"R3: {mat.m30:F4}, {mat.m31:F4}, {mat.m32:F4}, {mat.m33:F4}");
                EditorGUI.indentLevel--;
                return;
            }

            // Collection / List / Array Handling
            if (target is IEnumerable list && !(target is string))
            {
                List<object> itemList = new List<object>();
                foreach (var item in list) itemList.Add(item);

                if (!string.IsNullOrEmpty(label))
                {
                    EditorGUILayout.LabelField($"{label} ({itemList.Count} items)", EditorStyles.boldLabel);
                }

                if (itemList.Count == 0) return;

                EditorGUI.indentLevel++;

                int pageKey = (target.GetHashCode() ^ depth);
                if (!s_PaginationPages.ContainsKey(pageKey)) s_PaginationPages[pageKey] = 0;

                int currentPage = s_PaginationPages[pageKey];
                int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)itemList.Count / ITEMS_PER_PAGE));

                if (itemList.Count > ITEMS_PER_PAGE)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Prev", GUILayout.Width(50)) && currentPage > 0) currentPage--;
                    EditorGUILayout.LabelField($"Page {currentPage + 1}/{totalPages} ({currentPage * ITEMS_PER_PAGE}-{Mathf.Min(itemList.Count, (currentPage + 1) * ITEMS_PER_PAGE) - 1})");
                    if (GUILayout.Button("Next", GUILayout.Width(50)) && currentPage < totalPages - 1) currentPage++;
                    EditorGUILayout.EndHorizontal();
                    s_PaginationPages[pageKey] = currentPage;
                }

                int startIdx = currentPage * ITEMS_PER_PAGE;
                int endIdx = Mathf.Min(itemList.Count, (currentPage + 1) * ITEMS_PER_PAGE);

                for (int idx = startIdx; idx < endIdx; idx++)
                {
                    DrawObjectReflectively(itemList[idx], $"[{idx:000}]", depth + 1, maxDepth);
                }

                EditorGUI.indentLevel--;
                return;
            }

            // Complex Class or Struct
            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }

            EditorGUI.indentLevel++;

            PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                object val = null;
                try { val = prop.GetValue(target, null); } catch { continue; }

                DrawObjectReflectively(val, prop.Name, depth + 1, maxDepth);
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                object val = null;
                try { val = field.GetValue(target); } catch { continue; }

                DrawObjectReflectively(val, field.Name, depth + 1, maxDepth);
            }

            EditorGUI.indentLevel--;
        }
    }
}