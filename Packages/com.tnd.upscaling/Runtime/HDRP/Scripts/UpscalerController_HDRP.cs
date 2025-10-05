using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TND.Upscaling.Framework.HDRP
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(HDAdditionalCameraData))]
    public abstract class UpscalerController_HDRP : UpscalerController
    {
        private Camera _camera;
        private HDAdditionalCameraData _hdAdditionalCameraData;
        private GameObject _customPassGameObject;
        private OpaqueCopyPass _opaqueCopyPass;
        private FreezeJitterPass _freezeJitterPass;

        protected internal override Texture OpaqueOnlyTexture => _opaqueCopyPass?.OpaqueOnlyTexture;

        protected override void Awake()
        {
            base.Awake();

            _camera = GetComponent<Camera>();
            _hdAdditionalCameraData = GetComponent<HDAdditionalCameraData>();
            _opaqueCopyPass = CreateOpaqueOnlyCustomPass();
            _freezeJitterPass = CreateCustomJitterPass();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _hdAdditionalCameraData.allowDeepLearningSuperSampling = true;

            // Perform run-time validations when in Play mode
            if (Application.isPlaying)
            {
                switch (RuntimeValidations.ValidateInjection())
                {
                    case RuntimeValidations.InjectionStatus.NvidiaModuleNotInstalled:
                    {
                        Debug.LogError(
@$"TND Upscaler is present and enabled on camera {name}, but the NVIDIA DLSS scripting defines are missing!
Make sure the NVIDIA module is correctly enabled in the Package Manager and that ENABLE_NVIDIA and ENABLE_NVIDIA_MODULE are defined for the current build target. 
The TND Upscaler script will now disable itself.");
                        enabled = false;
                        return;
                    }
                    case RuntimeValidations.InjectionStatus.DlssPassUsesNvidiaModule:
                    {
                        Debug.LogError(
@$"TND Upscaler is present and enabled on camera {name}, but the TND classes are not fully injected into HDRP yet!
Make sure the TND Upscaler framework is installed properly, and try recompiling scripts or restarting Unity.
If this error does not go away by itself, please try manually reimporting the Unity.RenderPipelines.HighDefinition.Runtime assembly definition. 
The TND Upscaler script will now disable itself.");
                        enabled = false;
                        return;
                    }
                }

                if (!RuntimeValidations.ValidateRenderPipelineSettings(((HDRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).currentPlatformRenderPipelineSettings))
                {
                    Debug.LogError(
@$"TND Upscaler is present and enabled on camera {name}, but the current HDRP Render Pipeline Asset is incorrectly configured! 
Make sure you follow the TND Upscaler setup steps for the currently active Build Target, Quality Level and associated Render Pipeline Asset. 
The TND Upscaler script will now disable itself.");
                    enabled = false;
                    return;
                }

                if (!RuntimeValidations.ValidateCameraSettings(_camera, _hdAdditionalCameraData, this))
                {
                    Debug.LogError(
@$"TND Upscaler is present and enabled on camera {name}, but the camera itself is incorrectly configured! 
Make sure you follow the TND Upscaler setup steps for camera {name}. 
The TND Upscaler script will now disable itself.");
                    enabled = false;
                    return;
                }
            }

            if (_opaqueCopyPass != null)
                _opaqueCopyPass.enabled = EnableOpaqueOnlyCopy;
            
            if (_freezeJitterPass != null)
                _freezeJitterPass.enabled = UpscalerContext?.ActiveUpscalerPlugin is not { IsTemporalUpscaler: true };
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _hdAdditionalCameraData.allowDeepLearningSuperSampling = false;
            
            if (_opaqueCopyPass != null)
                _opaqueCopyPass.enabled = false;
            
            if (_freezeJitterPass != null)
                _freezeJitterPass.enabled = false;
        }

        protected override void Update()
        {
            base.Update();
            
            if (qualityMode > UpscalerQuality.Custom)
            {
                DynamicResolutionHandler.SetDynamicResScaler(() => 100f / GetScaleFactor(qualityMode), DynamicResScalePolicyType.ReturnsPercentage);
                DynamicResolutionHandler.SetActiveDynamicScalerSlot(DynamicResScalerSlot.User);
            }
            
            if (_opaqueCopyPass != null)
                _opaqueCopyPass.enabled = EnableOpaqueOnlyCopy;

            if (_freezeJitterPass != null)
                _freezeJitterPass.enabled = UpscalerContext?.ActiveUpscalerPlugin is not { IsTemporalUpscaler: true };
        }

        protected void ResetCameraHistory()
        {
            HDCamera.GetOrCreate(_camera).Reset();
        }

        private OpaqueCopyPass CreateOpaqueOnlyCustomPass()
        {
            _customPassGameObject = GetOrCreateCustomPassObject();

            var customPassVolume = _customPassGameObject.AddComponent<CustomPassVolume>();
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeTransparent;
            return (OpaqueCopyPass)customPassVolume.AddPassOfType<OpaqueCopyPass>();
        }

        private FreezeJitterPass CreateCustomJitterPass()
        {
            _customPassGameObject = GetOrCreateCustomPassObject();

            var jitterPassVolume = _customPassGameObject.AddComponent<CustomPassVolume>();
            jitterPassVolume.injectionPoint = CustomPassInjectionPoint.BeforeRendering;
            return (FreezeJitterPass)jitterPassVolume.AddPassOfType<FreezeJitterPass>();
        }

        private GameObject GetOrCreateCustomPassObject()
        {
            if (_customPassGameObject == null)
            {
                _customPassGameObject = new GameObject("HDRP UpscalerController Custom Passes");
                _customPassGameObject.transform.parent = transform;
                _customPassGameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            return _customPassGameObject;
        }
    }
}
