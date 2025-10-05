using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace TND.Upscaling.Framework.BIRP
{
    [RequireComponent(typeof(Camera), typeof(UpscalerController))]
    [AddComponentMenu("")]
    public class ImageEffectUpscaler: MonoBehaviour
    {
        private Camera _camera;
        private UpscalerController _upscalerController;
        private readonly List<UpscalerContext> _upscalerContexts = new();
        
        // TODO: camera targetTexture
        private DepthTextureMode _originalDepthTextureMode;
        private Rect _originalRect;
        
        private Vector2Int _maxRenderSize;
        private Vector2Int _displaySize;
        private Vector2 _jitterOffset;
        private int _historyResetFrame;
        
        private Vector2Int _prevDisplaySize;
        private UpscalerQuality _prevQualityMode;
        private bool _prevStereo;
        private bool _prevHDR;
        
        private CommandBuffer _dispatchCommandBuffer;
        private CommandBuffer _opaqueCopyCommandBuffer;
        private RenderTexture _colorOpaqueOnly;
        private RenderTexture _upscalerOutput;

        private Shader _blitShader;
        private Material _blitMaterial;
        private MaterialPropertyBlock _blitProperties;
        
        private RenderTexture _tempColor;
        private RenderTexture _tempDepth;
        private RenderTexture _tempMotion;
        private Material _copyDepthMaterial;

        private readonly TextureRef.BlitterDelegate _inputColorBlitter;
        private readonly TextureRef.BlitterDelegate _inputDepthBlitter;
        private readonly TextureRef.BlitterDelegate _inputMotionBlitter;

        internal Vector2Int MaxRenderSize => _maxRenderSize;
        internal Vector2Int DisplaySize => _displaySize;

        public ImageEffectUpscaler()
        {
            _inputColorBlitter = BlitColor;
            _inputDepthBlitter = BlitDepth;
            _inputMotionBlitter = BlitMotion;
        }
        
        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _upscalerController = GetComponent<UpscalerController>();
            _blitProperties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            UpscalerUtils.TryLoadMaterial("TND_Blit", ref _blitMaterial, ref _blitShader);
            
            _copyDepthMaterial = new Material(Shader.Find("Hidden/BlitCopyWithDepth"));
            
            _originalDepthTextureMode = _camera.depthTextureMode;
            _camera.depthTextureMode = _originalDepthTextureMode | DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            
            CreateCommandBuffers();
            
            _displaySize = GetDisplaySize();
            _maxRenderSize = CalculateMaxRenderSize();
            
            _prevQualityMode = _upscalerController.qualityMode;
            _prevDisplaySize = _displaySize;
            _prevStereo = _camera.stereoEnabled;
            _prevHDR = _camera.allowHDR;
            
            if (!ValidateSize())
            {
                return;
            }
            
            int numViews = _camera.stereoEnabled ? 2 : 1;
            
            _upscalerContexts.Clear();
            for (int view = 0; view < numViews; ++view)
            {
                _upscalerContexts.Add(new UpscalerContext());
            }

            UpscalerInitParams initParams = new()
            {
                camera = _camera,
                maxRenderSize = _maxRenderSize,
                upscaleSize = _displaySize,
                useTextureArrays = false,   // TODO: true for single-pass instanced left eye
                numTextureSlices = 1,
                enableHDR = _camera.allowHDR,
                invertedDepth = SystemInfo.usesReversedZBuffer,
                highResMotionVectors = false,
                jitteredMotionVectors = false,
            };
            
            UpscalerUtils.RunWithCommandBuffer(cmd =>
            {
                for (int view = 0; view < numViews; ++view)
                {
                    _upscalerContexts[view].Initialize(cmd, initParams);
                }
            });
        }

        private void OnDisable()
        {
            UpscalerUtils.RunWithCommandBuffer(cmd =>
            {
                foreach (var upscalerContext in _upscalerContexts)
                {
                    upscalerContext.Destroy(cmd);
                }
                
                _upscalerContexts.Clear();
            });
                
            DestroyCommandBuffers();
            
            UpscalerUtils.DestroyRenderTexture(ref _upscalerOutput);
            UpscalerUtils.DestroyRenderTexture(ref _tempColor);
            UpscalerUtils.DestroyRenderTexture(ref _tempDepth);
            UpscalerUtils.DestroyRenderTexture(ref _tempMotion);

            _camera.depthTextureMode = _originalDepthTextureMode;
            
            UpscalerUtils.DestroyObject(_copyDepthMaterial);
            
            UpscalerUtils.UnloadMaterial(ref _blitMaterial, ref _blitShader);
        }

        private void Update()
        {
            _displaySize = GetDisplaySize();
            if (_displaySize != _prevDisplaySize || _upscalerController.qualityMode != _prevQualityMode || _camera.allowHDR != _prevHDR || _camera.stereoEnabled != _prevStereo)
            {
                OnDisable();
                OnEnable();
            }
        }

        private void LateUpdate()
        {
            // Remember the original camera viewport before we modify it in OnPreCull
            _originalRect = _camera.rect;
        }

        public void ResetHistory()
        {
            _historyResetFrame = Time.frameCount;
        }
        
        private void OnPreCull()
        {
            if (!ValidateSize())
            {
                return;
            }
            
            // Render to a smaller portion of the screen by manipulating the camera's viewport rect
            _camera.aspect = (float)_displaySize.x / _displaySize.y;
            _camera.rect = new Rect(0, 0, _originalRect.width * _maxRenderSize.x / _displaySize.x, _originalRect.height * _maxRenderSize.y / _displaySize.y);

            // Set up the opaque-only command buffer to make a copy of the camera color buffer right before transparent drawing starts 
            _opaqueCopyCommandBuffer.Clear();
            if (_upscalerController.EnableOpaqueOnlyCopy)
            {
                var scaledRenderSize = GetScaledRenderSize();
                _colorOpaqueOnly = RenderTexture.GetTemporary(scaledRenderSize.x, scaledRenderSize.y, 0, GetDefaultFormat());	// TODO: temporary array when SPI
                _opaqueCopyCommandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, _colorOpaqueOnly);
            }

            ApplyJitter();
        }

        private static readonly int MainTexPropertyID = Shader.PropertyToID("_MainTex");
        private static readonly int DepthTexPropertyID = Shader.PropertyToID("_DepthTex");
        private static readonly int ScaleBiasPropertyID = Shader.PropertyToID("_BlitScaleBias");

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!ValidateSize())
            {
                Graphics.Blit(source, destination);
                return;
            }

            UpscalerContext upscalerContext = GetActiveUpscalerContext();

            // Restore the camera's viewport rect so we can output at full resolution
            _camera.rect = _originalRect;
            _camera.ResetProjectionMatrix();

            _dispatchCommandBuffer.Clear();
            
            // The backbuffer is not set up to allow random-write access, so we need a temporary render texture for upscaling to output to
            var upscaler = upscalerContext.ActiveUpscaler;
            bool requireIntermediateOutput = (upscaler != null && upscaler.RequiresRandomWriteOutput) || destination == null || _camera.stereoEnabled;
            if (requireIntermediateOutput && (_upscalerOutput == null || _upscalerOutput.width != _displaySize.x || _upscalerOutput.height != _displaySize.y))
            {
                UpscalerUtils.DestroyRenderTexture(ref _upscalerOutput);
                _upscalerOutput = UpscalerUtils.CreateRenderTexture("_UpscalerOutput", _displaySize, GetDefaultFormat(), true);
            }
            else if (!requireIntermediateOutput && _upscalerOutput != null)
            {
                UpscalerUtils.DestroyRenderTexture(ref _upscalerOutput);
            }

            bool isSrgb = UpscalerUtils.IsSRGBFormat(SystemInfo.GetGraphicsFormat(DefaultFormat.LDR));  // TODO: can cache this somewhere
            var colorFormat = UpscalerUtils.GetGraphicsFormat(GetDefaultFormat(), isSrgb);
            var inputColorDesc = new RenderTextureDescriptor(_maxRenderSize.x, _maxRenderSize.y, colorFormat, GraphicsFormat.None);
            var inputDepthDesc = new RenderTextureDescriptor(_maxRenderSize.x, _maxRenderSize.y, GraphicsFormat.None, GraphicsFormat.D32_SFloat);
            var inputMotionDesc = new RenderTextureDescriptor(_maxRenderSize.x, _maxRenderSize.y, GraphicsFormat.R16G16_SFloat, GraphicsFormat.None);
            
            TextureRef inputColor = new TextureRef(source, inputColorDesc, _inputColorBlitter);
            TextureRef inputOpaque = new TextureRef(_colorOpaqueOnly);
            TextureRef inputDepth = new TextureRef(GetDepthTexture(), inputDepthDesc, _inputDepthBlitter, RenderTextureSubElement.Depth);
            TextureRef inputMotion = new TextureRef(new RenderTargetIdentifier(BuiltinRenderTextureType.MotionVectors), inputMotionDesc, _inputMotionBlitter);
            if (_camera.stereoEnabled)
            {
                // Create properly sized copies of the input buffers, as BiRP refuses to render them at lower resolution when XR is active
                inputColor = new TextureRef(inputColor.GetTexture(_dispatchCommandBuffer)); // TODO: use BlitColorTexture with depth slice = view index, batch temp textures together like in URP
                inputOpaque = new TextureRef(inputOpaque.GetTexture(_dispatchCommandBuffer));
                inputDepth = new TextureRef(inputDepth.GetTexture(_dispatchCommandBuffer)); // TODO: we need a BlitDepth that allows texture array and depth slice input, BlitCopyWithDepth can't do that :(
                inputMotion = new TextureRef(inputMotion.GetTexture(_dispatchCommandBuffer));
            }
            
            var scaledRenderSize = GetScaledRenderSize();
            UpscalerDispatchParams dispatchParams = new()
            {
                nonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(_camera.nonJitteredProjectionMatrix, SystemInfo.graphicsUVStartsAtTop && destination != null),    // TODO: _originalRenderTarget
                viewIndex = 0,
                inputColor = inputColor,
                inputDepth = inputDepth,
                inputMotionVectors = inputMotion,
                inputExposure = TextureRef.Null,
                inputReactiveMask = TextureRef.Null,
                inputOpaqueOnly = inputOpaque,
                outputColor = requireIntermediateOutput ? new TextureRef(_upscalerOutput) : new TextureRef(destination),
                renderSize = scaledRenderSize,
                motionVectorScale = -scaledRenderSize,
                jitterOffset = _jitterOffset,
                preExposure = 1.0f,
                enableSharpening = _upscalerController.enableSharpening,
                sharpness = _upscalerController.sharpness,
                resetHistory = _historyResetFrame == Time.frameCount,
            };

            upscalerContext.Execute(_dispatchCommandBuffer, ref dispatchParams);

            // Output the upscaled image
            // if (_originalRenderTarget != null)   // TODO: render texture support
            // {
            //     // Output to the camera target texture, passing through depth as well    // TODO: do we want a depth passthrough/upsample?
            //     _dispatchCommandBuffer.SetGlobalTexture("_DepthTex", GetDepthTexture(), RenderTextureSubElement.Depth);
            //     _dispatchCommandBuffer.Blit(Fsr3ShaderIDs.UavUpscaledOutput, _originalRenderTarget, _copyWithDepthMaterial);
            // }
            // else
            if (requireIntermediateOutput)
            {
                bool isRenderToBackBufferTarget = _camera.cameraType != CameraType.SceneView && (destination == null || _camera.stereoEnabled);
                bool yFlip = isRenderToBackBufferTarget && _camera.targetTexture == null && SystemInfo.graphicsUVStartsAtTop;
                Vector4 scaleBias = yFlip ? new Vector4(1, -1, 0, 1) : new Vector4(1, 1, 0, 0);
                
                _blitProperties.SetTexture(MainTexPropertyID, _upscalerOutput);
                _blitProperties.SetVector(ScaleBiasPropertyID, scaleBias);

                // Output directly to the backbuffer
                _dispatchCommandBuffer.SetRenderTarget(destination, destination, 0, CubemapFace.Unknown, 0);    // TODO: depth slice = view index
                _dispatchCommandBuffer.DrawProcedural(Matrix4x4.identity, _blitMaterial, 0, MeshTopology.Triangles, 3, 1, _blitProperties);
            }

            Graphics.ExecuteCommandBuffer(_dispatchCommandBuffer);

            if (_colorOpaqueOnly != null)
            {
                RenderTexture.ReleaseTemporary(_colorOpaqueOnly);
                _colorOpaqueOnly = null;
            }

            // Silence the Unity warning about not writing to the destination texture 
            RenderTexture.active = destination;
        }

        private void ApplyJitter()
        {
            UpscalerContext upscalerContext = GetActiveUpscalerContext();
            
            var scaledRenderSize = GetScaledRenderSize();
            var upscalerPlugin = upscalerContext.ActiveUpscalerPlugin;
            var upscaler = upscalerContext.ActiveUpscaler;
            
            if (upscalerPlugin != null && upscalerPlugin.IsTemporalUpscaler)
            {
                if (upscaler != null)
                {
                    // Allow upscalers to provide a custom jitter pattern
                    _jitterOffset = upscaler.GetJitterOffset(Time.frameCount, scaledRenderSize.x, _displaySize.x);
                }
                else
                {
                    // Fallback jitter pattern
                    int index = Time.frameCount % 1024;
                    _jitterOffset = new Vector2(
                        UpscalerBase.Halton(index + 1, 2) - 0.5f,
                        UpscalerBase.Halton(index + 1, 3) - 0.5f);
                }
            }
            else
            {
                // Spatial upscalers don't need any camera jitter, make this a noop
                _jitterOffset = Vector2.zero;
            }

            float jitterX = 2.0f * _jitterOffset.x / scaledRenderSize.x;
            float jitterY = 2.0f * _jitterOffset.y / scaledRenderSize.y;

            var jitterTranslationMatrix = Matrix4x4.Translate(new Vector3(jitterX, jitterY, 0));
            _camera.nonJitteredProjectionMatrix = _camera.projectionMatrix;
            _camera.projectionMatrix = jitterTranslationMatrix * _camera.nonJitteredProjectionMatrix;
            _camera.useJitteredProjectionMatrixForTransparentRendering = true;
        }

        private void CreateCommandBuffers()
        {
            _dispatchCommandBuffer = new CommandBuffer { name = "[Upscaler] Upscaling Pass" };
            _opaqueCopyCommandBuffer = new CommandBuffer { name = "[Upscaler] Opaque-Only Copy" };
            _camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _opaqueCopyCommandBuffer);
        }

        private void DestroyCommandBuffers()
        {
            if (_opaqueCopyCommandBuffer != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _opaqueCopyCommandBuffer);
                _opaqueCopyCommandBuffer.Release();
                _opaqueCopyCommandBuffer = null;
            }

            if (_dispatchCommandBuffer != null)
            {
                _dispatchCommandBuffer.Release();
                _dispatchCommandBuffer = null;
            }
        }

        private UpscalerContext GetActiveUpscalerContext()
        {
            int view = _camera.stereoEnabled ? (int)_camera.stereoActiveEye : 0;
            return _upscalerContexts[view];
        }
        
        private RenderTextureFormat GetDefaultFormat()
        {
            // TODO: render texture support
            // if (_originalRenderTarget != null)
            //     return _originalRenderTarget.format;

            return _camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        }
        
        private BuiltinRenderTextureType GetDepthTexture()
        {
            return _camera.actualRenderingPath is RenderingPath.Forward or RenderingPath.VertexLit ? BuiltinRenderTextureType.Depth : BuiltinRenderTextureType.CameraTarget;
        }
        
        private Vector2Int GetDisplaySize()
        {
            // TODO: render texture support
            // if (_originalRenderTarget != null)
            //     return new Vector2Int(_originalRenderTarget.width, _originalRenderTarget.height);
            
#if ENABLE_VR && ENABLE_XR_MODULE
            if (_camera.stereoEnabled)
            {
                return new Vector2Int(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight);
            }
#endif
            
            return new Vector2Int(_camera.pixelWidth, _camera.pixelHeight);
        }
        
        private Vector2Int CalculateMaxRenderSize()
        {
            float renderScale = _upscalerController.RenderScale;
            return new Vector2Int((int)(_displaySize.x * renderScale), (int)(_displaySize.y * renderScale));
        }
        
        private bool UsingDynamicResolution()
        {
            return _camera.allowDynamicResolution;// || (_originalRenderTarget != null && _originalRenderTarget.useDynamicScale);   // TODO: render texture support
        }
        
        private Vector2Int GetScaledRenderSize()
        {
            if (UsingDynamicResolution())
                return new Vector2Int(Mathf.CeilToInt(_maxRenderSize.x * ScalableBufferManager.widthScaleFactor), Mathf.CeilToInt(_maxRenderSize.y * ScalableBufferManager.heightScaleFactor));
            
            return _maxRenderSize;
        }

        private bool ValidateSize()
        {
            return _displaySize.x > 0 && _displaySize.y > 0 && _maxRenderSize.x > 0 && _maxRenderSize.y > 0;
        }
        
        private static Texture BlitColorTexture(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source, ref RenderTexture texture, string name, int sourceDepthSlice = 0)
        {
            if (!AllocateRenderTexture(ref texture, desc, name))
            {
                return null;
            }
            
            cmd.Blit(source, texture, sourceDepthSlice, 0);
            return texture;
        }
        
        private Texture BlitColor(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source) => BlitColorTexture(cmd, desc, source, ref _tempColor, "_TempColor");
        private Texture BlitMotion(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source) => BlitColorTexture(cmd, desc, source, ref _tempMotion, "_TempMotion");

        private Texture BlitDepth(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source)
        {
            if (!AllocateRenderTexture(ref _tempDepth, desc, "_TempDepth"))
            {
                return null;
            }
            
            cmd.SetGlobalTexture(DepthTexPropertyID, source, RenderTextureSubElement.Depth);
            cmd.Blit(source, _tempDepth, _copyDepthMaterial);
            return _tempDepth;
        }
        
        private static bool AllocateRenderTexture(ref RenderTexture rt, in RenderTextureDescriptor desc, string name)
        {
            if (rt == null || !rt.IsCreated() ||
                rt.width != desc.width || rt.height != desc.height || 
                rt.graphicsFormat != desc.graphicsFormat || rt.depthStencilFormat != desc.depthStencilFormat ||
                rt.enableRandomWrite != desc.enableRandomWrite)
            {
                if (rt != null)
                {
                    rt.Release();
                }

                rt = new RenderTexture(desc.width, desc.height, desc.graphicsFormat, desc.depthStencilFormat) { name = name, enableRandomWrite = desc.enableRandomWrite };
                return rt.Create();
            }

            return true;
        }
    }
}
