using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
#pragma warning disable 0672    // Disable obsolete warnings
#pragma warning disable 0618    // Disable obsolete warnings
#endif

namespace TND.Upscaling.Framework.URP
{
    public class CustomReactiveMaskPass : ScriptableRenderPass
    {
        private const string PassName = "[Upscaler] Custom Reactive Mask Pass";

        private RTHandle _customReactiveMask;
        public Texture Texture => _customReactiveMask;

        private RenderStateBlock _renderStateBlock;
        private LayerMask _layerMask;

        private static readonly ShaderTagId[] ShaderTagIds =
        {
            new("SRPDefaultUnlit"),
            new("UniversalForward"),
            new("UniversalForwardOnly"),
            new("LightweightForward")
        };
        private static readonly List<ShaderTagId> ShaderTagIdsList = new(ShaderTagIds);

        public CustomReactiveMaskPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 2;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
        }

        public virtual bool Setup(LayerMask layerMask)
        {
            _layerMask = layerMask;
            return true;
        }

        public void Dispose()
        {
            if (_customReactiveMask != null)
            {
                _customReactiveMask.Release();
                _customReactiveMask = null;
            }
            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

#if UNITY_2023_3_OR_NEWER
        private class PassData
        {
            public RendererListHandle rendererListHandle;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddUnsafePass<PassData>(PassName, out var passData))
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();

                CreateResources(cameraData.cameraTargetDescriptor, cameraData.camera);

                var rendererListDesc = new RendererListDesc(ShaderTagIds, renderingData.cullResults, cameraData.camera)
                {
                    sortingCriteria = cameraData.defaultOpaqueSortFlags,
                    renderQueueRange = RenderQueueRange.all,
                    layerMask = _layerMask,
                };

                passData.rendererListHandle = renderGraph.CreateRendererList(rendererListDesc);

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.UseRendererList(passData.rendererListHandle);


                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }

        private void ExecutePass(PassData passData, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            CoreUtils.SetRenderTarget(
              cmd,
              _customReactiveMask,
              RenderBufferLoadAction.DontCare,
              RenderBufferStoreAction.Store,
              ClearFlag.Color,
              Color.clear);

            context.cmd.DrawRendererList(passData.rendererListHandle);
        }
#endif

        /// <summary>
        /// OnCameraSetup gets called very early on in the process of rendering a camera.
        /// This can be used to make significant changes to the camera's parameters that affect all render passes.
        /// </summary>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            CreateResources(renderingData.cameraData.cameraTargetDescriptor, renderingData.cameraData.camera);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            ReleaseResources();
        }

        /// <summary>
        /// Create output textures for the upscaler to write to
        /// </summary>
        private void CreateResources(RenderTextureDescriptor cameraTargetDescriptor, Camera camera)
        {
            var customReactiveDescriptor = cameraTargetDescriptor;
            customReactiveDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;    // TODO: maybe needs to be configurable, Unity DLSS plugin requires R8G8B8A8
            customReactiveDescriptor.depthStencilFormat = GraphicsFormat.None;
            customReactiveDescriptor.useMipMap = false;
            customReactiveDescriptor.autoGenerateMips = false;
            customReactiveDescriptor.bindMS = false;

            UpscalingHelpers.AllocateRTHandle(ref _customReactiveMask, customReactiveDescriptor, FilterMode.Point, TextureWrapMode.Clamp, "_CustomReactiveMask");
            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        private void ReleaseResources()
        {
            UpscalingHelpers.ReleaseRTHandle(ref _customReactiveMask);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(PassName);
            ref var cameraData = ref renderingData.cameraData;
            ExecuteCustomReactiveMask(context, ref renderingData);
        }

        private void ExecuteCustomReactiveMask(ScriptableRenderContext context, ref RenderingData renderingData)
        {
#if UNITY_2022_1_OR_NEWER
            ConfigureTarget(_customReactiveMask, renderingData.cameraData.renderer.cameraDepthTargetHandle);
#else
            ConfigureTarget(_customReactiveMask, renderingData.cameraData.renderer.cameraDepthTarget);
#endif

            ConfigureClear(ClearFlag.Color, Color.clear);
            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(
                ShaderTagIdsList,
                ref renderingData,
                sortingCriteria
            );

            FilteringSettings filteringSettings = new FilteringSettings(
                RenderQueueRange.all,
                _layerMask
            );
            _renderStateBlock.mask |= RenderStateMask.Depth;
            _renderStateBlock.depthState = new DepthState(false, CompareFunction.LessEqual);

            context.DrawRenderers(
                renderingData.cullResults,
                ref drawingSettings,
                ref filteringSettings,
                ref _renderStateBlock
            );
        }
    }
}
