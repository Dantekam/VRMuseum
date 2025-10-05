// Set of shaders to merge together reactive masks from multiple different sources.
Shader "Hidden/TND/Upscaling/MergeReactive"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass    // 0
        {
            Name "Merge Two Reactive Masks"
            
            HLSLPROGRAM
            #pragma vertex VertMain
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            uniform TEXTURE2D _Input1;
            uniform TEXTURE2D _Input2;

            float4 main(float4 SvPosition : SV_POSITION) : SV_TARGET0
            {
                uint2 uPixelCoord = uint2(SvPosition.xy);
                float mask1 = _Input1[COORD(uPixelCoord)].r;
                float mask2 = _Input2[COORD(uPixelCoord)].r;
                return max(mask1, mask2).rrrr;
            }
            
            ENDHLSL
        }

        Pass    // 1
        {
            Name "Merge Three Reactive Masks"
            
            HLSLPROGRAM
            #pragma vertex VertMain
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            uniform TEXTURE2D _Input1;
            uniform TEXTURE2D _Input2;
            uniform TEXTURE2D _Input3;

            float4 main(float4 SvPosition : SV_POSITION) : SV_TARGET0
            {
                uint2 uPixelCoord = uint2(SvPosition.xy);
                float mask1 = _Input1[COORD(uPixelCoord)].r;
                float mask2 = _Input2[COORD(uPixelCoord)].r;
                float mask3 = _Input3[COORD(uPixelCoord)].r;
                return max(max(mask1, mask2), mask3).rrrr;
            }
            
            ENDHLSL
        }

        Pass    // 2
        {
            Name "Merge Four Reactive Masks"
            
            HLSLPROGRAM
            #pragma vertex VertMain
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            uniform TEXTURE2D _Input1;
            uniform TEXTURE2D _Input2;
            uniform TEXTURE2D _Input3;
            uniform TEXTURE2D _Input4;

            float4 main(float4 SvPosition : SV_POSITION) : SV_TARGET0
            {
                uint2 uPixelCoord = uint2(SvPosition.xy);
                float mask1 = _Input1[COORD(uPixelCoord)].r;
                float mask2 = _Input2[COORD(uPixelCoord)].r;
                float mask3 = _Input3[COORD(uPixelCoord)].r;
                float mask4 = _Input4[COORD(uPixelCoord)].r;
                return max(max(max(mask1, mask2), mask3), mask4).rrrr;
            }
            
            ENDHLSL
        }
    }
}
