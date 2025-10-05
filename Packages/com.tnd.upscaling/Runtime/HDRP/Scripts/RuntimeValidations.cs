using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TND.Upscaling.Framework.HDRP
{
    public static class RuntimeValidations
    {
        private static readonly Type DlssPassType = Type.GetType("UnityEngine.Rendering.HighDefinition.DLSSPass, Unity.RenderPipelines.HighDefinition.Runtime", true);
        private static readonly FieldInfo DlssDeviceField = DlssPassType.GetField("m_Device", BindingFlags.Instance | BindingFlags.NonPublic);

        public enum InjectionStatus
        {
            NvidiaModuleNotInstalled,
            DlssPassUsesNvidiaModule,
            DlssPassUsesTndPackage,
        }
        
        public static InjectionStatus ValidateInjection()
        {
            if (DlssDeviceField == null)
            {
                // Device field only gets included when the NVIDIA module is installed, so it's possible that it doesn't exist yet
                return InjectionStatus.NvidiaModuleNotInstalled;
            }

            // After injecting references, HDRP's DLSSPass should be using classes from the TND Injectors assembly.
            return DlssDeviceField.FieldType.Assembly.FullName.StartsWith("TND", StringComparison.InvariantCulture) 
                ? InjectionStatus.DlssPassUsesTndPackage 
                : InjectionStatus.DlssPassUsesNvidiaModule;
        }

        /// <summary>
        /// Render pipeline validations that are critical and should produce an error when set incorrectly.
        /// </summary>
        public static bool ValidateRenderPipelineSettings(in RenderPipelineSettings renderPipelineSettings)
        {
            // DLSS has enabled and needs top priority for the TND upscalers to be activated
            bool dlssEnabled = false;
#if UNITY_2023_2_OR_NEWER
            var dynamicResolutionSettings = renderPipelineSettings.dynamicResolutionSettings;
            if (dynamicResolutionSettings.advancedUpscalersByPriority != null && dynamicResolutionSettings.advancedUpscalersByPriority.Count > 0)
            {
                if (dynamicResolutionSettings.advancedUpscalersByPriority[0] == AdvancedUpscalers.DLSS)
                {
                    dlssEnabled = true;
                }
            }
#else
            dlssEnabled = renderPipelineSettings.dynamicResolutionSettings.enableDLSS;
#endif

            if (!dlssEnabled)
            {
                return false;
            }

            // Dynamic resolution needs to be enabled for upscaling to work at all
            if (!renderPipelineSettings.dynamicResolutionSettings.enabled)
            {
                return false;
            }

            // DLSS Mode should be set to Maximum Quality. This doesn't really affect the upscalers directly,
            // but it mitigates some weird glitches in the Unity Editor with certain native plugin upscalers.
            if (renderPipelineSettings.dynamicResolutionSettings.DLSSPerfQualitySetting != 2)
            {
                return false;
            }

            // DLSS Optimal Settings will override the quality mode set on the TNDUpscaler script, so that needs to be disabled
            if (renderPipelineSettings.dynamicResolutionSettings.DLSSUseOptimalSettings)
            {
                return false;
            }

            // Force resolution needs to be disabled to allow different quality modes to be possible
            if (renderPipelineSettings.dynamicResolutionSettings.forceResolution)
            {
                return false;
            }

            // A minimum scale percentage of 33% is required to allow Ultra Performance mode (3.0x scale) to be possible
            if (renderPipelineSettings.dynamicResolutionSettings.minPercentage > 33)
            {
                return false;
            }

            // A maximum scale percentage of 100% is required to allow Native AA mode (1.0x scale) to be possible
            if (renderPipelineSettings.dynamicResolutionSettings.maxPercentage < 100)
            {
                return false;
            }

            // Custom pass support is required for the opaque-only copy pass
            if (!renderPipelineSettings.supportCustomPass)
            {
                return false;
            }

            // MSAA should be disabled to prevent issues with unresolved render targets
            if (renderPipelineSettings.msaaSampleCount > MSAASamples.None)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Render pipeline validations that are not critical and should produce a warning when set incorrectly.
        /// </summary>
        public static bool ValidateRenderPipelineSettingsExtra(in RenderPipelineSettings renderPipelineSettings)
        {
            // Motion vectors need to be enabled for correct temporal reprojection
            if (!renderPipelineSettings.supportMotionVectors)
            {
                return false;
            }

            // The most basic upsample filter should be set to prevent interference with our upscalers
            if (renderPipelineSettings.dynamicResolutionSettings.upsampleFilter != DynamicResUpscaleFilter.CatmullRom)
            {
                return false;
            }

            // Dynamic resolution type should be set to Software for broadest compatibility
            if (renderPipelineSettings.dynamicResolutionSettings.dynResType != DynamicResolutionType.Software)
            {
                return false;
            }

            // Global mip bias should be enabled to keep textures sharp
            if (!renderPipelineSettings.dynamicResolutionSettings.useMipBias)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Camera setting validations that are critical and should produce an error when set incorrectly.
        /// </summary>
        public static bool ValidateCameraSettings(Camera camera, HDAdditionalCameraData additionalCameraData, UpscalerController upscalerController)
        {
            // The camera should not override any of the HDRP pipeline settings:
            // Dynamic resolution needs to be enabled
            bool scriptEnabled = upscalerController != null && upscalerController.enabled;
            if (scriptEnabled && !additionalCameraData.allowDynamicResolution)
            {
                return false;
            }

            // DLSS needs to be enabled
            if (scriptEnabled && !additionalCameraData.allowDeepLearningSuperSampling)
            {
                return false;
            }

            // DLSS custom quality settings needs to be disabled
            if (additionalCameraData.deepLearningSuperSamplingUseCustomQualitySettings)
            {
                return false;
            }

            // DLSS custom attributes need to be disabled
            if (additionalCameraData.deepLearningSuperSamplingUseCustomAttributes)
            {
                return false;
            }

            // DLSS optimal settings needs to be disabled, otherwise TNDUpscaler quality mode won't work
            if (additionalCameraData.deepLearningSuperSamplingUseOptimalSettings)
            {
                return false;
            }
            
            // MSAA should be disabled to prevent issues with unresolved render targets
            if (camera.allowMSAA)
            {
                return false;
            }

            return true;
        }
        
        /// <summary>
        /// Camera setting validations that are not critical and should produce a warning when set incorrectly.
        /// </summary>
        public static bool ValidateCameraSettingsExtra(HDAdditionalCameraData additionalCameraData)
        {
            // Anti-aliasing should be disabled to prevent interference with our upscalers
            if (additionalCameraData.antialiasing != HDAdditionalCameraData.AntialiasingMode.None)
            {
                return false;
            }

            return true;
        }
    }
}
