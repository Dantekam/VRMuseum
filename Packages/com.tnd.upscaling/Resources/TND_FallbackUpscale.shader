// Basic dejitter and bilinear upscale used as a fallback for when the upscaling system is misconfigured.
Shader "Hidden/TND/Upscaling/FallbackUpscale"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Sharpen"
            
            HLSLPROGRAM
            #pragma vertex VertMain
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            uniform TEXTURE2D _MainTex;

            uniform float2 _RenderSize;     // Size at which the image was rendered. UVs need to be clamped to this size.
            uniform float2 _InvUpscaleSize; // Inverse size of the upscaled output image. 
            uniform float2 _InvInputSize;   // Inverse size of the input color texture. This can differ from the render size.
            uniform float2 _JitterOffset;   // TAA jitter offset for this frame.

            SamplerState s_linear_clamp_sampler;

            float4 main(float4 SvPosition : SV_POSITION) : SV_TARGET0
            {
                uint2 OutputPos = uint2(SvPosition.xy);

                // Convert the output position to a UV and dejitter
                const float2 UpscaleUV = (OutputPos + 0.5f) * _InvUpscaleSize;
                const float2 JitteredUV = UpscaleUV + _JitterOffset;

                // Scale the UV to the input texture size and clamp to the render size
                const float2 SampleLocation = JitteredUV * _RenderSize;
                const float2 ClampedLocation = max(0.5f, min(SampleLocation, _RenderSize - 0.5f));
                const float2 SampleUV = ClampedLocation * _InvInputSize;

                // Bilinear sample the input texture for a very basic upscale
                const float4 Color = _MainTex.SampleLevel(s_linear_clamp_sampler, UV(SampleUV), 0);
                return Color;
            }
            
            ENDHLSL
        }
    }
}
