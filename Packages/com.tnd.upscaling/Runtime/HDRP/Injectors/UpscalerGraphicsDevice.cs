using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework
{
    public abstract class UpscalerGraphicsDeviceBase<TDevice, TContext>
        where TDevice: UpscalerGraphicsDeviceBase<TDevice, TContext>, new()
        where TContext: UpscalerContext, new()
    {
        protected static TDevice s_graphicsDevice;
        
        protected readonly List<TContext> _contexts = new();
        
        protected UpscalerGraphicsDeviceBase()
        {
#if UNITY_EDITOR
            // Ensure resources are properly cleaned up during an in-editor domain reload
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Destroy;
#endif
        }
        
        public static TDevice CreateGraphicsDevice()
        {
            if (s_graphicsDevice != null)
            {
                s_graphicsDevice.Destroy();
                s_graphicsDevice = null;
            }

            var graphicsDevice = new TDevice();
            if (graphicsDevice.Initialize())
            {
                s_graphicsDevice = graphicsDevice;
                return graphicsDevice;
            }
            
            Debug.LogWarning("Failed to initialize TND Upscaler Graphics Device");
            return null;
        }
        
        private bool Initialize()
        {
            return UpscalerPluginRegistry.AnySupported();
        }
        
        private void Destroy()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Destroy;
#endif

            CommandBuffer cmd = new CommandBuffer();
            
            foreach (var context in _contexts)
            {
                context.Destroy(cmd);
            }
            
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
            
            _contexts.Clear();
            
            UpscalerPluginRegistry.Cleanup();
        }
        
        public TContext CreateFeature(CommandBuffer cmd, in UpscalerInitParams initParams)
        {
            var context = new TContext();
            if (!context.Initialize(cmd, initParams))
                return null;
            
            _contexts.Add(context);
            return context;
        }

        public void DestroyFeature(CommandBuffer cmd, TContext context)
        {
            if (context == null)
                return;
            
            context.Destroy(cmd);
            _contexts.Remove(context);
        }
    }
}
