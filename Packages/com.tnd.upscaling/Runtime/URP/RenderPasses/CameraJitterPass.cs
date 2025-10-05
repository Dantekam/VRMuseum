using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#pragma warning disable 0672    // Disable obsolete warnings
#endif

namespace TND.Upscaling.Framework.URP
{
    public class CameraJitterPass : ScriptableRenderPass
    {
        protected UpscalerController_URP _currentController;
        protected Vector2 _currentJitterOffset;
        protected Matrix4x4 _inverseJitterMatrix;

        public CameraJitterPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
        }
        
        public virtual bool Setup(UpscalerController_URP controller)
        {
            _currentController = controller;
            return true;
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
#if UNITY_2022_2_OR_NEWER
            ref CameraData cameraData = ref renderingData.cameraData;
            Matrix4x4 jitterMatrix = SetupJitter(cameraData.camera.pixelWidth);
            UpscalingHelpers.SetCameraJitterMatrix(ref cameraData, jitterMatrix);
#endif
        }

#if UNITY_2023_3_OR_NEWER
        protected void OnCameraSetupRenderGraph(ref UniversalCameraData data)
        {
            ref UniversalCameraData cameraData = ref data;
            Matrix4x4 jitterMatrix = SetupJitter(cameraData.camera.pixelWidth);
            UpscalingHelpers.SetCameraJitterMatrixRenderGraph(ref cameraData, jitterMatrix);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        { 
            // Noop, this pass doesn't actually render anything
        }
#endif

        public Matrix4x4 SetupJitter(int displayWidth)
        {
            Vector2Int renderSize = _currentController.ScaledRenderSize;
            IUpscalerPlugin upscalerPlugin = _currentController.ActiveUpscalerPlugin;
            IUpscaler upscaler = _currentController.ActiveUpscaler;
            
            if (upscalerPlugin != null && upscalerPlugin.IsTemporalUpscaler)
            {
                if (upscaler != null)
                {
                    // Allow upscalers to provide a custom jitter pattern
                    _currentJitterOffset = upscaler.GetJitterOffset(Time.frameCount, renderSize.x, displayWidth);
                }
                else
                {
                    int jitterPhaseCount = GetJitterPhaseCount(renderSize.x, displayWidth);
                    GetJitterOffset(out float jitterX, out float jitterY, Time.frameCount, jitterPhaseCount);
                    _currentJitterOffset = new Vector2(jitterX, jitterY);
                }
            }
            else
            {
                // Spatial upscalers don't need any camera jitter, make this a noop
                _currentJitterOffset = Vector2.zero;
            }

            Vector3 jitterVector = CreateJitterVector(_currentJitterOffset, renderSize);
            _inverseJitterMatrix = Matrix4x4.Translate(-jitterVector);
            return Matrix4x4.Translate(jitterVector);
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Noop, this pass doesn't actually render anything
        }
        
        private static int GetJitterPhaseCount(int renderWidth, int displayWidth)
        {
            const float basePhaseCount = 8.0f;
            int jitterPhaseCount = (int)(basePhaseCount * Mathf.Pow((float)displayWidth / renderWidth, 2.0f));
            return jitterPhaseCount;
        }

        private static void GetJitterOffset(out float outX, out float outY, int index, int phaseCount)
        {
            outX = HaltonSequence.Get((index % phaseCount) + 1, 2) - 0.5f;
            outY = HaltonSequence.Get((index % phaseCount) + 1, 3) - 0.5f;
        }
        
        private static Vector3 CreateJitterVector(in Vector2 jitterOffset, in Vector2Int renderSize)
        {
            float jitterX = 2.0f * jitterOffset.x / renderSize.x;
            float jitterY = 2.0f * jitterOffset.y / renderSize.y;
            return new Vector3(jitterX, jitterY, 0.0f);
        }
    }
}
