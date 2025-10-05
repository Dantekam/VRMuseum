using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TND.Upscaling.Framework
{
    [RequireComponent(typeof(Camera))]
    public abstract class UpscalerController: MonoBehaviour
    {
        [SerializeField]
        internal List<string> upscalerChain = new();
        
        [SerializeField]
        protected internal UpscalerQuality qualityMode = UpscalerQuality.Quality;
        
        [SerializeField]
        protected internal bool enableSharpening = true;

        [SerializeField, Range(0f, 1f)]
        protected internal float sharpness = 0.5f;

        [Serializable]
        internal struct UpscalerSettingsPair
        {
            public string identifier;
            public UpscalerSettingsBase settings;
        }

        [SerializeField]
        internal List<UpscalerSettingsPair> upscalerSettings = new();

        [Header("Reactive Mask")]
        [SerializeField]
        protected internal bool autoGenerateReactiveMask = true;
        
        [SerializeField]
        protected internal AutoReactiveSettings autoReactiveSettings = AutoReactiveSettings.Default;

        [SerializeField]
        protected internal bool enableCustomReactiveMask = false;
        
        [SerializeField]
        protected internal LayerMask customReactiveMaskLayer;
        
        protected internal float RenderScale => 1.0f / GetScaleFactor(qualityMode);
        
        protected internal UpscalerContext UpscalerContext { get; set; }
        
        protected internal virtual bool EnableOpaqueOnlyCopy { get; internal set; }
        protected internal virtual Texture OpaqueOnlyTexture => null;
        
        protected internal virtual bool EnableCustomReactiveMask => enableCustomReactiveMask;
        protected internal virtual Texture CustomReactiveMask => null;
        
        protected internal virtual bool ResetHistory => false;
        
        protected virtual void Awake()
        {
        }

        protected virtual void OnValidate()
        {
            var upscalerPlugins = UpscalerPluginRegistry.GetUpscalerPlugins();
            for (int i = 0; i < upscalerPlugins.Count; ++i)
            {
                var upscalerPlugin = upscalerPlugins[i];
                if (upscalerPlugin == null)
                    continue;

                // Ensure we have a settings object for each upscaler type
                if (!TryGetUpscalerSettings(upscalerPlugin, out _))
                {
                    upscalerSettings.Add(new UpscalerSettingsPair
                    {
                        identifier = upscalerPlugin.Identifier,
                        settings = upscalerPlugin.CreateSettings(),
                    });
                }
            }
        }

        protected virtual void OnEnable()
        {
            StartCoroutine(CUpdateSettingsCaches());
        }

        protected virtual void OnDisable()
        {
            StopAllCoroutines();
        }

        protected virtual void Update()
        {
        }

        protected virtual void LateUpdate()
        {
        }

        protected void ValidateQualityMode()
        {
            if (qualityMode == UpscalerQuality.Off && enabled)
            {
                enabled = false;
            }
            else if (qualityMode != UpscalerQuality.Off && !enabled)
            {
                enabled = true;
            }
        }

        protected UpscalerName GetPrimaryUpscalerName()
        {
            if (upscalerChain == null || upscalerChain.Count == 0)
                return UpscalerName.None;
            
            IUpscalerPlugin plugin = UpscalerPluginRegistry.FindUpscalerPlugin(upscalerChain[0]);
            return plugin?.Name ?? UpscalerName.None;
        }

        internal string GetPrimaryUpscalerIdentifier()
        {
            if (upscalerChain == null || upscalerChain.Count == 0)
                return null;

            return upscalerChain[0];
        }
        
        protected bool SetPrimaryUpscaler(UpscalerName upscalerName)
        {
            var upscalerPlugins = UpscalerPluginRegistry.GetUpscalerPlugins();
            for (int i = 0; i < upscalerPlugins.Count; ++i)
            {
                if (upscalerPlugins[i].Name == upscalerName)
                {
                    return SetPrimaryUpscaler(upscalerPlugins[i]);
                }
            }
            
            Debug.LogWarning($"Could not find upscaler with name: {upscalerName}");
            return false;
        }

        protected bool SetPrimaryUpscaler(IUpscalerPlugin upscalerPlugin)
        {
            if (upscalerPlugin == null)
                return false;

            string identifier = upscalerPlugin.Identifier;
            upscalerChain.Remove(identifier);
            upscalerChain.Insert(0, identifier);
            return true;
        }

        protected IUpscalerPlugin GetCurrentlyActiveUpscalerPlugin()
        {
            return UpscalerContext?.ActiveUpscalerPlugin;
        }

        protected static void ForEachUpscalerPlugin(Action<IUpscalerPlugin> callback)
        {
            var upscalerPlugins = UpscalerPluginRegistry.GetUpscalerPlugins();
            for (int i = 0; i < upscalerPlugins.Count; ++i)
            {
                callback(upscalerPlugins[i]);
            }
        }

        protected internal bool TryGetUpscalerSettings(IUpscalerPlugin upscalerPlugin, out UpscalerSettingsBase settings)
        {
            if (upscalerPlugin == null)
            {
                settings = null;
                return false;
            }

            string identifier = upscalerPlugin.Identifier;
            for (int i = 0; i < upscalerSettings.Count; ++i)
            {
                if (upscalerSettings[i].identifier == identifier)
                {
                    settings = upscalerSettings[i].settings;
                    return true;
                }
            }

            settings = null;
            return false;
        }

        internal IEnumerator<IUpscalerPlugin> EnumerateUpscalerChain()
        {
            if (upscalerChain == null)
                yield break;
            
            for (int i = 0; i < upscalerChain.Count; ++i)
            {
                IUpscalerPlugin plugin = UpscalerPluginRegistry.FindUpscalerPlugin(upscalerChain[i]);
                if (plugin != null)
                    yield return plugin;
            }
        }

        protected static float GetScaleFactor(UpscalerQuality qualityMode)
        {
            switch (qualityMode)
            {
                case UpscalerQuality.NativeAA:
                    return 1.0f;
                case UpscalerQuality.UltraQuality:
                    return 1.2f;
                case UpscalerQuality.Quality:
                    return 1.5f;
                case UpscalerQuality.Balanced:
                    return 1.7f;
                case UpscalerQuality.Performance:
                    return 2.0f;
                case UpscalerQuality.UltraPerformance:
                    return 3.0f;
                default:
                    return 1.0f;
            }
        }
        
        private IEnumerator CUpdateSettingsCaches()
        {
            WaitForEndOfFrame endOfFrame = new();
            while (true)
            {
                yield return endOfFrame;

                // Updating cached values has to happen at end-of-frame, because it's the only Unity event that is guaranteed to execute
                // after all rendering is done and after all upscaler instances are done reading their setting values.
                // Sadly Unity has no normal end-of-frame event callback function, and Awaitable.EndOfFrameAsync is only available in Unity 6+, so coroutines it is.
                for (int i = 0; i < upscalerSettings.Count; ++i)
                {
                    upscalerSettings[i].settings?.UpdateCachedValues();
                }
            }
        }

#if TND_DEBUG
        private readonly System.Text.StringBuilder _guiBuilder = new();
        
        protected virtual void OnGUI()
        {
            if (!TryGetComponent(out Camera cam) || cam != Camera.main)
            {
                return;
            }
            
            float scale = 1f;
            var screenResolution = Screen.currentResolution;
            if (screenResolution.height > 0)
            {
                scale = screenResolution.height / 720f;
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var upscalerPlugin = GetCurrentlyActiveUpscalerPlugin();
            
            _guiBuilder.Clear();
            _guiBuilder.AppendFormat(" Active upscaler: {0}\n", upscalerPlugin?.DisplayName ?? "None");
            _guiBuilder.AppendFormat(" Render pipeline: {0}\n", UnityEngine.Rendering.RenderPipelineManager.currentPipeline?.GetType().Name ?? "Built-in");
            _guiBuilder.AppendFormat(" Unity version: {0}\n", Application.unityVersion);
            _guiBuilder.AppendFormat(" Graphics API: {0}\n", SystemInfo.graphicsDeviceType);
            
            float renderScale = RenderScale;
            Vector2Int display = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            Vector2Int render = new Vector2Int((int)(display.x * renderScale), (int)(display.y * renderScale));
            _guiBuilder.AppendFormat(" Input resolution: {0}x{1}\n", render.x, render.y);
            _guiBuilder.AppendFormat(" Output resolution: {0}x{1}\n", display.x, display.y);
            
            GUILayout.Label(_guiBuilder.ToString());
        }
#endif
    }
}
