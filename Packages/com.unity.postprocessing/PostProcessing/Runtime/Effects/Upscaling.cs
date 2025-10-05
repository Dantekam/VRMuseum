using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using TND.Upscaling.Framework;

namespace UnityEngine.Rendering.PostProcessing
{
    [Scripting.Preserve]
    [Serializable]
    public class Upscaling
    {
        public Vector2 Jitter { get; private set; }
        public Vector2 JitterOffset { get; private set; }
        public Vector2Int MaxRenderSize => _maxRenderSize;
        public Vector2Int DisplaySize => _displaySize;
        public RenderTargetIdentifier ColorOpaqueOnly { get; set; }
        public float RenderScale => GetUpscalerContext()?.UpscalerController?.RenderScale ?? 1.0f;
        public bool EnableOpaqueOnlyCopy => GetUpscalerContext()?.UpscalerController?.EnableOpaqueOnlyCopy ?? false;
        public bool RequiresRandomWriteOutput => GetUpscalerContext()?.ActiveUpscaler?.RequiresRandomWriteOutput ?? true;

        private readonly List<UpscalerContext> _upscalerContexts = new();
        
        private Vector2Int _maxRenderSize;
        private Vector2Int _displaySize;
        private bool _resetHistory;
        private bool _useOutputCopy;

        private Vector2Int _prevMaxRenderSize;
        private Vector2Int _prevDisplaySize;
        private bool _prevStereo;
        private bool _prevHDR;

        private Rect _originalRect;

        private RenderTexture _tempColor;
        private RenderTexture _tempDepth;
        private RenderTexture _tempMotion;
        private RenderTexture _tempOpaque;
        private RenderTexture _tempOutput;

        private readonly TextureRef.BlitterDelegate _inputColorBlitter;
        private readonly TextureRef.BlitterDelegate _inputDepthBlitter;
        private readonly TextureRef.BlitterDelegate _inputMotionBlitter;
        private readonly TextureRef.BlitterDelegate _inputOpaqueBlitter;
        private readonly TextureRef.BlitterDelegate _outputColorBlitter;

        public Upscaling()
        {
            _inputColorBlitter = BlitColor;
            _inputDepthBlitter = BlitDepth;
            _inputMotionBlitter = BlitMotion;
            _inputOpaqueBlitter = BlitOpaque;
            _outputColorBlitter = BlitOutput;
        }
        
        public bool IsSupported()
        {
            return UpscalerPluginRegistry.AnySupported();
        }
        
        public DepthTextureMode GetCameraFlags()
        {
            return DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }
        
        public void Release()
        {
            UpscalerUtils.RunWithCommandBuffer(commandBuffer =>
            {
                foreach (var upscalerContext in _upscalerContexts)
                {
                    upscalerContext.Destroy(commandBuffer);
                }
            });
            
            _upscalerContexts.Clear();

            UpscalerUtils.DestroyRenderTexture(ref _tempColor);
            UpscalerUtils.DestroyRenderTexture(ref _tempDepth);
            UpscalerUtils.DestroyRenderTexture(ref _tempMotion);
            UpscalerUtils.DestroyRenderTexture(ref _tempOpaque);
            UpscalerUtils.DestroyRenderTexture(ref _tempOutput);
        }

        public void ResetHistory()
        {
            _resetHistory = true;
        }

        public void ConfigureJitteredProjectionMatrix(PostProcessRenderContext context)
        {
            ApplyJitter(context.camera, context.stereoRenderingMode == PostProcessRenderContext.StereoRenderingMode.SinglePassInstanced, context.xrActiveEye, context.numberOfEyes);
        }

