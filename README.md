# UnityNN

**UnityNN** is a Unity package for importing, inspecting, and rendering assets from Sega's NinjaNext / Sega NN file format ecosystem (used in titles such as *Phantasy Star Universe*, *Sonic the Hedgehog (2006)*, *Sonic 4*, and related games).

---

## 🚀 Quick Start Guide

1. **Importing Assets:** Drag any supported Sega NN file (`.xno`, `.xnj`, `.xnm`, `.rel`, `.nbl`) into your Unity `Assets/` project folder. UnityNN's `NinjaNextImporter` automatically parses and builds the asset.
2. **Opening Data Inspector:** Navigate to **Window > UnityNN > Data Inspector** in the Unity top menu to view detailed hierarchy trees, vertex lists, material logic, and animation keyframes.
3. **Extracting Archives:** Right-click any `.nbl` file in the Project window and select **Assets > UnityNN > Extract NBL Archive to Folder...**.

---

## 🌟 Key Features

Note: UnityNN's functionality has been primarily tested with PSU's animated static meshes. Character models, enemy models, and models from other games probably won't work. 

- **Full Model & Mesh Importing (`.xno`, `.xna`, `.xnj`, `.gno`, `.zno`):** Imports rigid and skinned 3D mesh hierarchies, bone matrices, inverse bind poses, vertex colors, UVs, normals, and tangents.
- **BAMS Motion & Animation Resolver (`.xnm`, `.xnv`, `.gnm`, `.znm`):** Converts keyframes into native Unity `AnimationClip` curves mapped to transform hierarchies and material parameters.
- **XVR Texture Decoding (`.xvr`, `.xnt`):** Native decoding of XVR texture formats.
- **NBL / GBL / ZBL Archive Extraction (`.nbl`):** Archive loading and automated folder extraction for compressed PRS/Deflate payload archives.
- **REL Stage Layout & Collision Loader (`.rel`, `.xnr`):** Imports stage layout object placement, monster spawn waves, fog/lighting environments, and collision geometry.
- **Interactive Data Inspector:** Unity Editor window for inspecting node hierarchies, vertex/primitive arrays, material logic, motion tracks, and dumping raw format JSON.
- **Decompiled Engine Shader:** Custom HLSL shader and Shader GUI reproducing Sega NN multi-texture blending, MatCap reflections, vertex color multipliers, and forward lighting passes.

---

## 👏 Credits & Acknowledgments

### Sega NN Format Research & Libraries
- **hyperbx / Knuxfan24** — Creators of the **[Marathon](https://github.com/Knuxfan24/Marathon)** library, providing base C# data structures and binary reading/writing logic for Sega NN (`NinjaNext`) assets (`.xno`, `.xnm`, `.xna`, `.xnt`, `.nbl`).
- **Radfordhound** — Invaluable research and documentation on Sega NN format flags, BAMS angle systems, node types, vertex structures, and submotion interpolation types.
- **Agra ([Agrathejagged / TenoraWorks](https://github.com/Agrathejagged/tenora-works))** — Research, documentation, and object definitions for Phantasy Star Universe `.rel` / `.xnr` stage layout, environment, and mission file structures.

### Third-Party Libraries
- **GimSharp** — Sony GIM texture format decoding library.
- **nQuant (Wu Color Quantizer)** — Color quantization algorithm for indexed texture encoding.

---

## 📄 License

This project is licensed under the MIT License - see the `LICENSE` file for details.
Made with ✨Gemini 3.6.