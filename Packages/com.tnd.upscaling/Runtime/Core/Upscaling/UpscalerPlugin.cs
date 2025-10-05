using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework
{
    public interface IUpscalerPlugin: IComparable
    {
        /// <summary>
        /// Unique identifier for this upscaler plugin.
        /// This identifier should be considered opaque; code making use of it cannot make any assumptions about the contents of this string.
        /// </summary>
        string Identifier { get; }
        
        /// <summary>
        /// Short name that can be used to identify and address the upscaler plugin.
        /// </summary>
        UpscalerName Name { get; }
        
        /// <summary>
        /// Longer name for the upscaler plugin that can be used to display it in a user interface.
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Priority of this upscaler relative to other upscaler plugins.
        /// This number is used to sort upscaler plugins when displayed in UI.
        /// It is also used to resolve conflicts between plugins with the same Name; higher priority plugins get precedence.
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Whether this upscaler is supported on the current platform.
        /// Use this to determine ahead of time whether creation of this upscaler will succeed.
        /// </summary>
        bool IsSupported { get; }
        
        /// <summary>
        /// Whether this upscaler makes use of temporal reprojection and accumulation techniques.
        /// Temporal upscalers require a jittered camera projection matrix and usually have addition inputs to control the amount of temporal blending.
        /// If false, assume this is a spatial upscaler.
        /// </summary>
        bool IsTemporalUpscaler { get; }
        
        /// <summary>
        /// Whether this upscaler supports dynamic resolution scaling.
        /// If not, its internal resources should be allocated at a fixed render resolution and not change throughout its lifetime.
        /// </summary>
        bool SupportsDynamicResolution { get; }
        
        /// <summary>
        /// Whether this upscaler makes use of advanced machine learning (AI) techniques.
        /// ML-based upscalers usually need less assistance from additional inputs like transparency masks to produce good results.
        /// </summary>
        bool UsesMachineLearning { get; }
        
        /// <summary>
        /// Whether this upscaler can also upscale the alpha channel.
        /// </summary>
        bool IncludesAlphaUpscale { get; }
        
        /// <summary>
        /// Whether this upscaler can make use of a reactive mask as input to control the amount of temporal history blending.
        /// When this is false, all render passes related to the creation of a reactive mask can be skipped.
        /// </summary>
        bool AcceptsReactiveMask { get; }
        
        /// <summary>
        /// Required format for the reactive mask input texture.
        /// In most cases this will be a single-channel 8-bit format, but some upscalers may have different requirements.
        /// </summary>
        GraphicsFormat ReactiveMaskFormat { get; }

        UpscalerSettingsBase CreateSettings();
        bool TryCreateUpscaler(CommandBuffer commandBuffer, UpscalerSettingsBase settings, in UpscalerInitParams initParams, out IUpscaler upscaler);
        void Cleanup();
    }
    
    public abstract class UpscalerPlugin<TUpscaler, TSettings>: IUpscalerPlugin
        where TUpscaler: UpscalerBase<TSettings>
        where TSettings: UpscalerSettingsBase
    {
        public string Identifier => GetType().FullName;
        public abstract UpscalerName Name { get; }
        public abstract string DisplayName { get; }
        public abstract int Priority { get; }
        public abstract bool IsSupported { get; }
        public abstract bool IsTemporalUpscaler { get; }
        public virtual bool SupportsDynamicResolution => false;
        public virtual bool UsesMachineLearning => false;
        public virtual bool IncludesAlphaUpscale => false;
        public virtual bool AcceptsReactiveMask => false;
        public virtual GraphicsFormat ReactiveMaskFormat => GraphicsFormat.R8_UNorm;

        public UpscalerSettingsBase CreateSettings() => ScriptableObject.CreateInstance<TSettings>();

        public bool TryCreateUpscaler(CommandBuffer commandBuffer, UpscalerSettingsBase settings, in UpscalerInitParams initParams, out IUpscaler upscaler)
        {
            if (settings is not TSettings settingsImpl || !TryCreateUpscaler(commandBuffer, settingsImpl, initParams, out TUpscaler upscalerImpl))
            {
                upscaler = null;
                return false;
            }

            upscaler = upscalerImpl;
            return true;
        }
        
        protected abstract bool TryCreateUpscaler(CommandBuffer commandBuffer, TSettings settings, in UpscalerInitParams initParams, out TUpscaler upscaler);

        public virtual void Cleanup()
        {
        }
        
        protected static void RegisterUpscalerPlugin(IUpscalerPlugin upscalerPlugin)
        {
            UpscalerPluginRegistry.RegisterUpscalerPlugin(upscalerPlugin);
        }

        protected static void RegisterUpscalerPlugin<TUpscalerPlugin>()
            where TUpscalerPlugin : UpscalerPlugin<TUpscaler, TSettings>, new()
        {
            UpscalerPluginRegistry.RegisterUpscalerPlugin(new TUpscalerPlugin());
        }

        public int CompareTo(object obj)
        {
            if (obj is not IUpscalerPlugin other)
                return 0;

            return Priority.CompareTo(other.Priority);
        }
    }
}
