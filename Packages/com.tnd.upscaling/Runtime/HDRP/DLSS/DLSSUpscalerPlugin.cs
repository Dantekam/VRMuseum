using UnityEngine;
using UnityEngine.Rendering;
using TND.Upscaling.Framework;
using UnityEngine.Experimental.Rendering;

namespace TND.Upscaling.HDRP.DLSS
{
    public class DLSSUpscalerPlugin: UpscalerPlugin<DLSSUpscaler, DLSSUpscalerSettings>
    {
        public override UpscalerName Name => UpscalerName.DLSS3;
        public override string DisplayName => "DLSS 3.x";
        public override int Priority => (int)UpscalerName.DLSS3 + 31;
#if UNITY_STANDALONE_WIN && TND_NVIDIA_MODULE_INSTALLED
        public override bool IsSupported => UnityEngine.NVIDIA.NVUnityPlugin.IsLoaded() && UnityEngine.SystemInfo.graphicsDeviceVendor.ToLowerInvariant().Contains("nvidia");
#else
        public override bool IsSupported => false;
#endif
        public override bool IsTemporalUpscaler => true;
        public override bool SupportsDynamicResolution => true;
        public override bool UsesMachineLearning => true;
        public override bool AcceptsReactiveMask => true;
        public override GraphicsFormat ReactiveMaskFormat => GraphicsFormat.R8G8B8A8_UNorm;
        
        protected override bool TryCreateUpscaler(CommandBuffer commandBuffer, DLSSUpscalerSettings settings, in UpscalerInitParams initParams, out DLSSUpscaler upscaler)
        {
            if (!IsSupported)
            {
                upscaler = null;
                return false;
            }

            upscaler = new DLSSUpscaler(settings);
            return upscaler.Initialize(commandBuffer, initParams);
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void RegisterUpscalerPlugin()
        {
            RegisterUpscalerPlugin(new DLSSUpscalerPlugin());
        }
    }
}
