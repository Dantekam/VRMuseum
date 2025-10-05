// Standalone version of auto-generate reactive mask from FidelityFX FSR2 as a fragment shader, without any Unity render pipeline dependencies.
Shader "Hidden/TND/Upscaling/AutoReactive"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Auto-Generate Reactive Mask"
            
            HLSLPROGRAM
            #pragma vertex VertMain
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols
            
            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            // Copyright  © 2023 Advanced Micro Devices, Inc.
            // Copyright  © 2024 Arm Limited.
            //
            // Permission is hereby granted, free of charge, to any person obtaining a copy
            // of this software and associated documentation files (the "Software"), to deal
            // in the Software without restriction, including without limitation the rights
            // to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
            // copies of the Software, and to permit persons to whom the Software is
            // furnished to do so, subject to the following conditions:
            //
            // The above copyright notice and this permission notice shall be included in all
            // copies or substantial portions of the Software.
            //
            // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
            // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
            // FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
            // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
            // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
            // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
            // SOFTWARE.

            #define AUTOREACTIVEFLAGS_APPLY_TONEMAP             1
            #define AUTOREACTIVEFLAGS_APPLY_INVERSETONEMAP      2
            #define AUTOREACTIVEFLAGS_APPLY_THRESHOLD           4
            #define AUTOREACTIVEFLAGS_USE_COMPONENTS_MAX        8
            
            uniform TEXTURE2D _OpaqueOnly;
            uniform TEXTURE2D _MainTex;

            uniform float4 _ReactiveParams;
            uniform uint _ReactiveFlags;

            #if defined(SHADER_API_PSSL)
            #pragma PSSL_target_output_format(default FMT_FP16_ABGR)
            #endif

            float3 LoadOpaqueOnly(uint2 iPxPos)
            {
                return _OpaqueOnly[COORD(iPxPos)].rgb;
            }

            float3 LoadInputColor(uint2 iPxPos)
            {
                return _MainTex[COORD(iPxPos)].rgb;
            }

            float GenReactiveScale()
            {
                return _ReactiveParams.x;
            }

            float GenReactiveThreshold()
            {
                return _ReactiveParams.y;
            }

            float GenReactiveBinaryValue()
            {
                return _ReactiveParams.z;
            }

            uint GenReactiveFlags()
            {
                return _ReactiveFlags;
            }

            float3 Tonemap(float3 fRgb)
            {
                return fRgb / (max(max(0.f, fRgb.r), max(fRgb.g, fRgb.b)) + 1.f).xxx;
            }

            float3 InverseTonemap(float3 fRgb)
            {
                return fRgb / max(1.0f / 65504.0f, 1.f - max(fRgb.r, max(fRgb.g, fRgb.b))).xxx;
            }

            float main(float4 SvPosition : SV_POSITION) : SV_TARGET0
            {
                uint2 uPixelCoord = uint2(SvPosition.xy);

                float3 ColorPreAlpha    = LoadOpaqueOnly(uPixelCoord);
                float3 ColorPostAlpha   = LoadInputColor(uPixelCoord);

                if (GenReactiveFlags() & AUTOREACTIVEFLAGS_APPLY_TONEMAP)
                {
                    ColorPreAlpha = Tonemap(ColorPreAlpha);
                    ColorPostAlpha = Tonemap(ColorPostAlpha);
                }

                if (GenReactiveFlags() & AUTOREACTIVEFLAGS_APPLY_INVERSETONEMAP)
                {
                    ColorPreAlpha = InverseTonemap(ColorPreAlpha);
                    ColorPostAlpha = InverseTonemap(ColorPostAlpha);
                }

                float out_reactive_value = 0.f;
                float3 delta = abs(ColorPostAlpha - ColorPreAlpha);

                out_reactive_value = (GenReactiveFlags() & AUTOREACTIVEFLAGS_USE_COMPONENTS_MAX) ? max(delta.x, max(delta.y, delta.z)) : length(delta);
                out_reactive_value *= GenReactiveScale();

                out_reactive_value = (GenReactiveFlags() & AUTOREACTIVEFLAGS_APPLY_THRESHOLD) ? (out_reactive_value < GenReactiveThreshold() ? 0 : GenReactiveBinaryValue()) : out_reactive_value;

                return out_reactive_value;
            }
            
            ENDHLSL
        } 
    }
}
