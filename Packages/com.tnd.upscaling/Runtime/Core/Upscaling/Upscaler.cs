using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework
{
    public interface IUpscaler
    {
        /// <summary>
        /// The lowest possible render resolution that this upscaler is capable of upscaling from.
        /// For most upscalers this will be 1x1, but some specialty upscalers have higher requirements.
        /// </summary>
        Vector2Int MinimumRenderSize { get; }
        
        /// <summary>
        /// Whether this upscaler can benefit from using an opaque-only framebuffer copy. If not, the opaque-only copy pass can be skipped.
        /// This value may change depending on the current run-time settings.
        /// </summary>
        bool RequiresOpaqueOnlyInput { get; }
        
        /// <summary>
        /// Whether the upscaler requires random writes to be enabled on the render texture used as the output target.
        /// For compute shader-based upscalers, this is usually true. Some upscalers may be more lenient, allowing for additional optimizations and higher compatibility.
        /// </summary>
        bool RequiresRandomWriteOutput { get; }
        
        bool Initialize(CommandBuffer commandBuffer, in UpscalerInitParams initParams);
        void Dispatch(CommandBuffer commandBuffer, in UpscalerDispatchParams dispatchParams);
        void Destroy(CommandBuffer commandBuffer);

        /// <summary>
        /// Returns whether anything significant has changed in the upscaler's settings or internal state that requires a restart of the upscaler. 
        /// </summary>
        bool RestartRequired();
        
        /// <summary>
        /// Generate a jitter pattern for offsetting the camera projection matrix.
        /// Normally this is based on a Halton sequence, and the default implementation follows the standard recipe used by DLSS, FSR2 and their offshoots.
        /// Upscalers can override this method if they prefer to use a different jitter pattern.
        /// This may not work on all render pipelines.
        /// </summary>
        Vector2 GetJitterOffset(int frameIndex, int renderWidth, int upscaleWidth);
    }

    public abstract class UpscalerBase : IUpscaler
    {
        public virtual Vector2Int MinimumRenderSize => Vector2Int.one;
        public virtual bool RequiresOpaqueOnlyInput => false;
        public virtual bool RequiresRandomWriteOutput => true;

        private Shader _sharpenShader;
        private Material _sharpenMaterial;
        
        public abstract bool Initialize(CommandBuffer commandBuffer, in UpscalerInitParams initParams);
        
        public abstract void Dispatch(CommandBuffer commandBuffer, in UpscalerDispatchParams dispatchParams);
        
        public virtual void Destroy(CommandBuffer commandBuffer)
        {
            UpscalerUtils.UnloadMaterial(ref _sharpenMaterial, ref _sharpenShader);
        }

        public virtual bool RestartRequired() => false;

        public virtual Vector2 GetJitterOffset(int frameIndex, int renderWidth, int upscaleWidth)
        {
            const float basePhaseCount = 8.0f;
            int phaseCount = (int)(basePhaseCount * Mathf.Pow((float)upscaleWidth / renderWidth, 2.0f));
            int index = (frameIndex % phaseCount) + 1;
            return new Vector2(Halton(index, 2) - 0.5f, Halton(index, 3) - 0.5f);
        }
        
        public static float Halton(int index, int radix)
        {
            float f = 1.0f, result = 0.0f;

            for (int currentIndex = index; currentIndex > 0;) {

                f /= radix;
                result += f * (currentIndex % radix);
                currentIndex = (int)Mathf.Floor((float)currentIndex / radix);
            }

            return result;
        }

        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int SharpnessProperty = Shader.PropertyToID("_Sharpness");

        /// <summary>
        /// Standalone RCAS sharpening pass which can be used by any upscaling algorithm that doesn't have a built-in sharpener.
        /// Since this pass uses a fragment shader, it does not require a random read/write-enabled texture for the final output. Upscalers can take advantage of this.
        /// </summary>
        protected void SharpenPass(CommandBuffer cmd, RenderTargetIdentifier inputColor, RenderTargetIdentifier outputColor, float sharpness)
        {
            if (!UpscalerUtils.TryLoadMaterial("TND_Sharpen", ref _sharpenMaterial, ref _sharpenShader))
            {
                cmd.Blit(inputColor, outputColor);
                return;
            }
            
            cmd.BeginSample("RCAS Sharpening");
            cmd.SetGlobalTexture(MainTexProperty, inputColor);
            cmd.SetGlobalFloat(SharpnessProperty, Mathf.Pow(2.0f, 2.0f * sharpness - 2.0f));
            cmd.SetRenderTarget(outputColor);
            cmd.DrawProcedural(Matrix4x4.identity, _sharpenMaterial, 0, MeshTopology.Triangles, 3, 1);
            cmd.EndSample("RCAS Sharpening");
        }
    }
    
    public abstract class UpscalerBase<TSettings>: UpscalerBase
        where TSettings: UpscalerSettingsBase
    {
        protected TSettings Settings { get; }
        
        protected UpscalerBase(TSettings settings)
        {
            Settings = settings;
        }

        public override bool RestartRequired()
        {
            return Settings != null && Settings.RestartRequired();
        }
    }
}
