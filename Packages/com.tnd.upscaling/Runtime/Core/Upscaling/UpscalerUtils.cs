using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework
{
    public static class UpscalerUtils
    {
        public static bool TryLoadMaterial(string shaderResourceName, ref Material material, ref Shader shader)
        {
            if (material != null)
                return true;

            shader = Resources.Load<Shader>(shaderResourceName);
            if (shader == null)
                return false;

            material = new Material(shader);
            return true;
        }

        public static void UnloadMaterial(ref Material material, ref Shader shader)
        {
            if (material != null)
            {
                DestroyObject(material);
                material = null;
            }

            if (shader != null)
            {
                Resources.UnloadAsset(shader);
                shader = null;
            }
        }
        
        public static void RunWithCommandBuffer(Action<CommandBuffer> action)
        {
            using CommandBuffer cmd = new();
            action(cmd);
            Graphics.ExecuteCommandBuffer(cmd);
        }
        
        public static ComputeBuffer CreateComputeBuffer<TElem>(string name, int count)
        {
            return new ComputeBuffer(count, Marshal.SizeOf<TElem>());
        }

        public static Texture2D CreateLookupTexture(string name, GraphicsFormat format, Color data)
        {
            var tex = new Texture2D(1, 1, format, TextureCreationFlags.None) { name = name };
            tex.SetPixel(0, 0, data);
            tex.Apply();
            return tex;
        }

        public static Texture2DArray CreateLookupTextureArray(string name, GraphicsFormat format, int slices, Color data)
        {
            var tex = new Texture2DArray(1, 1, slices, format, TextureCreationFlags.None) { name = name };
            for (int i = 0; i < slices; ++i)
                tex.SetPixels(new[] { data }, i);
            
            tex.Apply();
            return tex;
        }
        
        public static Texture2D CreateLookupTexture<T>(string name, in Vector2Int size, GraphicsFormat format, T[] data)
        {
            var tex = new Texture2D(size.x, size.y, format, TextureCreationFlags.None) { name = name };
            tex.SetPixelData(data, 0);
            tex.Apply();
            return tex;
        }
        
        public static RenderTexture CreateRenderTexture(string name, in Vector2Int size, GraphicsFormat format, bool enableRandomWrite)
        {
            var rt = new RenderTexture(size.x, size.y, 0, format) { name = name, enableRandomWrite = enableRandomWrite };
            rt.Create();
            return rt;
        }
        
        public static RenderTexture CreateRenderTexture(string name, in Vector2Int size, RenderTextureFormat format, bool enableRandomWrite)
        {
            var rt = new RenderTexture(size.x, size.y, 0, format) { name = name, enableRandomWrite = enableRandomWrite };
            rt.Create();
            return rt;
        }

        public static RenderTexture CreateRenderTextureArray(string name, in Vector2Int size, int slices, GraphicsFormat format, bool enableRandomWrite)
        {
            var rt = new RenderTexture(size.x, size.y, 0, format) { name = name, enableRandomWrite = enableRandomWrite, dimension = TextureDimension.Tex2DArray, volumeDepth = slices };
            rt.Create();
            return rt;
        }
        
        public static RenderTexture CreateRenderTextureArray(string name, in Vector2Int size, int slices, RenderTextureFormat format, bool enableRandomWrite)
        {
            var rt = new RenderTexture(size.x, size.y, 0, format) { name = name, enableRandomWrite = enableRandomWrite, dimension = TextureDimension.Tex2DArray, volumeDepth = slices };
            rt.Create();
            return rt;
        }
        
        public static RenderTexture CreateRenderTextureMips(string name, in Vector2Int size, GraphicsFormat format, bool enableRandomWrite)
        {
            int mipCount = 1 + Mathf.FloorToInt(Mathf.Log(Math.Max(size.x, size.y), 2.0f));
            var rt = new RenderTexture(size.x, size.y, 0, format, mipCount) { name = name, enableRandomWrite = enableRandomWrite, useMipMap = true, autoGenerateMips = false };
            rt.Create();
            return rt;
        }
        
        public static RenderTexture CreateRenderTextureMips(string name, in Vector2Int size, RenderTextureFormat format, bool enableRandomWrite)
        {
            int mipCount = 1 + Mathf.FloorToInt(Mathf.Log(Math.Max(size.x, size.y), 2.0f));
            var rt = new RenderTexture(size.x, size.y, 0, format, mipCount) { name = name, enableRandomWrite = enableRandomWrite, useMipMap = true, autoGenerateMips = false };
            rt.Create();
            return rt;
        }
        
        public static void CreateRenderTextures(RenderTexture[] rtArray, string name, in Vector2Int size, GraphicsFormat format, bool enableRandomWrite)
        {
            for (int i = 0; i < rtArray.Length; ++i)
            {
                rtArray[i] = CreateRenderTexture($"{name}_{i + 1}", size, format, enableRandomWrite);
            }
        }
        
        public static void CreateRenderTextures(RenderTexture[] rtArray, string name, in Vector2Int size, RenderTextureFormat format, bool enableRandomWrite)
        {
            for (int i = 0; i < rtArray.Length; ++i)
            {
                rtArray[i] = CreateRenderTexture($"{name}_{i + 1}", size, format, enableRandomWrite);
            }
        }

        public static Texture CreateMatchingLookupTexture(this UpscalerInitParams initParams, string name, GraphicsFormat format, Color data)
        {
            return initParams.useTextureArrays
                ? CreateLookupTextureArray(name, format, initParams.numTextureSlices, data)
                : CreateLookupTexture(name, format, data);
        }
        
        public static RenderTexture CreateMatchingRenderTexture(this UpscalerInitParams initParams, string name, in Vector2Int size, GraphicsFormat format, bool enableRandomWrite)
        {
            return initParams.useTextureArrays
                ? CreateRenderTextureArray(name, size, initParams.numTextureSlices, format, enableRandomWrite)
                : CreateRenderTexture(name, size, format, enableRandomWrite);
        }

        public static RenderTexture CreateMatchingRenderTexture(this UpscalerInitParams initParams, string name, in Vector2Int size, RenderTextureFormat format, bool enableRandomWrite)
        {
            return initParams.useTextureArrays
                ? CreateRenderTextureArray(name, size, initParams.numTextureSlices, format, enableRandomWrite)
                : CreateRenderTexture(name, size, format, enableRandomWrite);
        }

        public static void GetMatchingTemporaryRT(this UpscalerInitParams initParams, CommandBuffer commandBuffer, int nameID, in Vector2Int size, GraphicsFormat format, bool enableRandomWrite)
        {
            if (initParams.useTextureArrays)
                commandBuffer.GetTemporaryRTArray(nameID, size.x, size.y, initParams.numTextureSlices, 0, FilterMode.Point, format, 1, enableRandomWrite);
            else
                commandBuffer.GetTemporaryRT(nameID, size.x, size.y, 0, FilterMode.Point, format, 1, enableRandomWrite);
        }
        
        public static void DestroyTexture(ref Texture texture)
        {
            if (texture == null)
                return;
            
            DestroyObject(texture);
            texture = null;
        }

        public static void DestroyRenderTexture(ref RenderTexture renderTexture)
        {
            if (renderTexture == null)
                return;
            
            renderTexture.Release();
            renderTexture = null;
        }
        
        public static void DestroyComputeBuffer(ref ComputeBuffer computeBuffer)
        {
            if (computeBuffer == null)
                return;
            
            computeBuffer.Release();
            computeBuffer = null;
        }

        public static void DestroyRenderTextures(RenderTexture[] rtArray)
        {
            for (int i = 0; i < rtArray.Length; ++i)
            {
                DestroyRenderTexture(ref rtArray[i]);
            }
        }
        
        public static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            
#if UNITY_EDITOR
            if (Application.isPlaying && !UnityEditor.EditorApplication.isPaused)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }

        private static readonly (GraphicsFormat, GraphicsFormat)[] GraphicsFormats =
        {
            (GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.R8G8B8A8_SRGB),  // ARGB32
            (GraphicsFormat.None, GraphicsFormat.None), // Depth
            (GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.R16G16B16A16_SFloat),   // ARGBHalf
            (GraphicsFormat.None, GraphicsFormat.None), // Shadowmap
            (GraphicsFormat.B5G6R5_UNormPack16, GraphicsFormat.B5G6R5_UNormPack16), // RGB565
            (GraphicsFormat.B4G4R4A4_UNormPack16, GraphicsFormat.B4G4R4A4_UNormPack16), // ARGB4444
            (GraphicsFormat.B5G5R5A1_UNormPack16, GraphicsFormat.B5G5R5A1_UNormPack16), // ARGB1555
            (GraphicsFormat.None, GraphicsFormat.None), // Default
            (GraphicsFormat.A2B10G10R10_UNormPack32, GraphicsFormat.A2B10G10R10_UNormPack32),   // ARGB2101010
            (GraphicsFormat.None, GraphicsFormat.None), // DefaultHDR
            (GraphicsFormat.R16G16B16A16_UNorm, GraphicsFormat.R16G16B16A16_UNorm),   // ARGB64
            (GraphicsFormat.R32G32B32A32_SFloat, GraphicsFormat.R32G32B32A32_SFloat),   // ARGBFloat
            (GraphicsFormat.R32G32_SFloat, GraphicsFormat.R32G32_SFloat),   // RGFloat
            (GraphicsFormat.R16G16_SFloat, GraphicsFormat.R16G16_SFloat),   // RGHalf
            (GraphicsFormat.R32_SFloat, GraphicsFormat.R32_SFloat),     // RFloat
            (GraphicsFormat.R16_SFloat, GraphicsFormat.R16_SFloat),     // RHalf
            (GraphicsFormat.R8_UNorm, GraphicsFormat.R8_UNorm),     // R8
            (GraphicsFormat.R32G32B32A32_SInt, GraphicsFormat.R32G32B32A32_SInt),   // ARGBInt
            (GraphicsFormat.R32G32_SInt, GraphicsFormat.R32G32_SInt),   // RGInt
            (GraphicsFormat.R32_SInt, GraphicsFormat.R32_SInt),     // RInt
            (GraphicsFormat.B8G8R8A8_UNorm, GraphicsFormat.B8G8R8A8_SRGB),  // BGRA32
            (GraphicsFormat.None, GraphicsFormat.None), // Undefined
            (GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormat.B10G11R11_UFloatPack32), // RGB111110Float
            (GraphicsFormat.R16G16_UNorm, GraphicsFormat.R16G16_UNorm),     // RG32
            (GraphicsFormat.R16G16B16A16_UInt, GraphicsFormat.R16G16B16A16_UInt),   // RGBAUShort
            (GraphicsFormat.R8G8_UNorm, GraphicsFormat.R8G8_UNorm), // RG16
            (GraphicsFormat.A10R10G10B10_XRUNormPack32, GraphicsFormat.A10R10G10B10_XRSRGBPack32),  // BGRA10101010_XR
            (GraphicsFormat.R10G10B10_XRUNormPack32, GraphicsFormat.R10G10B10_XRSRGBPack32),    // BGR101010_XR
            (GraphicsFormat.R16_UNorm, GraphicsFormat.R16_UNorm),   // R16
        };
        
        /// <summary>
        /// Reimplementation of GraphicsFormatUtility.GetGraphicsFormat, which is otherwise only available in Unity 2022.2+
        /// </summary>
        public static GraphicsFormat GetGraphicsFormat(RenderTextureFormat renderTextureFormat, bool isSRGB)
        {
            switch (renderTextureFormat)
            {
                case RenderTextureFormat.Default:
                    return SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
                case RenderTextureFormat.DefaultHDR:
                    return SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
                default:
                {
                    var result = GraphicsFormats[(int)renderTextureFormat];
                    return isSRGB ? result.Item2 : result.Item1;
                }
            }
        }
        
        private static readonly HashSet<GraphicsFormat> SRGBFormats = new()
        {
            GraphicsFormat.R8_SRGB, GraphicsFormat.R8G8_SRGB, GraphicsFormat.R8G8B8_SRGB,
            GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.B8G8R8_SRGB, GraphicsFormat.B8G8R8A8_SRGB,
            GraphicsFormat.A2R10G10B10_XRSRGBPack32, GraphicsFormat.R10G10B10_XRSRGBPack32, GraphicsFormat.A10R10G10B10_XRSRGBPack32,
            GraphicsFormat.RGBA_DXT1_SRGB, GraphicsFormat.RGBA_DXT3_SRGB, GraphicsFormat.RGBA_DXT5_SRGB,
            GraphicsFormat.RGBA_BC7_SRGB,
#pragma warning disable 0618    // Disable obsolete warnings
            GraphicsFormat.RGB_PVRTC_2Bpp_SRGB, GraphicsFormat.RGB_PVRTC_4Bpp_SRGB, GraphicsFormat.RGBA_PVRTC_2Bpp_SRGB, GraphicsFormat.RGBA_PVRTC_4Bpp_SRGB,
#pragma warning restore 0618
            GraphicsFormat.RGB_ETC2_SRGB, GraphicsFormat.RGB_A1_ETC2_SRGB, GraphicsFormat.RGBA_ETC2_SRGB,
            GraphicsFormat.RGBA_ASTC4X4_SRGB, GraphicsFormat.RGBA_ASTC5X5_SRGB, GraphicsFormat.RGBA_ASTC6X6_SRGB,
            GraphicsFormat.RGBA_ASTC8X8_SRGB, GraphicsFormat.RGBA_ASTC10X10_SRGB, GraphicsFormat.RGBA_ASTC12X12_SRGB,
        };

        /// <summary>
        /// Reimplementation of GraphicsFormatUtility.IsSRGBFormat, which is otherwise only be available in Unity 2022.2+
        /// </summary>
        public static bool IsSRGBFormat(GraphicsFormat graphicsFormat)
        {
            return SRGBFormats.Contains(graphicsFormat);
        }
    }
}
