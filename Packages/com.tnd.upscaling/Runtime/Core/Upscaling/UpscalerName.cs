using System;

namespace TND.Upscaling.Framework
{
    /// <summary>
    /// This is a comprehensive list of all known upscalers that may be supported at some point in the past, present or future.
    /// The numeric values are chosen specifically to allow grouping per upscaler brand, while also allowing separation by major and minor version numbers.
    /// Not all of these enum values translate to an actual existing upscaler plugin, nor should this list be seen as a promise for future support.
    /// </summary>
    public enum UpscalerName
    {
        None = 0,
        
        /// <summary>
        /// FidelityFX Super Resolution 1 (spatial upscaling)
        /// </summary>
        [Obsolete] FSR1 = 11000,
        
        /// <summary>
        /// FidelityFX Super Resolution 2.2 (analytical temporal upscaling)
        /// </summary>
        [Obsolete] FSR2 = 12000,
        
        /// <summary>
        /// FidelityFX Super Resolution 3.1 (analytical temporal upscaling)
        /// </summary>
        FSR3 = 13000,
        
        /// <summary>
        /// FidelityFX Super Resolution 4 (ML-based temporal upscaling)
        /// </summary>
        FSR4 = 14000,
        
        /// <summary>
        /// ARM Accuracy Super Resolution (mobile-optimized temporal upscaling)
        /// </summary>
        ASR = 21000,
        
        /// <summary>
        /// NVIDIA Deep Learning Super Sampling 3.x (ML-based temporal upscaling)
        /// </summary>
        DLSS3 = 33000,
        
        /// <summary>
        /// NVIDIA Deep Learning Super Sampling 4.x (ML-based temporal upscaling)
        /// </summary>
        DLSS4 = 34000,
        
        /// <summary>
        /// Intel Xe Super Sampling 1.x (ML-based temporal upscaling)
        /// </summary>
        [Obsolete] XeSS1 = 41000,
        
        /// <summary>
        /// Intel Xe Super Sampling 2.x (ML-based temporal upscaling)
        /// </summary>
        XeSS2 = 42000,
        
        /// <summary>
        /// Snapdragon Game Super Resolution 1 (mobile-optimized spatial upscaling)
        /// </summary>
        SGSR1 = 51000,
        
        /// <summary>
        /// Snapdragon Game Super Resolution 2 (mobile-optimized temporal upscaling)
        /// </summary>
        SGSR2 = 52000,
        
        Reserved1 = 61000,
        Reserved2 = 71000,
        Reserved3 = 81000,
        
        /// <summary>
        /// PlayStation Spectral Super Resolution (ML-based temporal upscaling)
        /// </summary>
        PSSR = 91000,
    }
}