        public void ConfigureCameraViewport(PostProcessRenderContext context)
        {
            var camera = context.camera;
            if (context.xrActiveEye == 0)
            {
                // Run this only once for the first view
                _originalRect = camera.rect;
                _displaySize = new Vector2Int(context.screenWidth, context.screenHeight);
                _maxRenderSize = _displaySize;
            }

            UpscalerController upscalerController = GetUpscalerContext(context.xrActiveEye)?.UpscalerController;
            if (upscalerController != null || context.camera.TryGetComponent(out upscalerController))
            {
                float renderScale = upscalerController.RenderScale;

                // Determine the desired rendering resolution
                _maxRenderSize = new Vector2Int((int)(_displaySize.x * renderScale), (int)(_displaySize.y * renderScale));

                // Render to a smaller portion of the screen by manipulating the camera's viewport rect
                camera.aspect = (float)_displaySize.x / _displaySize.y;
                camera.rect = new Rect(0, 0, _originalRect.width * _maxRenderSize.x / _displaySize.x, _originalRect.height * _maxRenderSize.y / _displaySize.y);
            }
        }

        public void ResetCameraViewport(PostProcessRenderContext context)
        {
            context.camera.rect = _originalRect;
        }

        public void Render(PostProcessRenderContext context)
        {
            var cmd = context.command;
            var camera = context.camera;
            int passIndex = context.xrActiveEye;
            int viewsPerPass = context.numberOfEyes;

            if (_maxRenderSize != _prevMaxRenderSize || _displaySize != _prevDisplaySize || camera.stereoEnabled != _prevStereo || camera.allowHDR != _prevHDR)
            {
                Release();
            }

            int totalViews = context.stereoActive ? 2 : 1;
            if (_upscalerContexts.Count < totalViews)
            {
                var initParams = new UpscalerInitParams
                {
                    camera = camera,
                    maxRenderSize = _maxRenderSize,
                    upscaleSize = _displaySize,
                    useTextureArrays = false,
                    numTextureSlices = 1,
                    enableHDR = context.camera.allowHDR,
                    invertedDepth = SystemInfo.usesReversedZBuffer,
                    highResMotionVectors = false,
                    jitteredMotionVectors = false,
                };

                for (int i = _upscalerContexts.Count; i < totalViews; ++i)
                {
                    var upscalerContext = new UpscalerContext();
                    _upscalerContexts.Add(upscalerContext);
                    upscalerContext.Initialize(context.command, initParams);
                }

                _prevMaxRenderSize = _maxRenderSize;
                _prevDisplaySize = _displaySize;
                _prevStereo = camera.stereoEnabled;
                _prevHDR = camera.allowHDR;
            }
            
            bool isSrgb = UpscalerUtils.IsSRGBFormat(SystemInfo.GetGraphicsFormat(DefaultFormat.LDR));  // TODO: can cache this somewhere, should also probably check camera allowHDR
            var colorFormat = UpscalerUtils.GetGraphicsFormat(context.sourceFormat, isSrgb);
            var inputColorDesc = new RenderTextureDescriptor(_maxRenderSize.x, _maxRenderSize.y, colorFormat, GraphicsFormat.None);
            var inputDepthDesc = new RenderTextureDescriptor(_maxRenderSize.x, _maxRenderSize.y, GraphicsFormat.R32_SFloat, GraphicsFormat.None);
            var inputMotionDesc = new RenderTextureDescriptor(_maxRenderSize.x, _maxRenderSize.y, GraphicsFormat.R16G16_SFloat, GraphicsFormat.None);
            var outputColorDesc = new RenderTextureDescriptor(_displaySize.x, _displaySize.y, colorFormat, GraphicsFormat.None) { enableRandomWrite = RequiresRandomWriteOutput };
            
            var renderSize = GetScaledRenderSize(camera);

            for (int view = 0; view < viewsPerPass; ++view)
            {
                int viewIndex = passIndex * viewsPerPass + view;
                var upscalerContext = GetUpscalerContext(viewIndex);

                TextureRef inputColor = new TextureRef(context.source, inputColorDesc, _inputColorBlitter);
                TextureRef inputOpaque = new TextureRef(ColorOpaqueOnly, inputColorDesc, _inputOpaqueBlitter);
                TextureRef inputDepth = new TextureRef(GetDepthTexture(camera), inputDepthDesc, _inputDepthBlitter, RenderTextureSubElement.Depth);
                TextureRef inputMotion = new TextureRef(new RenderTargetIdentifier(BuiltinRenderTextureType.MotionVectors), inputMotionDesc, _inputMotionBlitter);
                if (context.stereoActive)
                {
                    // Create properly sized copies of the input buffers, as BiRP refuses to render them at lower resolution when XR is active
                    if (context.stereoRenderingMode == PostProcessRenderContext.StereoRenderingMode.SinglePassInstanced)
                    {
                        // TODO: use single texture copies for single-pass instanced because BiRP makes a complete mess of texture arrays
                        inputColor = new TextureRef(inputColor.GetTexture(cmd));
                        inputOpaque = new TextureRef(inputOpaque.GetTexture(cmd));
                    }
                    inputDepth = new TextureRef(inputDepth.GetTexture(cmd));
                    inputMotion = new TextureRef(inputMotion.GetTexture(cmd));
                }

                Matrix4x4 nonJitteredProjectionMatrix =
                    context.stereoActive ? camera.GetStereoNonJitteredProjectionMatrix((Camera.StereoscopicEye)context.xrActiveEye) : camera.nonJitteredProjectionMatrix;

                var dispatchParams = new UpscalerDispatchParams
                {
                    nonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(nonJitteredProjectionMatrix, SystemInfo.graphicsUVStartsAtTop && camera.targetTexture != null),
                    viewIndex = view,
                    inputColor = inputColor,
                    inputDepth = inputDepth,
                    inputMotionVectors = inputMotion,
                    inputExposure = TextureRef.Null,
                    inputReactiveMask = TextureRef.Null, // PPV2 does not provide a reactive mask of its own
                    inputOpaqueOnly = inputOpaque,
                    outputColor = new TextureRef(context.destination, outputColorDesc, _outputColorBlitter),
                    renderSize = renderSize,
                    motionVectorScale = -renderSize,
                    jitterOffset = JitterOffset,
                    preExposure = 1.0f,
                    enableSharpening = false,
                    sharpness = 0.0f,
                    resetHistory = _resetHistory,
                };

                UpscalerController upscalerController = upscalerContext.UpscalerController;
                if (upscalerController != null)
                {
                    dispatchParams.enableSharpening = upscalerController.enableSharpening;
                    dispatchParams.sharpness = upscalerController.sharpness;
                    dispatchParams.resetHistory = dispatchParams.resetHistory || upscalerController.ResetHistory;
                }

                _useOutputCopy = false;

                upscalerContext.Execute(cmd, ref dispatchParams);

                if (_useOutputCopy)
                {
                    // Upscaler plugin indicated that it wrote to the intermediate output texture, so we need to blit that to the post-process destination
                    cmd.BlitFullscreenTriangle(_tempOutput, context.destination); // TODO: will need to figure out what destination is (texture array?) in SPI
                }
            }

            _resetHistory = false;
        }
        
