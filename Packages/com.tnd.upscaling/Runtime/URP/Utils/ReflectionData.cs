using System.Reflection;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace TND.Upscaling.Framework.URP
{
    public static class ScriptableRendererMembers
    {
        public static readonly PropertyInfo RendererFeatures = typeof(ScriptableRenderer).GetProperty("rendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
        
        public static readonly PropertyInfo ActiveRenderPassQueue = typeof(ScriptableRenderer).GetProperty("activeRenderPassQueue", BindingFlags.Instance | BindingFlags.NonPublic);
    }
    
    public static class UniversalRendererMembers
    {
        public static readonly FieldInfo ActiveCameraDepthAttachment = typeof(UniversalRenderer).GetField("m_ActiveCameraDepthAttachment", BindingFlags.Instance | BindingFlags.NonPublic);
        public static readonly FieldInfo MotionVectorColor = typeof(UniversalRenderer).GetField("m_MotionVectorColor", BindingFlags.Instance | BindingFlags.NonPublic);
        
        public static readonly FieldInfo FinalBlitPass = typeof(UniversalRenderer).GetField("m_FinalBlitPass", BindingFlags.Instance | BindingFlags.NonPublic);
        
        public static readonly FieldInfo UseUpscaledColorHandle = typeof(UniversalRenderer).GetField("m_UseUpscaledColorHandle", BindingFlags.Static | BindingFlags.NonPublic);
        public static readonly FieldInfo UpscaledCameraColorHandles = typeof(UniversalRenderer).GetField("m_RenderGraphUpscaledCameraColorHandles", BindingFlags.Static | BindingFlags.NonPublic);
    }

    public static class Renderer2DMembers
    {
        public static readonly FieldInfo FinalBlitPass = typeof(Renderer2D).GetField("m_FinalBlitPass", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static class PostProcessPassMembers
    {
        public static readonly FieldInfo Descriptor = typeof(PostProcessPass).GetField("m_Descriptor", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static class XRSystemMembers
    {
        public static readonly FieldInfo Display = typeof(XRSystem).GetField("display", BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
