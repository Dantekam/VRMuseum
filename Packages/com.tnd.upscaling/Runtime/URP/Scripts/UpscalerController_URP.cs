using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace TND.Upscaling.Framework.URP
{
    [RequireComponent(typeof(Camera), typeof(UniversalAdditionalCameraData))]
    public abstract class UpscalerController_URP: UpscalerController
    {
        internal Vector2Int DisplaySize => _displaySize;
        internal Vector2Int MaxRenderSize => _maxRenderSize;
        internal Vector2Int ScaledRenderSize => _scaledRenderSize;
        internal bool IsSinglePassXR => _isSinglePassXR;
        internal IUpscaler ActiveUpscaler => GetUpscalerContext().ActiveUpscaler;
        internal IUpscalerPlugin ActiveUpscalerPlugin => GetUpscalerContext().ActiveUpscalerPlugin;

        protected internal override bool ResetHistory => _historyResetFrame == Time.frameCount;
        
        protected Camera _camera;
        protected UniversalAdditionalCameraData _additionalCameraData;
        protected UniversalRenderPipelineAsset _renderPipelineAsset;
        protected int _historyResetFrame = 0;

#if UNITY_2022_2_OR_NEWER
        private bool EnableHDR => _camera.allowHDR && _additionalCameraData.allowHDROutput && _renderPipelineAsset.supportsHDR;
#else
        private bool EnableHDR => _camera.allowHDR && _renderPipelineAsset.supportsHDR;
#endif
        
        private readonly List<UpscalerContext> _upscalerContexts = new();

        private Vector2Int _displaySize;
        private Vector2Int _maxRenderSize;
        private Vector2Int _scaledRenderSize;
        private bool _isSinglePassXR;
        private int _viewCount;
        
        private Vector2Int _prevDisplaySize;
        private UpscalerQuality _prevQualityMode;
        private bool _prevHDR;
        private bool _prevSinglePassXR;
        private int _prevViewCount;

        protected override void Awake()
        {
            base.Awake();

            _camera = GetComponent<Camera>();
            _additionalCameraData = GetComponent<UniversalAdditionalCameraData>();
            _renderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (!RendererFeatureExists())
            {
                Debug.LogError(
@$"TND Upscaler is present and enabled on camera {name}, but the TND Upscaling Renderer Feature is missing on the current Renderer Data!
Be sure to add the TND Upscaling Renderer Feature to the currently active Render Pipeline Asset if you want to use upscaling.
The TND Upscaler script will now disable itself.");
                enabled = false;
                return;
            }

            _displaySize = CalculateDisplaySize(out _viewCount, out _isSinglePassXR);
            _maxRenderSize = CalculateMaxRenderSize();
            _scaledRenderSize = _maxRenderSize;

            UpscalerInitParams initParams = new()
            {
                camera = _camera,
                maxRenderSize = _maxRenderSize,
                upscaleSize = _displaySize,
                useTextureArrays = false,
                numTextureSlices = 1,
                enableHDR = EnableHDR,
                invertedDepth = SystemInfo.usesReversedZBuffer,
                highResMotionVectors = false,   // TODO: might need to be true for VR
#if UNITY_2022_2_OR_NEWER
                jitteredMotionVectors = false,
#else
                // Motion vector jitter cancellation is required if we jitter the camera projection matrix on-the-fly in BeginCameraRendering,
                // as that will affect all render passes that make use of the projection matrix, including the motion vector pass.
                jitteredMotionVectors = true,
#endif
            };

            // Ensure we have enough upscaler instances for the number of views on this camera
            for (int i = _upscalerContexts.Count; i < _viewCount; ++i)
            {
                _upscalerContexts.Add(new UpscalerContext());
            }
            
            UpscalerUtils.RunWithCommandBuffer(cmd =>
            {
                for (int view = 0; view < _viewCount; ++view)
                {
                    // The first view in single-pass instanced rendering will be presented as the original texture array,
                    // the remaining views will be presented as simple texture copies.
                    initParams.useTextureArrays = _isSinglePassXR && view == 0;
                    
                    _upscalerContexts[view].Initialize(cmd, initParams);
                }
            });

            _prevQualityMode = qualityMode;
            _prevDisplaySize = _displaySize;
            _prevHDR = EnableHDR;
            _prevSinglePassXR = _isSinglePassXR;
            _prevViewCount = _viewCount;
        }

        protected override void OnDisable()
        {
            base.OnDisable(); 
            
            UpscalerUtils.RunWithCommandBuffer(cmd =>
            {
                foreach (var upscalerContext in _upscalerContexts)
                {
                    upscalerContext.Destroy(cmd);
                }
            });
            
            _upscalerContexts.Clear();
        }

        protected override void Update()
        {
            base.Update();

            _displaySize = CalculateDisplaySize(out _viewCount, out _isSinglePassXR);
            if (_displaySize != _prevDisplaySize || qualityMode != _prevQualityMode || EnableHDR != _prevHDR || _isSinglePassXR != _prevSinglePassXR || _viewCount != _prevViewCount)
            {
                OnDisable();
                OnEnable();
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            
            _renderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            
            UpdateScaledRenderSize();
        }

        internal UpscalerContext GetUpscalerContext(int viewIndex = 0)
        {
            if (viewIndex < 0 || viewIndex >= _upscalerContexts.Count)
                return null;

            return _upscalerContexts[viewIndex];
        }

        private Vector2Int CalculateDisplaySize(out int viewCount, out bool isSinglePassXR)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            if (UpscalingHelpers.TryGetXREyeDisplaySize(_camera, _additionalCameraData, out Vector2Int xrDisplaySize, out viewCount, out isSinglePassXR))
            {
                return xrDisplaySize;
            }
#endif

            viewCount = 1;
            isSinglePassXR = false;
            return new Vector2Int(_camera.pixelWidth, _camera.pixelHeight);
        }
        
        private Vector2Int CalculateMaxRenderSize()
        {
            // Scale factor from the upscaler quality mode determines the maximum render size
            float renderScale = RenderScale;
            return new Vector2Int((int)(_displaySize.x * renderScale), (int)(_displaySize.y * renderScale));
        }

        private void UpdateScaledRenderSize()
        {
            _scaledRenderSize = _maxRenderSize;
            if (_renderPipelineAsset != null)
            {
                // Render Scale on the URP asset
                float renderScale = _renderPipelineAsset.renderScale;
                bool disableRenderScale = Mathf.Abs(1.0f - renderScale) < 0.05f;
                renderScale = disableRenderScale ? 1.0f : renderScale;
                _scaledRenderSize.x = Mathf.Max(1, (int)(_scaledRenderSize.x * renderScale));
                _scaledRenderSize.y = Mathf.Max(1, (int)(_scaledRenderSize.y * renderScale));
            }
            if (_camera.allowDynamicResolution)
            {
                // Dynamic resolution scale on the camera
                _scaledRenderSize.x = Mathf.CeilToInt(_scaledRenderSize.x * ScalableBufferManager.widthScaleFactor);
                _scaledRenderSize.y = Mathf.CeilToInt(_scaledRenderSize.y * ScalableBufferManager.heightScaleFactor);
            }
        }
        
        private static bool RendererFeatureExists()
        {
            var renderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (renderPipelineAsset == null)
                return false;

            var rendererData = UpscalingHelpers.GetRendererData(renderPipelineAsset);
            if (rendererData == null)
                return false;

            foreach (var rendererFeature in rendererData.rendererFeatures)
            {
                if (rendererFeature is UpscalingRendererFeature)
                    return true;
            }
            
            return false;
        }
    }
}