        private void ApplyJitter(Camera camera, bool singlePassInstanced, int passIndex, int viewsPerPass)
        {
            var renderSize = GetScaledRenderSize(camera);
            var upscalerPlugin = GetUpscalerContext()?.ActiveUpscalerPlugin;
            var upscaler = GetUpscalerContext()?.ActiveUpscaler;
            
            if (upscalerPlugin != null && upscalerPlugin.IsTemporalUpscaler)
            {
                if (upscaler != null)
                {
                    // Allow upscalers to provide a custom jitter pattern
                    JitterOffset = upscaler.GetJitterOffset(Time.frameCount, renderSize.x, _displaySize.x);
                }
                else
                {
                    int jitterPhaseCount = GetJitterPhaseCount(renderSize.x, _displaySize.x);
                    JitterOffset = GetJitterOffset(Time.frameCount, jitterPhaseCount);
                }
            }
            else
            {
                // Spatial upscalers don't need any camera jitter, make this a noop
                JitterOffset = Vector2.zero;
            }
            
            float jitterX = 2.0f * JitterOffset.x / renderSize.x;
            float jitterY = 2.0f * JitterOffset.y / renderSize.y;

            var jitterTranslationMatrix = Matrix4x4.Translate(new Vector3(jitterX, jitterY, 0));

            if (singlePassInstanced)
            {
                for (int view = 0; view < viewsPerPass; ++view)
                {
                    int viewIndex = passIndex * viewsPerPass + view;
            
                    Camera.StereoscopicEye eye = (Camera.StereoscopicEye)viewIndex;
                    Matrix4x4 projectionMatrix = camera.GetStereoProjectionMatrix(eye);
                    camera.CopyStereoDeviceProjectionMatrixToNonJittered(eye);
                    camera.SetStereoProjectionMatrix(eye, jitterTranslationMatrix * projectionMatrix);
                }
            }
            else
            {
                camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
                camera.projectionMatrix = jitterTranslationMatrix * camera.nonJitteredProjectionMatrix;
                camera.useJitteredProjectionMatrixForTransparentRendering = true;
            }

            Jitter = new Vector2(jitterX, jitterY);
        }
        
