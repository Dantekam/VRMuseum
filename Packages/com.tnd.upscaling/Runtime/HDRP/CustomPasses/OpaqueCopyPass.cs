using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace TND.Upscaling.Framework.HDRP
{
    public class OpaqueCopyPass : CustomPass
    {
        private RTHandle _opaqueCopy;

        public Texture OpaqueOnlyTexture => _opaqueCopy?.rt;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            name = "Opaque-Only Copy Pass";
            targetColorBuffer = TargetBuffer.Camera;
            targetDepthBuffer = TargetBuffer.None;

            CreateOpaqueOnlyTexture();
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (_opaqueCopy?.rt != null && GetColorBufferFormat() != _opaqueCopy.rt.graphicsFormat)
                CreateOpaqueOnlyTexture();

            Blitter.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, _opaqueCopy);
        }

        protected override void Cleanup()
        {
            if (_opaqueCopy != null)
            {
                _opaqueCopy.Release();
                _opaqueCopy = null;
            }
        }

        private void CreateOpaqueOnlyTexture()
        {
            _opaqueCopy?.Release();
            _opaqueCopy = RTHandles.Alloc(Vector2.one, colorFormat: GetColorBufferFormat(), dimension: TextureXR.dimension, slices: TextureXR.slices, useDynamicScale: true, name: "OpaqueOnlyCopy");
        }

        private static GraphicsFormat GetColorBufferFormat()
        {
            GraphicsFormat colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset hdRenderPipelineAsset)
            {
                colorFormat = (GraphicsFormat)hdRenderPipelineAsset.currentPlatformRenderPipelineSettings.postProcessSettings.bufferFormat;
            }

            return colorFormat;
        }
    }
}
