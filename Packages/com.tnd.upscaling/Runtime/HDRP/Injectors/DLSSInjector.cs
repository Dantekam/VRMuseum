using System;
using TND.Upscaling.Framework;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.HighDefinition.NVIDIA
{
    /// <summary>
    /// These classes and structs are the heart of what allows custom upscaling to work in HDRP without needing source modifications.
    /// 
    /// We make use of a classic C# namespace prioritization trick here:
    /// The DLSSPass in HDRP addresses Unity's DLSS plugin as `NVIDIA.NVUnityPlugin`; normally this refers to a class in the UnityEngine.NVIDIA namespace.
    /// However, by placing a class with the same name in a namespace 'closer' to where DLSSPass lives in the namespace hierarchy, we can make the C# compiler
    /// prioritize our class over Unity's own plugin class. That is what we're doing here by having an `NVUnityPlugin` class in the UnityEngine.Rendering.NVIDIA namespace.
    ///
    /// The rest of this file mimics the interfaces of all the classes and structs that DLSSPass expects to find in the NVIDIA namespace, but replaces their implementations
    /// with a custom upscaler plugin system. This allows us to inject different upscaling techniques and switch them on-the-fly without HDRP ever being aware that this is happening.
    /// </summary>
    public static class NVUnityPlugin
    {
        public static bool Load()
        {
            return IsLoaded();
        }

        public static bool IsLoaded()
        {
            return UpscalerPluginRegistry.AnySupported();
        }
        
        internal static void Unload()
        {
            UpscalerPluginRegistry.Cleanup();
        }
    }

    public class GraphicsDevice: UpscalerGraphicsDeviceBase<GraphicsDevice, DLSSContext>
    {
        public static GraphicsDevice device => s_graphicsDevice;

        public static uint version => 0x04;    // This needs to match what HDRP's DLSSPass expects

        public static GraphicsDevice CreateGraphicsDevice(string projectID) => CreateGraphicsDevice();

        public bool IsFeatureAvailable(GraphicsDeviceFeature feature) => feature == GraphicsDeviceFeature.DLSS;

        public DLSSContext CreateFeature(CommandBuffer cmd, in DLSSCommandInitializationData initSettings)
        {
            var context = new DLSSContext();
            context.Initialize(cmd, initSettings);
            _contexts.Add(context);
            return context;
        }

        public void ExecuteDLSS(CommandBuffer cmd, DLSSContext dlssContext, in DLSSTextureTable textures)
        {
            if (dlssContext == null)
                return;
            
            dlssContext.Execute(cmd, textures);
        }

        public void GetOptimalSettings(uint displayWidth, uint displayHeight, DLSSQuality qualityMode, out OptimalDLSSSettingsData optimalSettings)
        {
            GetRenderResolutionFromQualityMode(qualityMode, displayWidth, displayHeight, out optimalSettings.outRenderWidth, out optimalSettings.outRenderHeight);
            optimalSettings.maxWidth = optimalSettings.outRenderWidth;
            optimalSettings.maxHeight = optimalSettings.outRenderHeight;
            optimalSettings.minWidth = (uint)Mathf.CeilToInt(optimalSettings.outRenderWidth * 0.7f);
            optimalSettings.minHeight = (uint)Mathf.CeilToInt(optimalSettings.outRenderHeight * 0.7f);
            optimalSettings.sharpness = 0.5f;
        }
        
        private static void GetRenderResolutionFromQualityMode(DLSSQuality qualityMode, uint displayWidth, uint displayHeight, out uint renderWidth, out uint renderHeight)
        {
            float ratio = GetUpscaleRatioFromQualityMode(qualityMode);
            renderWidth = (uint)Mathf.RoundToInt(displayWidth / ratio);
            renderHeight = (uint)Mathf.RoundToInt(displayHeight / ratio);
        }
        
        private static float GetUpscaleRatioFromQualityMode(DLSSQuality qualityMode)
        {
            switch (qualityMode)
            {
                case DLSSQuality.MaximumQuality:
                    return 1.5f;
                case DLSSQuality.Balanced:
                    return 1.7f;
                case DLSSQuality.MaximumPerformance:
                    return 2.0f;
                case DLSSQuality.UltraPerformance:
                    return 3.0f;
                default:
                    return 1.0f;
            }
        }
    }

    public class DLSSContext: UpscalerContext
    {
        private DLSSCommandInitializationData _initData;
        public ref readonly DLSSCommandInitializationData initData => ref _initData;

        private DLSSCommandExecutionData _executeData;
        public ref DLSSCommandExecutionData executeData => ref _executeData;

        private Camera _camera;
        private bool _singlePassXR;
        
        private static int s_prevFrameCount;
        private static int s_viewId;

        internal bool Initialize(CommandBuffer cmd, in DLSSCommandInitializationData initSettings)
        {
            _camera = InjectorUtils.FindCurrentCamera();
            if (_camera == null)
            {
                return false;
            }

            _initData = initSettings;
            _executeData = new DLSSCommandExecutionData();
            _singlePassXR = IsUsingSinglePassXR(_camera);
            
            UpscalerInitParams initParams = new()
            {
                camera = _camera,
                maxRenderSize = new Vector2Int((int)initSettings.inputRTWidth, (int)initSettings.inputRTHeight),
                upscaleSize = new Vector2Int((int)initSettings.outputRTWidth, (int)initSettings.outputRTHeight),
                // HDRP creates single-texture copies of the input buffers when single-pass instanced XR is active.
                // Otherwise, we get texture arrays on platforms where it is allowed.
                useTextureArrays = TextureXR.useTexArray && !_singlePassXR,
                numTextureSlices = _singlePassXR ? 1 : TextureXR.slices,
                enableHDR = initSettings.GetFlag(DLSSFeatureFlags.IsHDR),
                invertedDepth = initSettings.GetFlag(DLSSFeatureFlags.DepthInverted),
                highResMotionVectors = !initSettings.GetFlag(DLSSFeatureFlags.MVLowRes),
                jitteredMotionVectors = initSettings.GetFlag(DLSSFeatureFlags.MVJittered),
            };

            return Initialize(cmd, initParams);
        }

        private static readonly int TempOpaqueId = Shader.PropertyToID("_TempOpaque");

        internal void Execute(CommandBuffer cmd, in DLSSTextureTable textures)
        {
            // Camera might become invalid on scene change, either refresh it or abort
            if (_camera == null)
            {
                _camera = InjectorUtils.FindCurrentCamera();
                if (_camera == null)
                {
                    return;
                }
            }

            // This is rather awkward, but we don't receive a view ID as input, and we can't query HDCamera here to check which view is currently rendering
            if (Time.frameCount != s_prevFrameCount || _camera.targetTexture != null)
            {
                s_viewId = 0;
                s_prevFrameCount = Time.frameCount;
            }
            
            Texture opaqueOnly = UpscalerController?.OpaqueOnlyTexture;
            bool tempOpaqueCopy = _singlePassXR && opaqueOnly != null && opaqueOnly.dimension == TextureDimension.Tex2DArray;
            if (tempOpaqueCopy)
            {
                // Opaque-only copy is a texture array but in single-pass XR all input textures need to be a regular texture
                cmd.GetTemporaryRT(TempOpaqueId, opaqueOnly.width, opaqueOnly.height, 0, opaqueOnly.filterMode, opaqueOnly.graphicsFormat, 1, false);
                cmd.CopyTexture(opaqueOnly, s_viewId, TempOpaqueId, 0);
            }
            
            UpscalerDispatchParams dispatchParams = new()
            {
                // Camera's projection matrix remains non-jittered, HDRP handles jitter through its ViewConstants (see HDCamera.UpdateAllViewConstants)
                // TODO: get multi-view projection matrix when XR is active (HDCamera.m_XRViewConstants)
                nonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, SystemInfo.graphicsUVStartsAtTop && _camera.targetTexture != null),
                
                // HDRP creates single-texture copies of the input buffers when single-pass instanced XR is active.
                // Multi-pass XR renders only a single view per pass, meaning the view ID is always 0.
                viewIndex = 0,
                
                inputColor = new TextureRef(textures.colorInput),
                inputDepth = new TextureRef(textures.depth),
                inputMotionVectors = new TextureRef(textures.motionVectors),
                inputExposure = new TextureRef(textures.exposureTexture),
                inputReactiveMask = new TextureRef(textures.biasColorMask),
                inputOpaqueOnly = tempOpaqueCopy ? new TextureRef(TempOpaqueId, new(), null) : new TextureRef(opaqueOnly), 
                outputColor = new TextureRef(textures.colorOutput),
                
                renderSize = new Vector2Int((int)_executeData.subrectWidth, (int)_executeData.subrectHeight),
                motionVectorScale = new Vector2(_executeData.mvScaleX, _executeData.mvScaleY),
                jitterOffset = new Vector2(_executeData.jitterOffsetX, _executeData.jitterOffsetY),
                preExposure = _executeData.preExposure,
                resetHistory = _executeData.reset != 0,
            };

            Execute(cmd, ref dispatchParams);

            if (tempOpaqueCopy)
            {
                cmd.ReleaseTemporaryRT(TempOpaqueId);
            }
            
            // This is highly suspect, but assuming each camera draws its views in order, this should ensure we always use the correct view even when multiple cameras are active 
            s_viewId = (s_viewId + 1) % TextureXR.slices;
        }

        private static bool IsUsingSinglePassXR(Camera camera)
        {
#if UNITY_2022_1_OR_NEWER && ENABLE_VR && ENABLE_XR_MODULE
            if (!XRSystem.displayActive || !XRSystem.singlePassAllowed)
                return false;
            
            var display = XRSystem.GetActiveDisplay();
            if (display == null)
                return false;
            
            display.GetRenderPass(0, out var renderPass);
            if (renderPass.renderTargetDesc.dimension != TextureDimension.Tex2DArray)
                return false;
            
            if (renderPass.GetRenderParameterCount() != 2 || renderPass.renderTargetDesc.volumeDepth != 2)
                return false;
            
            renderPass.GetRenderParameter(camera, 0, out var renderParam0);
            renderPass.GetRenderParameter(camera, 1, out var renderParam1);

            if (renderParam0.textureArraySlice != 0 || renderParam1.textureArraySlice != 1)
                return false;

            if (renderParam0.viewport != renderParam1.viewport)
                return false;

            return true;
#else
            return false;
#endif
        }
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

    public struct DLSSTextureTable
    {
        public Texture colorInput;
        public Texture colorOutput;
        public Texture depth;
        public Texture motionVectors;
        public Texture transparencyMask;
        public Texture exposureTexture;
        public Texture biasColorMask;
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

    public struct OptimalDLSSSettingsData
    {
        public uint outRenderWidth;
        public uint outRenderHeight;
        public float sharpness;
        public uint maxWidth;
        public uint maxHeight;
        public uint minWidth;
        public uint minHeight;
    }
}
