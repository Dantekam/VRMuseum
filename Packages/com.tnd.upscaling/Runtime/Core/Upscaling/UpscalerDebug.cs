using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace TND.Upscaling.Framework
{
    /// <summary>
    /// Passthrough for diagnostic log messages that will only show when TND_DEBUG is defined in the project.
    /// </summary>
    public static class UpscalerDebug
    {
        [Conditional("TND_DEBUG")]
        public static void Log(object message)
        {
            Debug.Log(message);
        }

        [Conditional("TND_DEBUG")]
        public static void LogWarning(object message)
        {
            Debug.LogWarning(message);
        }

        [Conditional("TND_DEBUG")]
        public static void LogError(object message)
        {
            Debug.LogError(message);
        }

        [Conditional("TND_DEBUG")]
        public static void LogException(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
