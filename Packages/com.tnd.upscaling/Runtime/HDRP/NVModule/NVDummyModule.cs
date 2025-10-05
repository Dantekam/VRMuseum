#if !UNITY_STANDALONE_WIN || !TND_NVIDIA_MODULE_INSTALLED
using System;
using System.Collections.Generic;

namespace UnityEngine.NVIDIA
{
    /// <summary>
    /// This entire file is a dummy implementation of the DLSS classes from the NVIDIA Unity Plugin module.
    /// These classes and methods are just empty stubs; they don't actually do anything.
    /// They're only here to allow the HDRP code to compile on platforms where the real NVIDIA Unity Plugin is not supported.
    /// </summary>
    public static class NVUnityPlugin
    {
        public static bool IsLoaded() => true;
    }
    
    public class GraphicsDevice
    {
        private static readonly GraphicsDevice Device = new();
        public static GraphicsDevice device => Device;

        public bool IsFeatureAvailable(GraphicsDeviceFeature feature) => true;
        
        public GraphicsDeviceDebugView CreateDebugView() => new();
        public void UpdateDebugView(GraphicsDeviceDebugView debugView) { }
        public void DeleteDebugView(GraphicsDeviceDebugView debugView) { }
    }

    public class GraphicsDeviceDebugView
    {
        public readonly List<DLSSDebugFeatureInfos> dlssFeatureInfos = new();
        public int deviceVersion = 0x04;
        public uint ngxVersion = (3u << 16) | (1u << 8);
    }
    
    public struct DLSSDebugFeatureInfos
    {
        public bool validFeature;
        public int featureSlot;
        public DLSSCommandInitializationData initData;
        public DLSSCommandExecutionData execData;
    }
    
    public struct DLSSCommandInitializationData
    {
        public uint inputRTWidth;
        public uint inputRTHeight;
        public uint outputRTWidth;
        public uint outputRTHeight;
        public DLSSQuality quality;
        public DLSSFeatureFlags flags;
        public uint featureSlot;
        
        public readonly bool GetFlag(DLSSFeatureFlags flag)
        {
            return (flags & flag) == flag;
        }

        public void SetFlag(DLSSFeatureFlags flag, bool value)
        {
            if (value)
                flags |= flag;
            else
                flags &= ~flag;
        }
    }
    
    public struct DLSSCommandExecutionData
    {
        public int reset;
        public float sharpness;
        public float mvScaleX;
        public float mvScaleY;
        public float jitterOffsetX;
        public float jitterOffsetY;
        public float preExposure;
        public uint subrectOffsetX;
        public uint subrectOffsetY;
        public uint subrectWidth;
        public uint subrectHeight;
        public uint invertXAxis;
        public uint invertYAxis;
        public uint featureSlot;
    }
    
    [Flags]
    public enum DLSSFeatureFlags
    {
        None = 0,
        IsHDR = 1,
        MVLowRes = 2,
        MVJittered = 4,
        DepthInverted = 8,
        DoSharpening = 16,
    }

    public enum DLSSQuality
    {
        MaximumPerformance,
        Balanced,
        MaximumQuality,
        UltraPerformance,
    }

    public enum GraphicsDeviceFeature
    {
        DLSS,
    }
}
#endif
