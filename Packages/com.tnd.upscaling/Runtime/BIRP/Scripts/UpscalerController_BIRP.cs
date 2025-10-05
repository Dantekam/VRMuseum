using System;
using System.Collections.Generic;
using UnityEngine;
#if TND_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

namespace TND.Upscaling.Framework.BIRP
{
    [RequireComponent(typeof(Camera))]
    public abstract class UpscalerController_BIRP : UpscalerController
    {
        [SerializeField, Tooltip("Whether to automatically adjust texture mipmap bias")]
        protected internal bool autoTextureUpdate = true;
        
        [SerializeField, Tooltip("How often the texture mipmap bias should be checked and updated, in seconds")]
        protected internal float updateInterval = 2.0f;
        
        [SerializeField, Range(-1.0f, 1.0f), Tooltip("Additional adjustment to the mipmap bias value")]
        protected internal float additionalBias = 0.0f;
        
        private ImageEffectUpscaler _imageEffectUpscaler;
#if TND_POST_PROCESSING_STACK_V2
        private PostProcessLayer _postProcessLayer;
#endif

        private float _mipmapTimer;
        private float _prevMipmapBias;
        private ulong _prevMemoryUsage;

        protected override void Awake()
        {
            base.Awake();
            _imageEffectUpscaler = GetComponent<ImageEffectUpscaler>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
#if TND_POST_PROCESSING_STACK_V2
            if (TryGetComponent(out _postProcessLayer) && _postProcessLayer.enabled)
            {
                _postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.AdvancedUpscaling;
            }
#endif
            
            CheckImageEffectUpscaler();
        }

        protected override void OnDisable()
        {
            RemoveImageEffectUpscaler();
            
#if TND_POST_PROCESSING_STACK_V2
            if (_postProcessLayer != null && _postProcessLayer.enabled)
            {
                _postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
            }
#endif

            if (autoTextureUpdate)
            {
                MipmapUtils.ResetAllMipmaps(ref _prevMipmapBias);
            }

            base.OnDisable();
        }

        protected override void Update()
        {
            base.Update();
            
#if TND_POST_PROCESSING_STACK_V2
            // Check if a post-processing layer component may have been added at run-time
            if (_postProcessLayer == null && TryGetComponent(out _postProcessLayer) && _postProcessLayer.enabled)
            {
                _postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.AdvancedUpscaling;
            }
#endif
            
            CheckImageEffectUpscaler();

            if (autoTextureUpdate)
            {
                Vector2Int renderSize = Vector2Int.zero;
                Vector2Int displaySize = Vector2Int.zero;
                
#if TND_POST_PROCESSING_STACK_V2
                if (_postProcessLayer != null && _postProcessLayer.upscaling != null && _postProcessLayer.enabled)
                {
                    renderSize = _postProcessLayer.upscaling.MaxRenderSize;
                    displaySize = _postProcessLayer.upscaling.DisplaySize;
                }
#endif

                if (_imageEffectUpscaler != null)
                {
                    renderSize = _imageEffectUpscaler.MaxRenderSize;
                    displaySize = _imageEffectUpscaler.DisplaySize;
                }

                if (renderSize.x > 0 && displaySize.x > 0)
                {
                    MipmapUtils.AutoUpdateMipmaps(renderSize.x, displaySize.x, additionalBias, updateInterval, ref _prevMipmapBias, ref _mipmapTimer, ref _prevMemoryUsage);
                }
            }
            else
            {
                MipmapUtils.ResetAllMipmaps(ref _prevMipmapBias);
            }
        }

        protected void ResetCameraHistory()
        {
#if TND_POST_PROCESSING_STACK_V2
            if (_postProcessLayer != null && _postProcessLayer.upscaling != null)
            {
                _postProcessLayer.upscaling.ResetHistory();
            }
#endif

            if (_imageEffectUpscaler != null)
            {
                _imageEffectUpscaler.ResetHistory();
            }
        }

        private void CheckImageEffectUpscaler()
        {
#if TND_POST_PROCESSING_STACK_V2
            if (_postProcessLayer != null && _postProcessLayer.enabled)
            {
                RemoveImageEffectUpscaler();
                return;
            }
#endif
            
            AddImageEffectUpscaler();
        }

        private void AddImageEffectUpscaler()
        {
            if (_imageEffectUpscaler != null)
                return;

            // Create a separate invisible script component to do the upscaling
            // This way OnRenderImage will only be used if image effect upscaling is active
            _imageEffectUpscaler = gameObject.AddComponent<ImageEffectUpscaler>();
            _imageEffectUpscaler.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
        }

        private void RemoveImageEffectUpscaler()
        {
            if (_imageEffectUpscaler == null)
                return;
            
            UpscalerUtils.DestroyObject(_imageEffectUpscaler);
            _imageEffectUpscaler = null;
        }
    }
}
