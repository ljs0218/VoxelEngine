Shader "VoxelEngine/VoxelHighlight"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 0.9)
        _EdgeWidth ("Edge Width", Range(0.001, 0.08)) = 0.025
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "Highlight"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor;
                float _EdgeWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Remap from [-0.5, 0.5] to [0, 1]
                output.localPos = input.positionOS.xyz + 0.5;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.localPos;

                // Distance from each face edge (0 at edge, 0.5 at center)
                float3 d = min(p, 1.0 - p);

                // Sort the 3 distances — an edge is where 2 of 3 axes are near 0
                // Use the second-smallest distance: if it's < EdgeWidth, we're on an edge
                float d0 = d.x;
                float d1 = d.y;
                float d2 = d.z;

                // Sort ascending
                float tmp;
                if (d0 > d1) { tmp = d0; d0 = d1; d1 = tmp; }
                if (d1 > d2) { tmp = d1; d1 = d2; d2 = tmp; }
                if (d0 > d1) { tmp = d0; d0 = d1; d1 = tmp; }

                // d0 = smallest (distance to nearest face — always ~0 on surface)
                // d1 = second smallest — distance to nearest edge line
                // If d1 < EdgeWidth, this pixel is on a cube edge
                float edgeDist = d1;

                half alpha = _EdgeColor.a * (1.0 - smoothstep(0.0, _EdgeWidth, edgeDist));

                // Discard fully transparent pixels
                if (alpha < 0.01) discard;

                return half4(_EdgeColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}