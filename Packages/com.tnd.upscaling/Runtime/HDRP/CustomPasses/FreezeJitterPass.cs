using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace TND.Upscaling.Framework.HDRP
{
    /// <summary>
    /// Custom pass that doesn't render anything, but is instead used to "freeze" TAA jitter in place, effectively disabling it.
    /// This is used for spatial upscalers, which do not require any camera projection jittering.
    /// Since the HDRP upscaler injection relies on pretending we are DLSS, we always get a jittered camera from HDRP whether we want it or not.
    /// To get around this, we lock the TAA jitter in place by repeatedly setting the same frame index value on the HDCamera. 
    /// </summary>
    public class FreezeJitterPass : CustomPass
    {
        /// <summary>
        /// This method gets called by HDRP during culling, after the initial HDCamera parameters are set, but before any actual rendering begins.
        /// It's the perfect moment for us to make some changes to HDCamera that will affect the entire render pipeline.
        /// </summary>
        protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
        {
            TaaFrameIndexField.SetValue(hdCamera, 21504); // This value produces a taaJitter very close to (0, 0)
            UpdateAllViewConstants(hdCamera, true, false);
        }

        private static readonly MethodInfo UpdateAllViewConstantsMethod =
            typeof(HDCamera).GetMethod("UpdateAllViewConstants", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool), typeof(bool) }, null);

        private delegate void UpdateAllViewConstantsDelegate(HDCamera hdCamera, bool jitterProjectionMatrix, bool updatePreviousFrameConstants);

        private static readonly UpdateAllViewConstantsDelegate UpdateAllViewConstants =
            (UpdateAllViewConstantsDelegate)UpdateAllViewConstantsMethod.CreateDelegate(typeof(UpdateAllViewConstantsDelegate));

        private static readonly FieldInfo TaaFrameIndexField = typeof(HDCamera).GetField("taaFrameIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        
    }
}
