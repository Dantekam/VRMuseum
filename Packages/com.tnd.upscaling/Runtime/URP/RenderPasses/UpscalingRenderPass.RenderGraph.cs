#if UNITY_2023_3_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TND.Upscaling.Framework.URP
{
    public partial class UpscalingRenderPass
    {
        private static readonly int ScreenSizePropertyID = Shader.PropertyToID("_ScreenSize");
        
        private readonly BaseRenderFunc<PassData, UnsafeGraphContext> _executePassDelegate;

        private class PassData
        {
            public UniversalCameraData cameraData;
            public TextureHandle activeColorTexture;
            
            public TextureHandle colorBuffer;
            public TextureHandle depthBuffer;
            public TextureHandle motionVectorBuffer;
            public TextureHandle output;
            public RendererListHandle rendererListHandle;
            public int viewCount;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            OnCameraSetupRenderGraph(ref cameraData);

            RenderTextureDescriptor upscaledDesc = CreateResources(cameraData.cameraTargetDescriptor, _currentController.DisplaySize);
            UpscalingHelpers.SetupUpscaledColorHandles(cameraData.renderer, upscaledDesc);

            using (var builder = renderGraph.AddUnsafePass<PassData>(PassName, out var passData))
            {
                // TODO: isn't this creating a duplicate upscaled output texture?
                TextureHandle rtHandle = UniversalRenderer.CreateRenderGraphTexture(
                     renderGraph,
                     upscaledDesc,
                     "_CameraUpscaledColor",
                     false
                 );

                passData.cameraData = cameraData;
                passData.activeColorTexture = resourceData.activeColorTexture;
                
                passData.colorBuffer = resourceData.cameraColor;
                passData.depthBuffer = resourceData.cameraDepth;
                passData.motionVectorBuffer = resourceData.motionVectorColor;
                passData.output = rtHandle;
                passData.viewCount = cameraData.xr.enabled ? cameraData.xr.viewCount : 1;
                
                builder.UseTexture(resourceData.cameraColor, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepth, AccessFlags.Read);
                builder.UseTexture(resourceData.motionVectorColor, AccessFlags.Read);
                builder.UseTexture(rtHandle, AccessFlags.ReadWrite);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Write);
                builder.AllowPassCulling(false);

                resourceData.cameraColor = rtHandle;

                builder.SetRenderFunc(_executePassDelegate);
            }

            if (cameraData.postProcessEnabled)
            {
                // Inform the post-processing passes of the new render resolution
                UpscalingHelpers.UpdatePostProcessDescriptors(cameraData.renderer, upscaledDesc);
                UpdateCameraResolution(renderGraph, cameraData, upscaledDesc);
                UpscalingHelpers.SetCameraDepthTexture(resourceData, renderGraph.ImportTexture(_upsampledDepth));
            }
        }

        private void ExecutePass(PassData passData, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            // Execute Upscaler
            for (int view = 0; view < passData.viewCount; ++view)
            {
                DispatchUpscaler(cmd, UpscalingHelpers.GetGPUProjectionMatrixNoJitter(passData.cameraData, passData.activeColorTexture, view), 
                    passData.cameraData.xr.multipassId * passData.viewCount + view,
                    new TextureRef(passData.colorBuffer),
                    new TextureRef(passData.depthBuffer),
                    new TextureRef(passData.motionVectorBuffer),
                    new TextureRef(passData.output));
            }

            // Prepare the Depth output for the next render pass
            UpsampleDepth(cmd, passData.cameraData.renderer, _currentController.DisplaySize, passData.cameraData.postProcessEnabled, passData.depthBuffer, passData.viewCount);
        }
        
        private class UpdateCameraResolutionPassData
        {
            public Vector2Int newCameraTargetSize;
        }
        
        // This is originally part of "UpdateCameraResolution" of the PostProcessPassRenderGraph internal function, so we had to move it
        private static void UpdateCameraResolution(RenderGraph renderGraph, UniversalCameraData cameraData, in RenderTextureDescriptor upscaledDesc)
        {
            cameraData.cameraTargetDescriptor.width = upscaledDesc.width;
            cameraData.cameraTargetDescriptor.height = upscaledDesc.height;

            // Update the shader constants to reflect the new camera resolution
            using (var builder = renderGraph.AddUnsafePass<UpdateCameraResolutionPassData>("[Upscaler] Update Camera Resolution", out var passData))
            {
                passData.newCameraTargetSize = new Vector2Int(upscaledDesc.width, upscaledDesc.height);

                // This pass only modifies shader constants so we need to set some special flags to ensure it isn't culled or optimized away
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (UpdateCameraResolutionPassData data, UnsafeGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalVector(
                        ScreenSizePropertyID,
                        new Vector4(
                            data.newCameraTargetSize.x,
                            data.newCameraTargetSize.y,
                            1.0f / data.newCameraTargetSize.x,
                            1.0f / data.newCameraTargetSize.y
                        )
                    );
                });
            }
        }
    }
}

#endif
