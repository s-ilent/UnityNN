// File: Marathon/Editor/Inspector/NinjaJsonSerializer.cs
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SilentTools.Editor
{
    public static class NinjaJsonSerializer
    {
        /// <summary>
        /// Serializes an object to a JSON string in memory (suitable for small/medium objects).
        /// </summary>
        public static string Serialize(object obj, int indentLevel = 0)
        {
            using (StringWriter sw = new StringWriter())
            {
                Serialize(obj, sw, indentLevel);
                return sw.ToString();
            }
        }

        /// <summary>
        /// Streams object serialization directly to a file without large in-memory string allocations.
        /// </summary>
        public static void SerializeToFile(string filePath, object obj)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8, 65536))
            {
                Serialize(obj, sw, 0);
            }
        }

        /// <summary>
        /// Recursively streams object serialization to a TextWriter.
        /// </summary>
        public static void Serialize(object obj, TextWriter writer, int indentLevel = 0)
        {
            if (obj == null)
            {
                writer.Write("null");
                return;
            }

            if (indentLevel > 12)
            {
                writer.Write("\"...\"");
                return;
            }

            Type type = obj.GetType();

            if (obj is string str)
            {
                writer.Write("\"");
                for (int i = 0; i < str.Length; i++)
                {
                    char c = str[i];
                    switch (c)
                    {
                        case '\\': writer.Write("\\\\"); break;
                        case '"': writer.Write("\\\""); break;
                        case '\n': writer.Write("\\n"); break;
                        case '\r': writer.Write("\\r"); break;
                        case '\t': writer.Write("\\t"); break;
                        default: writer.Write(c); break;
                    }
                }
                writer.Write("\"");
                return;
            }

            if (type.IsPrimitive || obj is decimal)
            {
                if (obj is bool b)
                {
                    writer.Write(b ? "true" : "false");
                }
                else
                {
                    writer.Write(Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture));
                }
                return;
            }

            if (type.IsEnum)
            {
                writer.Write("\"");
                writer.Write(obj.ToString());
                writer.Write("\"");
                return;
            }

            if (obj is Vector2 v2)
            {
                writer.Write($"{{\"x\": {v2.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {v2.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");
                return;
            }

            if (obj is Vector3 v3)
            {
                writer.Write($"{{\"x\": {v3.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {v3.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {v3.z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");
                return;
            }

            if (obj is Vector4 v4)
            {
                writer.Write($"{{\"x\": {v4.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {v4.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {v4.z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"w\": {v4.w.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");
                return;
            }

            if (obj is Quaternion q)
            {
                writer.Write($"{{\"x\": {q.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {q.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {q.z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"w\": {q.w.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");
                return;
            }

            if (obj is Color col)
            {
                writer.Write($"{{\"r\": {col.r.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"g\": {col.g.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"b\": {col.b.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"a\": {col.a.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");
                return;
            }

            if (obj is Matrix4x4 mat)
            {
                writer.Write("{");
                writer.Write($"\"m00\": {mat.m00.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m01\": {mat.m01.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m02\": {mat.m02.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m03\": {mat.m03.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                writer.Write($"\"m10\": {mat.m10.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m11\": {mat.m11.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m12\": {mat.m12.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m13\": {mat.m13.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                writer.Write($"\"m20\": {mat.m20.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m21\": {mat.m21.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m22\": {mat.m22.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m23\": {mat.m23.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, ");
                writer.Write($"\"m30\": {mat.m30.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m31\": {mat.m31.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m32\": {mat.m32.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}, \"m33\": {mat.m33.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");
                return;
            }

            // Dictionary as JSON Object
            if (obj is IDictionary dict)
            {
                writer.WriteLine("{");
                string objIndent = new string(' ', (indentLevel + 1) * 2);
                string closeIndent = new string(' ', indentLevel * 2);

                bool first = true;
                foreach (DictionaryEntry kvp in dict)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.Write($"{objIndent}\"{kvp.Key}\": ");
                    Serialize(kvp.Value, writer, indentLevel + 1);
                }
                writer.WriteLine();
                writer.Write(closeIndent + "}");
                return;
            }

            // List / Array
            if (obj is IEnumerable list && !(obj is string))
            {
                writer.WriteLine("[");
                string indent = new string(' ', (indentLevel + 1) * 2);
                string childIndent = new string(' ', indentLevel * 2);

                bool first = true;
                foreach (var item in list)
                {
                    if (!first) writer.WriteLine(",");
                    first = false;
                    writer.Write(indent);
                    Serialize(item, writer, indentLevel + 1);
                }
                writer.WriteLine();
                writer.Write(childIndent + "]");
                return;
            }

            // Complex Class or Struct (Properties + Fields)
            writer.WriteLine("{");
            string propIndent = new string(' ', (indentLevel + 1) * 2);
            string endIndent = new string(' ', indentLevel * 2);

            bool isFirstEntry = true;

            PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                object val = null;
                try { val = prop.GetValue(obj, null); } catch { continue; }

                if (!isFirstEntry) writer.WriteLine(",");
                isFirstEntry = false;

                writer.Write($"{propIndent}\"{prop.Name}\": ");
                Serialize(val, writer, indentLevel + 1);
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                object val = null;
                try { val = field.GetValue(obj); } catch { continue; }

                if (!isFirstEntry) writer.WriteLine(",");
                isFirstEntry = false;

                writer.Write($"{propIndent}\"{field.Name}\": ");
                Serialize(val, writer, indentLevel + 1);
            }

            writer.WriteLine();
            writer.Write(endIndent + "}");
        }
    }
}