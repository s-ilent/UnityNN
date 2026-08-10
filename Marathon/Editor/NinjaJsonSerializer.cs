// File: Marathon/Editor/NinjaJsonSerializer.cs
using System;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SilentTools.Editor
{
    public static class NinjaJsonSerializer
    {
        public static string Serialize(object obj, int indentLevel = 0)
        {
            if (obj == null) return "null";

            Type type = obj.GetType();

            if (obj is string str)
            {
                string escaped = str.Replace("\\", "\\\\")
                                   .Replace("\"", "\\\"")
                                   .Replace("\n", "\\n")
                                   .Replace("\r", "\\r");
                return $"\"{escaped}\"";
            }

            if (type.IsPrimitive || obj is decimal)
            {
                if (obj is bool b) return b ? "true" : "false";
                return Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (type.IsEnum)
            {
                return $"\"{obj}\"";
            }

            if (obj is Vector2 v2)
            {
                return $"{{\"x\": {v2.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {v2.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}";
            }

            if (obj is Vector3 v3)
            {
                return $"{{\"x\": {v3.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {v3.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {v3.z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}";
            }

            if (obj is Vector4 v4)
            {
                return $"{{\"x\": {v4.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {v4.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {v4.z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"w\": {v4.w.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}";
            }

            if (obj is IEnumerable list && !(obj is string))
            {
                StringBuilder sbList = new StringBuilder();
                sbList.AppendLine("[");
                string indent = new string(' ', (indentLevel + 1) * 2);
                string childIndent = new string(' ', indentLevel * 2);

                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sbList.AppendLine(",");
                    first = false;
                    sbList.Append(indent + Serialize(item, indentLevel + 1));
                }
                sbList.AppendLine();
                sbList.Append(childIndent + "]");
                return sbList.ToString();
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            string objIndent = new string(' ', (indentLevel + 1) * 2);
            string closeIndent = new string(' ', indentLevel * 2);

            PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            bool isFirstProp = true;

            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                object val = null;
                try { val = prop.GetValue(obj, null); } catch { continue; }

                if (!isFirstProp) sb.AppendLine(",");
                isFirstProp = false;

                sb.Append($"{objIndent}\"{prop.Name}\": {Serialize(val, indentLevel + 1)}");
            }

            sb.AppendLine();
            sb.Append(closeIndent + "}");
            return sb.ToString();
        }
    }
}