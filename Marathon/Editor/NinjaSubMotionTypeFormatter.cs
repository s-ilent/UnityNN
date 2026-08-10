// File: Marathon/Editor/NinjaSubMotionTypeFormatter.cs
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public static class NinjaSubMotionTypeFormatter
    {
        public static string FormatSubMotionType(SubMotionType typeEnum, MotionType parentType)
        {
            uint raw_val = (uint)typeEnum;
            uint cat = (uint)parentType & 31U; // NND_MOTIONTYPE_CATEGORY_MASK
            List<string> parts = new List<string>();

            uint frame_part = raw_val & 3U;
            if (frame_part == 1) parts.Add("NND_SMOTTYPE_FRAME_FLOAT");
            else if (frame_part == 2) parts.Add("NND_SMOTTYPE_FRAME_SINT16");

            uint angle_part = raw_val & 28U;
            if ((angle_part & 4U) != 0) parts.Add("NND_SMOTTYPE_ANGLE_RADIAN");
            if ((angle_part & 8U) != 0) parts.Add("NND_SMOTTYPE_ANGLE_ANGLE32");
            if ((angle_part & 16U) != 0) parts.Add("NND_SMOTTYPE_ANGLE_ANGLE16");

            if (cat == 1 || cat == 0) // Node Motion
            {
                uint trans = raw_val & 0x700U;
                if (trans == 0x700U) parts.Add("NND_SMOTTYPE_TRANSLATION_XYZ");
                else
                {
                    if ((trans & 0x100U) != 0) parts.Add("NND_SMOTTYPE_TRANSLATION_X");
                    if ((trans & 0x200U) != 0) parts.Add("NND_SMOTTYPE_TRANSLATION_Y");
                    if ((trans & 0x400U) != 0) parts.Add("NND_SMOTTYPE_TRANSLATION_Z");
                }

                uint rot = raw_val & 0x7800U;
                if (rot == 0x3800U) parts.Add("NND_SMOTTYPE_ROTATION_XYZ");
                else if ((rot & 0x4000U) != 0) parts.Add("NND_SMOTTYPE_QUATERNION");
                else
                {
                    if ((rot & 0x800U) != 0) parts.Add("NND_SMOTTYPE_ROTATION_X");
                    if ((rot & 0x1000U) != 0) parts.Add("NND_SMOTTYPE_ROTATION_Y");
                    if ((rot & 0x2000U) != 0) parts.Add("NND_SMOTTYPE_ROTATION_Z");
                }

                uint scl = raw_val & 0x38000U;
                if (scl == 0x38000U) parts.Add("NND_SMOTTYPE_SCALING_XYZ");
                else
                {
                    if ((scl & 0x8000U) != 0) parts.Add("NND_SMOTTYPE_SCALING_X");
                    if ((scl & 0x10000U) != 0) parts.Add("NND_SMOTTYPE_SCALING_Y");
                    if ((scl & 0x20000U) != 0) parts.Add("NND_SMOTTYPE_SCALING_Z");
                }

                if ((raw_val & 0x40000U) != 0) parts.Add("NND_SMOTTYPE_USER_UINT32");
                if ((raw_val & 0x80000U) != 0) parts.Add("NND_SMOTTYPE_USER_FLOAT");
                if ((raw_val & 0x10000U) != 0) parts.Add("NND_SMOTTYPE_NODEHIDE");
            }
            else if (cat == 16) // Material Motion
            {
                if ((raw_val & 0x100U) != 0) parts.Add("NND_SMOTTYPE_HIDE");

                uint diff = raw_val & 0xE00U;
                if (diff == 0xE00U) parts.Add("NND_SMOTTYPE_DIFFUSE_RGB");
                else
                {
                    if ((diff & 0x200U) != 0) parts.Add("NND_SMOTTYPE_DIFFUSE_R");
                    if ((diff & 0x400U) != 0) parts.Add("NND_SMOTTYPE_DIFFUSE_G");
                    if ((diff & 0x800U) != 0) parts.Add("NND_SMOTTYPE_DIFFUSE_B");
                }

                if ((raw_val & 0x1000U) != 0) parts.Add("NND_SMOTTYPE_ALPHA");

                uint spec = raw_val & 0xE000U;
                if (spec == 0xE000U) parts.Add("NND_SMOTTYPE_SPECULAR_RGB");
                else
                {
                    if ((spec & 0x2000U) != 0) parts.Add("NND_SMOTTYPE_SPECULAR_R");
                    if ((spec & 0x4000U) != 0) parts.Add("NND_SMOTTYPE_SPECULAR_G");
                    if ((spec & 0x8000U) != 0) parts.Add("NND_SMOTTYPE_SPECULAR_B");
                }

                if ((raw_val & 0x10000U) != 0) parts.Add("NND_SMOTTYPE_SPECULAR_LEVEL");
                if ((raw_val & 0x20000U) != 0) parts.Add("NND_SMOTTYPE_SPECULAR_GLOSS");

                uint amb = raw_val & 0x1C0000U;
                if (amb == 0x1C0000U) parts.Add("NND_SMOTTYPE_AMBIENT_RGB");
                else
                {
                    if ((amb & 0x40000U) != 0) parts.Add("NND_SMOTTYPE_AMBIENT_R");
                    if ((amb & 0x80000U) != 0) parts.Add("NND_SMOTTYPE_AMBIENT_G");
                    if ((amb & 0x100000U) != 0) parts.Add("NND_SMOTTYPE_AMBIENT_B");
                }

                if ((raw_val & 0x200000U) != 0) parts.Add("NND_SMOTTYPE_TEXTURE_INDEX");
                if ((raw_val & 0x400000U) != 0) parts.Add("NND_SMOTTYPE_TEXTURE_BLEND");

                uint uv = raw_val & 0x1800000U;
                if (uv == 0x1800000U) parts.Add("NND_SMOTTYPE_OFFSET_UV");
                else
                {
                    if ((uv & 0x800000U) != 0) parts.Add("NND_SMOTTYPE_OFFSET_U");
                    if ((uv & 0x1000000U) != 0) parts.Add("NND_SMOTTYPE_OFFSET_V");
                }

                if ((raw_val & 0x2000000U) != 0) parts.Add("NND_SMOTTYPE_MATCLBK_USER");
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : $"0x{raw_val:X8}";
        }
    }
}