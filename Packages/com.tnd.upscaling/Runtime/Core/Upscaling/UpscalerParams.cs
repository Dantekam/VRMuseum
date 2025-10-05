using UnityEngine;

namespace TND.Upscaling.Framework
{
    public struct UpscalerInitParams
    {
        public Camera camera;
        public bool useTextureArrays;
        public int numTextureSlices;
        
        public Vector2Int maxRenderSize;
        public Vector2Int upscaleSize;

        public bool enableHDR;
        public bool invertedDepth;
        public bool highResMotionVectors;
        public bool jitteredMotionVectors;
    }
    
    public struct UpscalerDispatchParams
    {
        public Matrix4x4 nonJitteredProjectionMatrix;
        public int viewIndex;
        
        public TextureRef inputColor;
        public TextureRef inputDepth;
        public TextureRef inputMotionVectors;
        public TextureRef inputExposure;
        public TextureRef inputReactiveMask;
        public TextureRef inputOpaqueOnly;
        public TextureRef outputColor;
        
        public Vector2Int renderSize;
        public Vector2 motionVectorScale;
        public Vector2 jitterOffset;
        public float preExposure;
        public bool enableSharpening;
        public float sharpness;
        public bool resetHistory;
    }
}
