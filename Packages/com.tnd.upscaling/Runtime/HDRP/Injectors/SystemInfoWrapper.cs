#pragma warning disable

using UnityEngine.Experimental.Rendering;
using USI = UnityEngine.SystemInfo;

namespace UnityEngine.Rendering.HighDefinition
{
    /// <summary>
    /// For DLSS injection to work properly, there is one annoying aspect that we have to fix: HDRP checks the graphicsDeviceVendor string
    /// to see if the word "nvidia" is in there, in order to allow DLSS to be enabled and activated.
    ///
    /// Because we intend to override DLSS in HDRP and inject our own upscalers that may work on any graphics device, we have to trick
    /// HDRP into allowing 'DLSS' to be enabled. For this purpose we also inject our own SystemInfo override into HDRP, which implements
    /// everything HDRP needs but extends the graphicsDeviceVendor string to make HDRP think we're always running on an Nvidia GPU.
    /// </summary>
    public static class SystemInfo
    {
        public static string graphicsDeviceVendor => USI.graphicsDeviceVendor + " nvidia";  // Trick HDRP into allowing DLSS to be enabled
        public static int graphicsDeviceVendorID => USI.graphicsDeviceVendorID; // This is used for other purposes, we don't need to spoof Nvidia here
        public static string graphicsDeviceName => USI.graphicsDeviceName;
        public static GraphicsDeviceType graphicsDeviceType => USI.graphicsDeviceType;
        public static bool graphicsUVStartsAtTop => USI.graphicsUVStartsAtTop;
        public static bool usesReversedZBuffer => USI.usesReversedZBuffer;
        public static bool supportsComputeShaders => USI.supportsComputeShaders;
        public static bool supportsAsyncCompute => USI.supportsAsyncCompute;
        public static bool supportsRenderTargetArrayIndexFromVertexShader => USI.supportsRenderTargetArrayIndexFromVertexShader;
        public static bool supportsRayTracing => USI.supportsRayTracing;
        public static string operatingSystem => USI.operatingSystem;
        public static OperatingSystemFamily operatingSystemFamily => USI.operatingSystemFamily;
        public static HDRDisplaySupportFlags hdrDisplaySupportFlags => USI.hdrDisplaySupportFlags;
        public static CopyTextureSupport copyTextureSupport => USI.copyTextureSupport;
        public static bool supportsMultiview => USI.supportsMultiview;
        public static bool IsFormatSupported(GraphicsFormat format, FormatUsage usage) => USI.IsFormatSupported(format, usage);
#if UNITY_2023_2_OR_NEWER
        public static bool IsFormatSupported(GraphicsFormat format, GraphicsFormatUsage usage) => USI.IsFormatSupported(format, usage);
#endif
    }
}
