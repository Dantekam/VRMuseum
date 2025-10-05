using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework
{
    /// <summary>
    /// Structure to encapsulate some of the complexity of dealing with the various different methods to reference texture resources in Unity.
    /// 
    /// While the modern render pipelines mostly use Texture and derived types, some older legacy systems still use indirect RenderTargetIdentifiers instead.
    /// The problem with RenderTargetIdentifier is that Unity provides no easy way to convert that to a Texture object or obtain a native texture pointer.
    /// Since most native plugin-based upscalers require native texture pointers, this presents a problem that is usually solved by blitting or copying to a temporary render texture.
    /// We do not want to burden the upscaler plugins with this complexity, hence the reason for this struct's existence.
    ///
    /// Upscaler plugins will see TextureRef as part of their input and can simply request the type of texture reference that they require using a stable API.
    /// Meanwhile, the framework and render pipeline integrations will deal with managing temporary render textures and blitting where necessary.
    /// This also allows the framework to make improvements to efficiency in the future as and when possible, without breaking the public upscaler plugin API.
    /// </summary>
    public readonly struct TextureRef
    {
        public static readonly TextureRef Null = new TextureRef(null);
        
        public delegate Texture BlitterDelegate(CommandBuffer cmd, in RenderTextureDescriptor desc, in RenderTargetIdentifier source);
        
        public bool IsValid => _texture != null || _renderTargetIdentifier != BuiltinRenderTextureType.None;
        public int Width => _renderTextureDescriptor.width;
        public int Height => _renderTextureDescriptor.height;
        public GraphicsFormat GraphicsFormat => _renderTextureDescriptor.graphicsFormat;
        
        private readonly Texture _texture;
        private readonly RenderTargetIdentifier _renderTargetIdentifier;
        private readonly RenderTextureSubElement _renderTextureSubElement;
        private readonly RenderTextureDescriptor _renderTextureDescriptor;
        private readonly BlitterDelegate _blitter;
        
        /// <summary>
        /// Create a texture reference based on a Texture object.
        /// This is the preferred method of using TextureRef, as Texture objects can be translated to any other kind of representation
        /// without requiring copying, blitting or other conversions.
        /// </summary>
        public TextureRef(Texture texture, RenderTextureSubElement renderTextureSubElement = RenderTextureSubElement.Default)
        {
            _texture = texture;
            _renderTargetIdentifier = BuiltinRenderTextureType.None;
            _renderTextureSubElement = renderTextureSubElement;
            _blitter = null;

            if (_texture != null)
            {
                _renderTextureDescriptor = new RenderTextureDescriptor
                {
                    width = _texture.width,
                    height = _texture.height,
                    graphicsFormat = _texture.graphicsFormat,
                };
            }
            else
            {
                _renderTextureDescriptor = default;
            }
        }

        /// <summary>
        /// Create a texture reference based on an opaque render target identifier.
        /// This requires an additional render texture descriptor that describes the texture being referenced, so that a temporary render texture
        /// with matching specifications can be allocated if needed. This information cannot be derived from a RenderTargetIdentifier directly.
        /// </summary>
        public TextureRef(in RenderTargetIdentifier renderTargetIdentifier, in RenderTextureDescriptor renderTextureDescriptor, BlitterDelegate blitter,
            RenderTextureSubElement renderTextureSubElement = RenderTextureSubElement.Default)
        {
            _texture = null;
            _renderTargetIdentifier = renderTargetIdentifier;
            _renderTextureSubElement = renderTextureSubElement;
            _renderTextureDescriptor = renderTextureDescriptor;
            _blitter = blitter;
        }

        /// <summary>
        /// The fastest and most compatible method of retrieving a texture reference, for use with Unity's CommandBuffer API.
        /// Any kind of texture representation can be translated to a RenderTargetIdentifier without requiring any copying, blitting or conversion.
        /// </summary>
        public RenderTargetIdentifier GetRenderTargetIdentifier(int depthSlice = 0)
        {
            return _texture != null ? new RenderTargetIdentifier(_texture, depthSlice: depthSlice) : new RenderTargetIdentifier(_renderTargetIdentifier, 0, depthSlice: depthSlice);
        }

        /// <summary>
        /// Get the sub-element for this texture reference.
        /// Mostly used to obtain the depth attachment from a color render texture. 
        /// </summary>
        public RenderTextureSubElement GetRenderTextureSubElement()
        {
            return _renderTextureSubElement;
        }

        /// <summary>
        /// Obtain a Texture object, primarily meant for use with native plugins.
        /// This may trigger the creation of a temporary render texture and a blit of the input texture's contents, if it cannot be translated directly to a Texture object.
        /// </summary>
        public Texture GetTexture(CommandBuffer cmd)
        {
            if (_texture != null)
                return _texture;
            
            if (_renderTargetIdentifier != BuiltinRenderTextureType.None)
            {
                return _blitter?.Invoke(cmd, _renderTextureDescriptor, _renderTargetIdentifier);
            }

            return null;
        }

        /// <summary>
        /// Obtain a native texture pointer, primarily meant for use with native plugins.
        /// This may trigger the creation of a temporary render texture and a blit of the input texture's contents, if it cannot be translated directly to a Texture object.
        /// </summary>
        public IntPtr GetNativeTexturePointer(CommandBuffer cmd)
        {
            Texture texture = GetTexture(cmd);
            return texture != null ? texture.GetNativeTexturePtr() : IntPtr.Zero;
        }
    }
}
