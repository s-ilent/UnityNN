// File: Marathon/Editor/Inspector/NinjaSubMotionTypeFormatter.cs
using System.Collections.Generic;
using Marathon.Formats.Mesh.Ninja;

namespace SilentTools.Editor
{
    public static class NinjaSubMotionTypeFormatter
    {
        private struct SubMotionFlagDef
        {
            public uint CategoryMask; // 0 = Any, 1 = Node, 2 = Camera, 4 = Light, 8 = Morph, 16 = Material
            public uint Mask;
            public uint Value;
            public string Name;
        }

        private static readonly SubMotionFlagDef[] FlagDefinitions = new[]
        {
            // Frame & Angle Types (Common)
            new SubMotionFlagDef { CategoryMask = 0, Mask = 0x03U, Value = 1, Name = "NND_SMOTTYPE_FRAME_FLOAT" },
            new SubMotionFlagDef { CategoryMask = 0, Mask = 0x03U, Value = 2, Name = "NND_SMOTTYPE_FRAME_SINT16" },
            new SubMotionFlagDef { CategoryMask = 0, Mask = 0x04U, Value = 0x04U, Name = "NND_SMOTTYPE_ANGLE_RADIAN" },
            new SubMotionFlagDef { CategoryMask = 0, Mask = 0x08U, Value = 0x08U, Name = "NND_SMOTTYPE_ANGLE_ANGLE32" },
            new SubMotionFlagDef { CategoryMask = 0, Mask = 0x10U, Value = 0x10U, Name = "NND_SMOTTYPE_ANGLE_ANGLE16" },

            // Node Motion Tracks (Category 1 / 0)
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x700U, Value = 0x700U, Name = "NND_SMOTTYPE_TRANSLATION_XYZ" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x100U, Value = 0x100U, Name = "NND_SMOTTYPE_TRANSLATION_X" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x200U, Value = 0x200U, Name = "NND_SMOTTYPE_TRANSLATION_Y" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x400U, Value = 0x400U, Name = "NND_SMOTTYPE_TRANSLATION_Z" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x7800U, Value = 0x3800U, Name = "NND_SMOTTYPE_ROTATION_XYZ" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x4000U, Value = 0x4000U, Name = "NND_SMOTTYPE_QUATERNION" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x800U, Value = 0x800U, Name = "NND_SMOTTYPE_ROTATION_X" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x1000U, Value = 0x1000U, Name = "NND_SMOTTYPE_ROTATION_Y" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x2000U, Value = 0x2000U, Name = "NND_SMOTTYPE_ROTATION_Z" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x38000U, Value = 0x38000U, Name = "NND_SMOTTYPE_SCALING_XYZ" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x8000U, Value = 0x8000U, Name = "NND_SMOTTYPE_SCALING_X" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x10000U, Value = 0x10000U, Name = "NND_SMOTTYPE_SCALING_Y" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x20000U, Value = 0x20000U, Name = "NND_SMOTTYPE_SCALING_Z" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x40000U, Value = 0x40000U, Name = "NND_SMOTTYPE_USER_UINT32" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x80000U, Value = 0x80000U, Name = "NND_SMOTTYPE_USER_FLOAT" },
            new SubMotionFlagDef { CategoryMask = 1, Mask = 0x100000U, Value = 0x100000U, Name = "NND_SMOTTYPE_NODEHIDE" },

