Shader "DonkeyDungeon/Planar Mirror/HDRP"
{
    Properties
    {
        [HideInInspector] _DDMirrorReflectionTex ("Reflection Texture", 2D) = "black" {}
        [HideInInspector] _DDMirrorHasReflection ("Has Reflection", Float) = 0
        _StainMask ("Water Stain Mask (Alpha)", 2D) = "black" {}
        _StainStrength ("Water Stain Strength", Range(0,1)) = 0
        _Color ("Mirror Color (Tint & Alpha)", Color) = (1, 1, 1, 1)

        [Normal] _NoiseTex ("Normal Map (Distortion)", 2D) = "bump" {}
        _Distortion ("Distortion Strength", Range(0, 0.5)) = 0.05


        _Roughness ("Scratch Mask (Green Channel)", 2D) = "black" {}
        _RoughnessValue ("Scratch Intensity", Range(0, 1)) = 0.5

        [HideInInspector] _DDMirrorTransparentBackground ("Enable Transparent Background", Float) = 0

        [Toggle] _PuddleMode ("Enable Puddle Mode", Float) = 0
        _PuddleThreshold ("Puddle Threshold (Higher = Less Water)", Range(0, 1)) = 0.5
        _PuddleEdgeSoftness ("Puddle Edge Softness", Range(0.001, 0.25)) = 0.04
        _PuddleScale ("Procedural Puddle Scale", Range(0.25, 20)) = 3.5
        _PuddleWarp ("Procedural Shape Warp", Range(0, 3)) = 1.1
        _PuddleDetail ("Procedural Small Detail", Range(0, 1)) = 0.35
        _PuddleSeed ("Procedural Seed", Range(0, 100)) = 1
        [Toggle] _PuddleRipples ("Enable Procedural Water Ripples", Float) = 1
        _PuddleRippleScale ("Water Ripple Scale", Range(1, 100)) = 24
        _PuddleRippleSpeed ("Water Ripple Speed", Range(0, 5)) = 0.35
        _PuddleRippleStrength ("Water Ripple Strength", Range(0, 0.1)) = 0.012

    }

    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="ForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #define fixed4 float4
            #define TRANSFORM_TEX(tex,name) ((tex) * name##_ST.xy + name##_ST.zw)
            #define UNITY_PROJ_COORD(a) a

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float2 uv_Roughness : TEXCOORD2;
                float2 surfaceUV : TEXCOORD3;
            };

            sampler2D _StainMask;
            float4 _StainMask_ST;
            float _StainStrength;
            sampler2D _DDMirrorReflectionTex;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;


            sampler2D _Roughness;
            float4 _Roughness_ST;
            float _RoughnessValue;

            float _PuddleMode;
            float _PuddleThreshold;
            float _PuddleEdgeSoftness;
            float _PuddleScale;
            float _PuddleWarp;
            float _PuddleDetail;
            float _PuddleSeed;
            float _PuddleRipples;
            float _PuddleRippleScale;
            float _PuddleRippleSpeed;
            float _PuddleRippleStrength;

            float _DDMirrorTransparentBackground;
            float _DDMirrorHasReflection;
            float4x4 _DDMirrorReflectionVP;
            fixed4 _Color;
            float _Distortion;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));

                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float PuddleFbm(float2 p)
            {
                float value = 0.0;
                value += ValueNoise(p) * 0.5333;
                p = p * 2.03 + float2(13.7, 9.2);
                value += ValueNoise(p) * 0.2667;
                p = p * 2.01 + float2(7.4, 15.6);
                value += ValueNoise(p) * 0.1333;
                p = p * 2.04 + float2(11.1, 5.3);
                value += ValueNoise(p) * 0.0667;
                return value;
            }

            float ProceduralPuddle(float2 surfaceUV)
            {
                float2 seedOffset = float2(_PuddleSeed * 17.17, _PuddleSeed * 31.73);
                float2 p = surfaceUV * _PuddleScale + seedOffset;

                float2 warp;
                warp.x = PuddleFbm(p + float2(5.2, 1.3));
                warp.y = PuddleFbm(p + float2(1.7, 9.2));

                float broadShape = PuddleFbm(p + (warp - 0.5) * _PuddleWarp);
                float smallDetail = ValueNoise(p * 5.7 + seedOffset * 0.13);
                return lerp(broadShape, broadShape * 0.78 + smallDetail * 0.22, _PuddleDetail);
            }

            float2 ProceduralRipples(float2 surfaceUV)
            {
                float2 p = surfaceUV * _PuddleRippleScale;
                float t = _Time.y * _PuddleRippleSpeed;
                float waveA = sin(dot(p, normalize(float2(1.0, 0.63))) + t);
                float waveB = sin(dot(p * 1.37, normalize(float2(-0.71, 1.0))) - t * 1.21);
                float waveC = sin(dot(p * 0.73, normalize(float2(0.42, 1.0))) + t * 0.67);
                return float2(waveA + waveC * 0.35, waveB + waveC * 0.35) * 0.55;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformWorldToHClip(TransformObjectToWorld(v.vertex.xyz));
                o.screenPos = mul(_DDMirrorReflectionVP, float4(GetAbsolutePositionWS(TransformObjectToWorld(v.vertex.xyz)),1));
                o.screenPos.xy = o.screenPos.xy * 0.5 + o.screenPos.w * 0.5;
                o.uv = TRANSFORM_TEX(v.uv, _NoiseTex);
                o.uv_Roughness = TRANSFORM_TEX(v.uv, _Roughness);
                o.surfaceUV = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_DDMirrorHasReflection < 0.5) return 0;

                float3 normal = UnpackNormal(tex2D(_NoiseTex, i.uv));


                float scratch = tex2D(_Roughness, i.uv_Roughness).g * _RoughnessValue;



                float2 offset = (normal.xy * _Distortion + (normal.xy * scratch * 0.1)) * i.screenPos.w;

                if (_PuddleMode > 0.5 && _PuddleRipples > 0.5)
                {
                    offset += ProceduralRipples(i.surfaceUV) *
                        _PuddleRippleStrength * i.screenPos.w;
                }

                i.screenPos.xy += offset;


                fixed4 col = tex2Dproj(_DDMirrorReflectionTex, UNITY_PROJ_COORD(i.screenPos));




                half3 scratchColor = half3(0.8, 0.8, 0.8);
                col.rgb = lerp(col.rgb, scratchColor, scratch);
                float stain = tex2D(_StainMask, i.surfaceUV * _StainMask_ST.xy + _StainMask_ST.zw).a * _StainStrength;
                col.rgb = lerp(col.rgb, half3(0.65,0.65,0.65), stain);


                col.rgb *= _Color.rgb;
                col.a = lerp(1.0, col.a, _DDMirrorTransparentBackground) * _Color.a;


                if (_PuddleMode > 0.5)
                {
                    float puddleNoise = ProceduralPuddle(i.surfaceUV);
                    float edge = max(_PuddleEdgeSoftness, 0.001);
                    float puddleMask = smoothstep(
                        _PuddleThreshold - edge,
                        _PuddleThreshold + edge,
                        puddleNoise);
                    col.a *= puddleMask;
                }

                return col;
            }
            ENDHLSL
        }
    }
}
