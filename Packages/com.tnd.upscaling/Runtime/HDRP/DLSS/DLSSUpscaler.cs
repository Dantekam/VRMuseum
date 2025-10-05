using UnityEngine.Rendering;
using TND.Upscaling.Framework;
#if UNITY_STANDALONE_WIN && TND_NVIDIA_MODULE_INSTALLED
using UnityEngine.NVIDIA;
#endif

namespace TND.Upscaling.HDRP.DLSS
{
    /// <summary>
    /// Basic passthrough DLSS upscaler based on Unity's own NVUnityPlugin code.
    /// This is included in HDRP because TND Upscaling works by overriding HDRP's built-in DLSS implementation and replacing it with a framework.
    /// This effectively removes DLSS from HDRP, so with this upscaler we add it back in.
    /// </summary>
    public class DLSSUpscaler: UpscalerBase<DLSSUpscalerSettings>
    {
        public DLSSUpscaler(DLSSUpscalerSettings settings)
            : base(settings)
        {
        }
        
#if UNITY_STANDALONE_WIN && TND_NVIDIA_MODULE_INSTALLED
        private DLSSContext _context;

        public override bool Initialize(CommandBuffer commandBuffer, in UpscalerInitParams initParams)
        {
            if (GraphicsDevice.device == null)
            {
                if (GraphicsDevice.CreateGraphicsDevice() == null)
                    return false;
            }

            DLSSFeatureFlags flags = DLSSFeatureFlags.None;
            if (initParams.enableHDR) flags |= DLSSFeatureFlags.IsHDR;
            if (initParams.invertedDepth) flags |= DLSSFeatureFlags.DepthInverted;
            if (!initParams.highResMotionVectors) flags |= DLSSFeatureFlags.MVLowRes;
            if (initParams.jitteredMotionVectors) flags |= DLSSFeatureFlags.MVJittered;

            DLSSCommandInitializationData initData = new()
            {
                quality = DLSSQuality.MaximumQuality,
                inputRTWidth = (uint)initParams.maxRenderSize.x,
                inputRTHeight = (uint)initParams.maxRenderSize.y,
                outputRTWidth = (uint)initParams.upscaleSize.x,
                outputRTHeight = (uint)initParams.upscaleSize.y,
                featureFlags = flags,
            };

            _context = GraphicsDevice.device.CreateFeature(commandBuffer, initData);
            return _context != null;
        }

        public override void Destroy(CommandBuffer commandBuffer)
        {
            if (_context != null && GraphicsDevice.device != null)
            {
                GraphicsDevice.device.DestroyFeature(commandBuffer, _context);
                _context = null;
            }
            
            base.Destroy(commandBuffer);
        }

        public override void Dispatch(CommandBuffer commandBuffer, in UpscalerDispatchParams dispatchParams)
        {
            if (_context == null || GraphicsDevice.device == null)
                return;

            ref var executeData = ref _context.executeData;
            executeData.reset = dispatchParams.resetHistory ? 1 : 0;
            executeData.sharpness = dispatchParams.sharpness;
            executeData.mvScaleX = dispatchParams.motionVectorScale.x;
            executeData.mvScaleY = dispatchParams.motionVectorScale.y;
            executeData.jitterOffsetX = dispatchParams.jitterOffset.x;
            executeData.jitterOffsetY = dispatchParams.jitterOffset.y;
            executeData.preExposure = dispatchParams.preExposure;
            executeData.subrectOffsetX = 0u;
            executeData.subrectOffsetY = 0u;
            executeData.subrectWidth = (uint)dispatchParams.renderSize.x;
            executeData.subrectHeight = (uint)dispatchParams.renderSize.y;
            executeData.invertXAxis = 0u;
            executeData.invertYAxis = 1u;

            DLSSTextureTable textureTable = new()
            {
                colorInput = dispatchParams.inputColor.GetTexture(commandBuffer),
                depth = dispatchParams.inputDepth.GetTexture(commandBuffer),
                motionVectors = dispatchParams.inputMotionVectors.GetTexture(commandBuffer),
                exposureTexture = dispatchParams.inputExposure.GetTexture(commandBuffer),
                biasColorMask = dispatchParams.inputReactiveMask.GetTexture(commandBuffer),
                colorOutput = dispatchParams.outputColor.GetTexture(commandBuffer),
            };
            
            GraphicsDevice.device.ExecuteDLSS(commandBuffer, _context, textureTable);
        }
#else
        public override bool Initialize(CommandBuffer commandBuffer, in UpscalerInitParams initParams) => false;
        public override void Dispatch(CommandBuffer commandBuffer, in UpscalerDispatchParams dispatchParams) { }
#endif
    }
}
