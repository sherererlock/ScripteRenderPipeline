Shader "Hidden/Custom RP/Post FX Stack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "PostFXStackPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "BloomPrefilterFireFlies"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterFireFliesPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "BloomPrefilter"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }

        Pass 
        {
            Name "BloomHorizontal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "BloomVertical"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "BloomAdd"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomAddPassFragment 
            ENDHLSL
        }

        Pass
        {
            Name "BloomScatter"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomScatterPassFragment 
            ENDHLSL
        }

        Pass
        {
            Name "BloomScatterFinal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomScatterFinalPassFragment 
            ENDHLSL
        }

        Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "ToneMappingACES"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ToneMappingACESPassFragment 
            ENDHLSL
        }

        Pass
        {
            Name "ToneMappingNeutral"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ToneMappingNeutralPassFragment 
            ENDHLSL
        }

        Pass
        {
            Name "ToneMappingReinhard"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ToneMappingReinhardPassFragment 
            ENDHLSL
        }
    }
}
