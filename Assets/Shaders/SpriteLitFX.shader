// URP 2D lit sprite (same lighting as Sprite-Lit-Default) with two extras (plan T22):
//  - Dissolve: pixel-blocky dissolve with a glowing HDR edge (enemy deaths). _Dissolve 0..1.
//  - Outline: 1 px outline drawn after lighting, so it reads at night (hover, selection, elites).
// Driven per renderer through a MaterialPropertyBlock by the SpriteStyle component.
Shader "RPG/Sprite Lit FX"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Dissolve)]
        _Dissolve("Amount", Range(0, 1)) = 0
        _DissolveEdge("Edge Width", Range(0, 0.5)) = 0.12
        [HDR] _DissolveEdgeColor("Edge Colour", Color) = (2.2, 1.1, 0.35, 1)
        _DissolvePixel("Block Size (texels)", Float) = 1

        [Header(Outline)]
        _Outline("Amount", Range(0, 1)) = 0
        [HDR] _OutlineColor("Colour", Color) = (1.6, 1.4, 0.6, 1)
        _OutlineWidth("Width (texels)", Float) = 1

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            // same layout in every pass (SRP batcher)
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Dissolve;
                half _DissolveEdge;
                half4 _DissolveEdgeColor;
                half _DissolvePixel;
                half _Outline;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            #include "SpriteFX.hlsl"

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 main = input.color * tex;

                half edge = 0;
                if (_Dissolve > 0.001)
                {
                    half d = RpgDissolve(input.uv, _Dissolve, _DissolvePixel);
                    clip(d);
                    edge = (1 - saturate(d / max(_DissolveEdge, 0.001))) * step(0.001, main.a);
                }

                // outline texels are empty in the sprite; URP's lighting discards alpha 0, so draw them here
                half outline = 0;
                if (_Outline > 0.001)
                    outline = RpgOutline(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, tex.a, _OutlineWidth) * _Outline * input.color.a;
                if (main.a <= 0.0)
                {
                    clip(outline - 0.001);
                    return half4(_OutlineColor.rgb, outline * _OutlineColor.a);
                }

                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);
#if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
#endif
                half4 lit = CombinedShapeLightShared(surfaceData, inputData);

                // the burning edge glows on top of the lighting (bloom picks up the HDR colour)
                lit.rgb = lerp(lit.rgb, _DissolveEdgeColor.rgb, edge * _DissolveEdgeColor.a);
                // half-transparent edge texels blend toward the outline too
                lit.rgb = lerp(lit.rgb, _OutlineColor.rgb, outline);
                lit.a = max(lit.a, outline * _OutlineColor.a);
                return lit;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Dissolve;
                half _DissolveEdge;
                half4 _DissolveEdgeColor;
                half _DissolvePixel;
                half _Outline;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                SetUpSpriteInstanceProperties();
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Dissolve;
                half _DissolveEdge;
                half4 _DissolveEdgeColor;
                half _DissolvePixel;
                half _Outline;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
