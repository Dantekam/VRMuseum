using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TND.Upscaling.Framework.URP
{
    [DisallowMultipleRendererFeature("TND Upscaling")]
    public class UpscalingRendererFeature : ScriptableRendererFeature
    {
        public Shader copyDepthShader;
        public Shader upsampleDepthShader;

        private UniversalRenderPipelineAsset _renderPipelineAsset;

        private UpscalingRenderPass _upscalingRenderPass;
        private OpaqueCopyPass _opaqueCopyPass;
        private CustomReactiveMaskPass _customReactiveMaskPass;
        private CleanupPass _cleanupPass;
        private Material _copyDepthMaterial;
        private Material _upsampleDepthMaterial;
        
        public override void Create()
        {
#if UNITY_EDITOR
            if (copyDepthShader == null)
            {
                const string path = "Packages/com.unity.render-pipelines.universal/Shaders/Utils/CopyDepth.shader";
                copyDepthShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(path);
            }
            
            if (upsampleDepthShader == null)
            {
                const string path = "Packages/com.tnd.upscaling/Runtime/URP/Shaders/UpsampleDepth.shader";
                upsampleDepthShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(path);
            }
#endif
            
            name = "TND Upscaling";
            _upscalingRenderPass = new UpscalingRenderPass();
            _opaqueCopyPass = new OpaqueCopyPass();
            _customReactiveMaskPass = new CustomReactiveMaskPass();
            _cleanupPass = new CleanupPass();
            _copyDepthMaterial = new Material(copyDepthShader);
            _upsampleDepthMaterial = new Material(upsampleDepthShader);

            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.endCameraRendering += EndCameraRendering;
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= EndCameraRendering;

            if (_upsampleDepthMaterial != null)
            {
                CoreUtils.Destroy(_upsampleDepthMaterial);
                _upsampleDepthMaterial = null;
            }

            if (_opaqueCopyPass != null)
            {
                _opaqueCopyPass.Dispose();
                _opaqueCopyPass = null;
            }

            if (_customReactiveMaskPass != null)
            {
                _customReactiveMaskPass.Dispose();
                _customReactiveMaskPass = null;
            }

            if (_upscalingRenderPass != null)
            {
                _upscalingRenderPass.Dispose();
                _upscalingRenderPass = null;
            }
            
            UpscalingHelpers.CleanupRTHandles();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying || !isActive)
            {
                return;
            }

            if (_renderPipelineAsset == null)
            {
                return;
            }

            ref CameraData cameraData = ref renderingData.cameraData;
            if (!cameraData.resolveFinalTarget)
            {
                // Only perform upscaling on the final camera in a stack
                return;
            }
            
            Camera camera = cameraData.camera;
            if (camera.cameraType is not (CameraType.Game or CameraType.VR))
            {
                // Only perform upscaling on game cameras
                return;
            }
            
            // Ensure stale resources created by the upscaling passes don't stick around for too long
            UpscalingHelpers.PurgeUnusedRTHandles();

            if (!camera.TryGetComponent(out UpscalerController_URP controller) || !controller.enabled)
            {
                // Only perform upscaling when there's an active script attached to this camera
                return;
            }
            
            Vector2Int maxRenderSize = controller.MaxRenderSize;
            Vector2Int displaySize = controller.DisplaySize;

#if UNITY_2022_2_OR_NEWER
            // We force render scaling by modifying the camera's target texture descriptor, from which all of URP's render buffers are derived.
            // Doing this instead of using render scale ensures that the XR System allocates its output textures at full resolution, allowing for proper upscaling.
            if (_renderPipelineAsset != null)
            {
                // Apply render scale from the URP asset settings
                float renderScale = _renderPipelineAsset.renderScale;
                bool disableRenderScale = Mathf.Abs(1.0f - renderScale) < 0.05f;
                renderScale = disableRenderScale ? 1.0f : renderScale;
                maxRenderSize.x = Mathf.Max(1, (int)(maxRenderSize.x * renderScale));
                maxRenderSize.y = Mathf.Max(1, (int)(maxRenderSize.y * renderScale));
            }
            cameraData.cameraTargetDescriptor.width = maxRenderSize.x;
            cameraData.cameraTargetDescriptor.height = maxRenderSize.y;
#endif

            OpaqueCopyPass opaqueOnlySource = null;
            if (controller.EnableOpaqueOnlyCopy)
            {
                renderer.EnqueuePass(_opaqueCopyPass);
                opaqueOnlySource = _opaqueCopyPass;
            }

            if (controller.EnableCustomReactiveMask && _customReactiveMaskPass.Setup(controller.customReactiveMaskLayer))
            {
                renderer.EnqueuePass(_customReactiveMaskPass);
            }

            if (_upscalingRenderPass.Setup(_renderPipelineAsset, controller, _copyDepthMaterial, _upsampleDepthMaterial, opaqueOnlySource))
            {
                renderer.EnqueuePass(_upscalingRenderPass);
            }

            if (_cleanupPass.Setup(maxRenderSize, displaySize))
            {
                renderer.EnqueuePass(_cleanupPass);
            }
        }

        private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!Application.isPlaying || !isActive)
            {
                return;
            }

            if (_renderPipelineAsset == null)
            {
                _renderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (_renderPipelineAsset == null)
                {
                    return;
                }
            }

#if !UNITY_2022_2_OR_NEWER
            if (!camera.TryGetComponent<UpscalerController_URP>(out var controller) || !controller.enabled)
            {
                return;
            }

            // Manipulating the camera target descriptor doesn't work on URP 13 and older,
            // so instead we use URP's own render scale setting to control scaling.
            _renderPipelineAsset.renderScale = controller.RenderScale;

            // URP 13 and older do not have a separate camera jitter matrix for TAA yet,
            // so instead we modify the projection matrix on-the-fly as the camera is about to start rendering.
            _upscalingRenderPass.Setup(controller);
            Matrix4x4 jitterMatrix = _upscalingRenderPass.SetupJitter(camera.pixelWidth);
            
            camera.ResetProjectionMatrix();
            Matrix4x4 projectionMatrix = camera.projectionMatrix;
            camera.nonJitteredProjectionMatrix = projectionMatrix;
            camera.projectionMatrix = jitterMatrix * projectionMatrix;
            camera.useJitteredProjectionMatrixForTransparentRendering = true;
#endif
        }

        private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!Application.isPlaying || !isActive)
            {
                return;
            }

#if !UNITY_2022_2_OR_NEWER
            // Reset rendering setup only when upscaling has been active on this camera
            if (!camera.TryGetComponent(out TNDUpscaler controller) || !controller.enabled)
            {
                return;
            }

            if (_renderPipelineAsset != null)
            {
                _renderPipelineAsset.renderScale = 1.0f;
            }
            
            camera.ResetProjectionMatrix();
#endif
        }
    }
}
