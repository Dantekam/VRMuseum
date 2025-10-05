Shader "Hidden/TND/Upscaling/DilateDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            PackageRequirements { "com.unity.render-pipelines.universal" }
            
            Name "DilateDepth"
            ZTest Always ZWrite On ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            //#pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

            uniform int _ViewIndex;
            uniform float4 _BlitScaleBias;
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);

                output.positionCS = pos;
                output.texcoord   = uv;
                return output;
            }

            #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                #define DEPTH_TEXTURE(name) TEXTURE2D_ARRAY_FLOAT(name)
                #define LOAD(pos) _InputDepthTexture[int3(pos, _ViewIndex)]
                #define SAMPLE(uv) SAMPLE_TEXTURE2D_ARRAY(_InputDepthTexture, sampler_InputDepthTexture, uv, _ViewIndex).r
            #else
                #define DEPTH_TEXTURE(name) TEXTURE2D_FLOAT(name)
                #define LOAD(pos) _InputDepthTexture[pos]
                #define SAMPLE(uv) SAMPLE_DEPTH_TEXTURE(_InputDepthTexture, sampler_InputDepthTexture, uv)
            #endif

            DEPTH_TEXTURE(_InputDepthTexture);
            SAMPLER(sampler_InputDepthTexture);
            uniform float4 _InputDepthTexture_TexelSize;

            float FindNearestDepth(in float2 fHrUV, in float2 fHrTexelSize, in int2 iLrSize, in float2 iLrJitter)
            {
                const int iSampleCount = 9;
                const int2 iSampleOffsets[iSampleCount] =
                {
                    int2(+0, +0),
                    int2(+1, +0),
                    int2(+0, +1),
                    int2(+0, -1),
                    int2(-1, +0),
                    int2(-1, +1),
                    int2(+1, +1),
                    int2(-1, -1),
                    int2(+1, -1),
                };

                // pull out the depth loads to allow SC to batch them
                float depth[9];
                int2 position[9];
                int iSampleIndex;
                UNITY_UNROLL
                for (iSampleIndex = 0; iSampleIndex < iSampleCount; ++iSampleIndex)
                {
                    // Sample for neighbouring texels in high-res texture space
                    float2 fUV = fHrUV + iSampleOffsets[iSampleIndex] * fHrTexelSize;

                    // Convert UV to low-res texture space and dejitter
                    int2 iPxPos = int2(floor((fUV + iLrJitter) * iLrSize - float2(0.5f, 0.5f)));

                    // Sample low-res depth texture
                    depth[iSampleIndex] = LOAD(iPxPos).r;
                    position[iSampleIndex] = iPxPos;
                }

                // find closest depth
                float fNearestDepth = depth[0];
                UNITY_UNROLL
                for (iSampleIndex = 1; iSampleIndex < iSampleCount; ++iSampleIndex)
                {
                    int2 iPos = position[iSampleIndex];
                    if (all(iPos < iLrSize))
                    {
                        float fNdDepth = depth[iSampleIndex];
                    #if UNITY_REVERSED_Z
                        if (fNdDepth < fNearestDepth)
                    #else
                        if (fNdDepth > fNearestDepth)
                    #endif
                        {
                            fNearestDepth = fNdDepth;
                        }
                    }
                }
                
                return fNearestDepth;
            }
            
            float frag(Varyings input) : SV_Depth
            {
                const float2 iHrTexelSize = _BlitScaleBias.xy;
                const int2 iLrSize = int2(_InputDepthTexture_TexelSize.zw);
                const float2 iLrJitter = _BlitScaleBias.zw;
                
                return FindNearestDepth(input.texcoord, iHrTexelSize, iLrSize, iLrJitter);
            }

            ENDHLSL
        }
    }
}