        private UpscalerContext GetUpscalerContext(int viewIndex = 0)
        {
            if (viewIndex < 0 || viewIndex >= _upscalerContexts.Count)
                return null;

            return _upscalerContexts[viewIndex];
        }

        private Texture BlitColorTexture(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source, ref RenderTexture texture, string name)
        {
            if (!AllocateRenderTexture(ref texture, desc, name))
            {
                return null;
            }
            
            cmd.BlitFullscreenTriangle(source, texture);
            return texture;
        }

        private Texture BlitColor(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source) => BlitColorTexture(cmd, desc, source, ref _tempColor, "_TempColor");
        private Texture BlitMotion(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source) => BlitColorTexture(cmd, desc, source, ref _tempMotion, "_TempMotion");
        private Texture BlitOpaque(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source) => BlitColorTexture(cmd, desc, source, ref _tempOpaque, "_TempOpaque");
        
        private Texture BlitDepth(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source)
        {
            if (!AllocateRenderTexture(ref _tempDepth, desc, "_TempDepth"))
            {
                return null;
            }
            
            // TODO: this works, but now depth isn't stored in the Depth sub-element anymore. Need to make sure this works well with all native plugin upscalers. 
            cmd.SetRenderTarget(_tempDepth);
            cmd.SetGlobalTexture(ShaderIDs.MainTex, source, RenderTextureSubElement.Depth);
            cmd.DrawMesh(RuntimeUtilities.fullscreenTriangle, Matrix4x4.identity, RuntimeUtilities.copyMaterial, 0, 0);
            return _tempDepth;
        }
        
        private Texture BlitOutput(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source)
        {
            if (!AllocateRenderTexture(ref _tempOutput, desc, "_TempOutput"))
            {
                return null;
            }

            // Trigger a blit from this texture to the post-process destination after the upscaler is done
            _useOutputCopy = true;
            return _tempOutput;
        }
        
        private Vector2Int GetScaledRenderSize(Camera camera)
        {
            if (!RuntimeUtilities.IsDynamicResolutionEnabled(camera))
                return _maxRenderSize;

            return new Vector2Int(Mathf.CeilToInt(_maxRenderSize.x * ScalableBufferManager.widthScaleFactor), Mathf.CeilToInt(_maxRenderSize.y * ScalableBufferManager.heightScaleFactor));
        }

        private static BuiltinRenderTextureType GetDepthTexture(Camera cam)
        {
            return cam.actualRenderingPath is RenderingPath.Forward or RenderingPath.VertexLit ? BuiltinRenderTextureType.Depth : BuiltinRenderTextureType.CameraTarget;
        }
        
        private static int GetJitterPhaseCount(int renderWidth, int displayWidth)
        {
            const float basePhaseCount = 8.0f;
            int jitterPhaseCount = (int)(basePhaseCount * Mathf.Pow((float)displayWidth / renderWidth, 2.0f));
            return jitterPhaseCount;
        }

        private static Vector2 GetJitterOffset(int index, int phaseCount)
        {
            return new Vector2(
                HaltonSeq.Get((index % phaseCount) + 1, 2) - 0.5f,
                HaltonSeq.Get((index % phaseCount) + 1, 3) - 0.5f
            );
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
