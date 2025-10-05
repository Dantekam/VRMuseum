using System;
using System.Collections.Generic;
using UnityEngine;

namespace TND.Upscaling.Framework
{
    public static class UpscalerPluginRegistry
    {
        private static readonly List<IUpscalerPlugin> UpscalerPlugins = new();
        public static IReadOnlyList<IUpscalerPlugin> GetUpscalerPlugins() => UpscalerPlugins;
        
        public static bool AnySupported()
        {
            foreach (IUpscalerPlugin upscalerPlugin in UpscalerPlugins)
            {
                if (upscalerPlugin != null && upscalerPlugin.IsSupported)
                    return true;
            }

            return false;
        }
        
        public static void Cleanup()
        {
            foreach (IUpscalerPlugin upscalerPlugin in UpscalerPlugins)
            {
                upscalerPlugin?.Cleanup();
            }
        }
        
        public static void RegisterUpscalerPlugin(IUpscalerPlugin upscalerPlugin)
        {
            if (upscalerPlugin == null)
                return;

            // Ensure we don't add any duplicate upscaler plugins
            Type upscalerPluginType = upscalerPlugin.GetType();
            for (int i = 0; i < UpscalerPlugins.Count; ++i)
            {
                IUpscalerPlugin plugin = UpscalerPlugins[i];
                if (plugin.GetType() == upscalerPluginType)
                    return;
                
                // Allow upscalers with the same Name to override each other, based on priority
                if (plugin.Name == upscalerPlugin.Name)
                {
                    if (upscalerPlugin.Priority > plugin.Priority)
                    {
                        UpscalerDebug.Log($"Registering upscaler plugin: {upscalerPlugin.DisplayName}");
                        UpscalerPlugins[i] = upscalerPlugin;
                        UpscalerPlugins.Sort();
                    }

                    return;
                }
            }
            
            UpscalerDebug.Log($"Registering upscaler plugin: {upscalerPlugin.DisplayName}");
            UpscalerPlugins.Add(upscalerPlugin);
            UpscalerPlugins.Sort();
        }

        public static IUpscalerPlugin FindUpscalerPlugin(string upscalerPluginIdentifier)
        {
            foreach (IUpscalerPlugin plugin in UpscalerPlugins)
            {
                if (plugin.Identifier == upscalerPluginIdentifier)
                {
                    return plugin;
                }
            }

            return null;
        }
        
        public static IUpscalerPlugin FindUpscalerPlugin(UpscalerName upscalerName)
        {
            foreach (IUpscalerPlugin plugin in UpscalerPlugins)
            {
                if (plugin.Name == upscalerName)
                {
                    return plugin;
                }
            }

            return null;
        }
    }
}
