Shader "JapaneseDemonHunter/CastleBeacon"
{
    Properties
    {
        _BaseColor("Stone color",Color)=(0.35,0.28,0.16,1)
        _EmissionColor("Warm glow",Color)=(0.08,0.05,0.015,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO};
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half light=.25h+.75h*saturate(dot(normalize(input.normalWS),normalize(half3(-.3h,.8h,-.2h))));
                // Only the distant goal bypasses forest fog, remaining depth-tested against scenery.
                return half4(_BaseColor.rgb*light+_EmissionColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
