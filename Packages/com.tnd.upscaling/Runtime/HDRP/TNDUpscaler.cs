using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TND.Upscaling.Framework
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(HDAdditionalCameraData))]
    [ExecuteInEditMode]
    public class TNDUpscaler : HDRP.UpscalerController_HDRP
    {
        /// <summary>
        /// Sets the currently selected upscaler.
        /// Returns whether the upscaler was found and successfully set.
        /// This does not guarantee that the upscaler is supported or successfully initialized.
        /// </summary>
        public bool SetUpscaler(UpscalerName upscalerName)
        {
            return SetPrimaryUpscaler(upscalerName);
        }

        /// <summary>
        /// Gets the currently selected upscaler.
        /// This may differ from the currently active upscaler; if the selected upscaler is not supported then a different upscaler will be activated.
        /// </summary>
        public UpscalerName GetSelectedUpscaler()
        {
            return GetPrimaryUpscalerName();
        }

        /// <summary>
        /// Gets the currently active upscaler, if any.
        /// This may differ from the currently selected upscaler; if the selected upscaler is not supported then a different upscaler will be activated.
        /// </summary>
        public UpscalerName GetActiveUpscaler()
        {
            return GetCurrentlyActiveUpscalerPlugin()?.Name ?? UpscalerName.None;
        }
        
        /// <summary>
        /// Use this method to adjust the upscaler quality level.
        /// </summary>
        public void SetQuality(UpscalerQuality value)
        {
            qualityMode = value;
            ValidateQualityMode();
        }

        /// <summary>
        /// Returns the currently selected upscaler quality level.
        /// </summary>
        public UpscalerQuality GetQuality()
        {
            return qualityMode;
        }
        
        /// <summary>
        /// Returns the scale factor (e.g. 1.5x, 2.0x, 3.0x) for the currently selected upscaler quality level.
        /// </summary>
        public float GetScaling()
        {
            return GetScaleFactor(qualityMode);
        }

        /// <summary>
        /// Use this method to enable or disable sharpening.
        /// </summary>
        public void SetSharpening(bool value)
        {
            enableSharpening = value;
        }

        /// <summary>
        /// Returns whether sharpening is currently enabled.
        /// </summary>
        public bool GetSharpening()
        {
            return enableSharpening;
        }

        /// <summary>
        /// Use this method to set the sharpening intensity.
        /// </summary>
        public void SetSharpness(float value)
        {
            sharpness = value;
        }

        /// <summary>
        /// Returns the current sharpening intensity value.
        /// </summary>
        public float GetSharpness()
        {
            return sharpness;
        }
        
        /// <summary>
        /// Sets whether to enable or disable the automatic reactive mask generation feature. 
        /// </summary>
        public void SetAutoReactive(bool value)
        {
            autoGenerateReactiveMask = value;
        }

        /// <summary>
        /// Returns whether automatic reactive mask generation is currently enabled.
        /// </summary>
        public bool GetAutoReactive()
        {
            return autoGenerateReactiveMask;
        }
        
        /// <summary>
        /// Retrieve the upscaler-specific settings for the given upscaler.
        /// Returns null if the settings could not be found.
        /// </summary>
        public UpscalerSettingsBase GetUpscalerSettings(UpscalerName upscalerName)
        {
            IUpscalerPlugin upscalerPlugin = UpscalerPluginRegistry.FindUpscalerPlugin(upscalerName);
            if (TryGetUpscalerSettings(upscalerPlugin, out UpscalerSettingsBase settings))
            {
                return settings;
            }

            return null;
        }

        /// <summary>
        /// Retrieve the upscaler-specific settings for the given upscaler, cast to a specific type.
        /// Returns null if the settings could not be found.
        /// </summary>
        public TSettings GetUpscalerSettings<TSettings>(UpscalerName upscalerName) where TSettings : UpscalerSettingsBase
        {
            return GetUpscalerSettings(upscalerName) as TSettings;
        }
        
        /// <summary>
        /// Use this method to Reset the camera for the next frame by clearing all buffers from previous frames, preventing visual artifacts.
        /// Use this on camera cuts, or when the camera teleports or moves abruptly.
        /// </summary>
        public void ResetCamera()
        {
            ResetCameraHistory();
        }

        /// <summary>
        /// Use this method to retrieve a list of all currently supported upscalers.
        /// </summary>
        public static List<UpscalerName> GetSupported()
        {
            List<UpscalerName> supported = new();
            GetSupported(supported);
            return supported;
        }

        /// <summary>
        /// Use this method to retrieve a list of all currently supported upscalers.
        /// This method takes a pre-allocated list, so you can prevent unnecessary GC allocations. 
        /// </summary>
        public static void GetSupported(List<UpscalerName> supported)
        {
            ForEachUpscalerPlugin(upscalerPlugin =>
            {
                if (upscalerPlugin.IsSupported)
                {
                    supported.Add(upscalerPlugin.Name);
                }
            });
        }

        /// <summary>
        /// Returns if the given upscaler is available and supported on the current hardware.
        /// </summary>
        public static bool IsSupported(UpscalerName upscalerName)
        {
            bool isSupported = false;
            
            ForEachUpscalerPlugin(upscalerPlugin =>
            {
                if (upscalerPlugin.Name == upscalerName && upscalerPlugin.IsSupported)
                {
                    isSupported = true;
                }
            });

            return isSupported;
        }
    }
}