            // Material Motion Tracks (Category 16)
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x100U, Value = 0x100U, Name = "NND_SMOTTYPE_HIDE" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0xE00U, Value = 0xE00U, Name = "NND_SMOTTYPE_DIFFUSE_RGB" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x200U, Value = 0x200U, Name = "NND_SMOTTYPE_DIFFUSE_R" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x400U, Value = 0x400U, Name = "NND_SMOTTYPE_DIFFUSE_G" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x800U, Value = 0x800U, Name = "NND_SMOTTYPE_DIFFUSE_B" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x1000U, Value = 0x1000U, Name = "NND_SMOTTYPE_ALPHA" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0xE000U, Value = 0xE000U, Name = "NND_SMOTTYPE_SPECULAR_RGB" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x2000U, Value = 0x2000U, Name = "NND_SMOTTYPE_SPECULAR_R" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x4000U, Value = 0x4000U, Name = "NND_SMOTTYPE_SPECULAR_G" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x8000U, Value = 0x8000U, Name = "NND_SMOTTYPE_SPECULAR_B" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x10000U, Value = 0x10000U, Name = "NND_SMOTTYPE_SPECULAR_LEVEL" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x20000U, Value = 0x20000U, Name = "NND_SMOTTYPE_SPECULAR_GLOSS" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x1C0000U, Value = 0x1C0000U, Name = "NND_SMOTTYPE_AMBIENT_RGB" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x40000U, Value = 0x40000U, Name = "NND_SMOTTYPE_AMBIENT_R" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x80000U, Value = 0x80000U, Name = "NND_SMOTTYPE_AMBIENT_G" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x100000U, Value = 0x100000U, Name = "NND_SMOTTYPE_AMBIENT_B" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x200000U, Value = 0x200000U, Name = "NND_SMOTTYPE_TEXTURE_INDEX" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x400000U, Value = 0x400000U, Name = "NND_SMOTTYPE_TEXTURE_BLEND" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x1800000U, Value = 0x1800000U, Name = "NND_SMOTTYPE_OFFSET_UV" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x800000U, Value = 0x800000U, Name = "NND_SMOTTYPE_OFFSET_U" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x1000000U, Value = 0x1000000U, Name = "NND_SMOTTYPE_OFFSET_V" },
            new SubMotionFlagDef { CategoryMask = 16, Mask = 0x2000000U, Value = 0x2000000U, Name = "NND_SMOTTYPE_MATCLBK_USER" },

            // Camera Tracks (Category 2)
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0x1C0000U, Value = 0x1C0000U, Name = "NND_SMOTTYPE_TARGET_XYZ" },
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0x200000U, Value = 0x200000U, Name = "NND_SMOTTYPE_ROLL" },
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0x1C00000U, Value = 0x1C00000U, Name = "NND_SMOTTYPE_UPTARGET_XYZ" },
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0xE000000U, Value = 0xE000000U, Name = "NND_SMOTTYPE_UPVECTOR_XYZ" },
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0x10000000U, Value = 0x10000000U, Name = "NND_SMOTTYPE_FOVY" },
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0x20000000U, Value = 0x20000000U, Name = "NND_SMOTTYPE_ZNEAR" },
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0x40000000U, Value = 0x40000000U, Name = "NND_SMOTTYPE_ZFAR" },
            new SubMotionFlagDef { CategoryMask = 2, Mask = 0x80000000U, Value = 0x80000000U, Name = "NND_SMOTTYPE_ASPECT" },

            // Light Tracks (Category 4)
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0xE00000U, Value = 0xE00000U, Name = "NND_SMOTTYPE_LIGHT_COLOR_RGB" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x1000000U, Value = 0x1000000U, Name = "NND_SMOTTYPE_LIGHT_ALPHA" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x2000000U, Value = 0x2000000U, Name = "NND_SMOTTYPE_LIGHT_INTENSITY" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x4000000U, Value = 0x4000000U, Name = "NND_SMOTTYPE_FALLOFF_START" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x8000000U, Value = 0x8000000U, Name = "NND_SMOTTYPE_FALLOFF_END" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x10000000U, Value = 0x10000000U, Name = "NND_SMOTTYPE_INNER_ANGLE" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x20000000U, Value = 0x20000000U, Name = "NND_SMOTTYPE_OUTER_ANGLE" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x40000000U, Value = 0x40000000U, Name = "NND_SMOTTYPE_INNER_RANGE" },
            new SubMotionFlagDef { CategoryMask = 4, Mask = 0x80000000U, Value = 0x80000000U, Name = "NND_SMOTTYPE_OUTER_RANGE" },

            // Morph Tracks (Category 8)
            new SubMotionFlagDef { CategoryMask = 8, Mask = 0x1000000U, Value = 0x1000000U, Name = "NND_SMOTTYPE_MORPH_WEIGHT" }
        };

        public static string FormatSubMotionType(SubMotionType typeEnum, MotionType parentType)
        {
            uint raw = (uint)typeEnum;
            uint cat = (uint)parentType & 31U;
            if (cat == 0) cat = 1; // Default to node motion category

            List<string> parts = new List<string>();
            HashSet<string> added = new HashSet<string>();

            foreach (var flag in FlagDefinitions)
            {
                if (flag.CategoryMask == 0 || flag.CategoryMask == cat)
                {
                    if ((raw & flag.Mask) == flag.Value && added.Add(flag.Name))
                    {
                        parts.Add(flag.Name);
                    }
                }
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : $"0x{raw:X8}";
        }
    }
}