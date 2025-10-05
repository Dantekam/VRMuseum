using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#pragma warning disable 0672    // Disable obsolete warnings
#pragma warning disable 0618    // Disable obsolete warnings
#endif

namespace TND.Upscaling.Framework.URP
{
    public class OpaqueCopyPass : ScriptableRenderPass
    {
        private const string PassName = "[Upscaler] Opaque-Only Copy";

        private RTHandle _opaqueOnlyColor;
        public Texture Texture => _opaqueOnlyColor;

        public OpaqueCopyPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public void Dispose()
        {
            if (_opaqueOnlyColor != null)
            {
                _opaqueOnlyColor.Release();
                _opaqueOnlyColor = null;
            }
        }

#if UNITY_2023_3_OR_NEWER
        private class PassData
        {
            public TextureHandle activeColorTexture;
            public RTHandle opaqueOnlyColor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Setting up the render pass in RenderGraph
            using (var builder = renderGraph.AddUnsafePass<PassData>(PassName, out var passData))
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                CreateResources(ref cameraData.cameraTargetDescriptor);

                passData.activeColorTexture = resourceData.activeColorTexture;
                passData.opaqueOnlyColor = _opaqueOnlyColor;

                builder.UseTexture(passData.activeColorTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    unsafeCmd.CopyTexture(data.activeColorTexture, data.opaqueOnlyColor);
                });
            }
        }
#endif

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            CreateResources(ref renderingData.cameraData.cameraTargetDescriptor);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            ReleaseResources();
        }

        private void CreateResources(ref RenderTextureDescriptor cameraTargetDescriptor)
        {
            var descriptor = cameraTargetDescriptor;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            UpscalingHelpers.AllocateRTHandle(ref _opaqueOnlyColor, descriptor, FilterMode.Point, TextureWrapMode.Clamp, "_CameraOpaqueOnlyColor");
        }

        private void ReleaseResources()
        {
            UpscalingHelpers.ReleaseRTHandle(ref _opaqueOnlyColor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(PassName);

            var cameraColorTarget =
#if UNITY_2022_1_OR_NEWER
                renderingData.cameraData.renderer.cameraColorTargetHandle;
#else
                renderingData.cameraData.renderer.cameraColorTarget;
#endif
            cmd.CopyTexture(cameraColorTarget, _opaqueOnlyColor);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
