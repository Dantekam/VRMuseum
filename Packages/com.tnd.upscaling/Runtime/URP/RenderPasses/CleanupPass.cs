using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#pragma warning disable 0672    // Disable obsolete warnings
#endif

namespace TND.Upscaling.Framework.URP
{
    public class CleanupPass : ScriptableRenderPass
    {
        private Vector2Int _currentRenderSize;
        private Vector2Int _currentDisplaySize;

        private IntPtr _currentCameraDataPtr;
        
        public CleanupPass()
        {
            renderPassEvent = RenderPassEvent.AfterRendering + 100;
        }
        
        public virtual bool Setup(in Vector2Int renderSize, in Vector2Int displaySize)
        {
            _currentRenderSize = renderSize;
            _currentDisplaySize = displaySize;
            return true;
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
#if UNITY_2022_2_OR_NEWER
            _currentCameraDataPtr = UpscalingHelpers.GetPtr(ref renderingData.cameraData);
#endif
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
#if UNITY_2022_2_OR_NEWER
            if (_currentCameraDataPtr != IntPtr.Zero)
            {
                // Reset camera target descriptor back to render size, so that subsequent stages in the URP render pipeline run with the correct resolution values
                ref var cameraData = ref UpscalingHelpers.AsRef<CameraData>(_currentCameraDataPtr);
                cameraData.cameraTargetDescriptor.width = _currentRenderSize.x;
                cameraData.cameraTargetDescriptor.height = _currentRenderSize.y;
            }
#endif
        }

#if UNITY_2023_3_OR_NEWER
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        { 
            // Noop, this pass doesn't actually render anything
        }
#endif
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Noop, this pass doesn't actually render anything
        }
    }
}
