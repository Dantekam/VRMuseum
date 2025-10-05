using System;
using UnityEngine;

namespace TND.Upscaling.Framework
{
    public enum UpscalerQuality
    {
        Off = -1,
        [InspectorName(null)] Custom = 0,
        [InspectorName("Native AA (1x)")] NativeAA,
        [InspectorName("Ultra Quality (1.2x)")] UltraQuality,
        [InspectorName("Quality (1.5x)")] Quality,
        [InspectorName("Balanced (1.7x)")] Balanced,
        [InspectorName("Performance (2x)")] Performance,
        [InspectorName("Ultra Performance (3x)")] UltraPerformance,
    }
    
    [Serializable]
    public abstract class UpscalerSettingsBase: ScriptableObject
    {
        /// <summary>
        /// Returns whether the settings have changed in such a way that a restart of the upscaler is required.
        /// Subclasses should internally cache the previous state of relevant settings and compare the current values to check for changes.
        /// </summary>
        public virtual bool RestartRequired() => false;

        /// <summary>
        /// Called at the end of the frame to allow subclasses to update their internal values, used to detect changes on the next frame.
        /// </summary>
        public virtual void UpdateCachedValues() { }

        protected virtual void Awake()
        {
            UpdateCachedValues();
        }
    }

    [Serializable]
    public struct AutoReactiveSettings
    {
        [Range(0, 1)] public float scale;
        [Range(0, 1)] public float cutoffThreshold;
        [Range(0, 1)] public float binaryValue;
        public AutoReactiveFlags flags;

        public static readonly AutoReactiveSettings Default = new()
        {
            scale = 0.9f, 
            cutoffThreshold = 0.05f, 
            binaryValue = 0.5f, 
            flags = AutoReactiveFlags.ApplyTonemap | AutoReactiveFlags.ApplyThreshold | AutoReactiveFlags.UseComponentsMax,
        };
    }
    
    [Flags]
    public enum AutoReactiveFlags
    {
        ApplyTonemap = 1 << 0,
        ApplyInverseTonemap = 1 << 1,
        ApplyThreshold = 1 << 2,
        UseComponentsMax = 1 << 3,
    }
}
