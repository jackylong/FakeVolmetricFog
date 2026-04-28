Shader "Custom/Fog"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Mask ("MaskTex", 2D) = "white" {}
        _FogAreaSize ("Fog Area Size (XZ)", Vector) = (200, 200, 0, 0)
        _FogCenter ("Fog Center", Vector) = (0, 0, 0, 0)
        _FogThickness ("Fog Thickness", Range(0, 1)) = 0.3
        _FogDensity ("Fog Density (XYZ)", Vector) = (6, 4, 6)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        Cull off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FogAreaSize;
                float4 _FogCenter;
                float _FogThickness;
                float4 _FogDensity;
            CBUFFER_END



            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float3> PositionBuffer;
                StructuredBuffer<float4x4> RotationMatrixBuffer;
            #endif

            void setup()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float3 verts = PositionBuffer[unity_InstanceID];
                float x = verts.x;
                float y = verts.y;
                float z = verts.z;
                unity_ObjectToWorld._14_24_34_44 = float4(x, y, z, 1);
            #endif
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float3 scale = _FogDensity.xyz;
                float4x4 scaleMat = float4x4(
                    scale.x, 0, 0, 0,
                    0, scale.y, 0, 0,
                    0, 0, scale.z, 0,
                    0, 0, 0, 1
                );

                float4x4 billboardMat = UNITY_MATRIX_V;
                billboardMat._m03 =
                    billboardMat._m13 =
                    billboardMat._m23 =
                    billboardMat._m33 = 0;

                float4x4 rotMat = RotationMatrixBuffer[unity_InstanceID];
                v.vertex.xyz = mul(v.vertex.xyz, rotMat);
                float3 vert = mul(v.vertex.xyz, billboardMat);
                v.vertex.xyz = mul(vert, scaleMat);

                float3 instancePos = PositionBuffer[unity_InstanceID];
                o.maskUV = (instancePos.xz - _FogCenter.xz) / _FogAreaSize.xy * 0.5 + 0.5;
            #else
                o.maskUV = float2(0, 0);
            #endif

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float4 res = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.maskUV).r;
                return float4(res.rgb, res.a * _FogThickness * mask);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
// Shader "Custom/Fog"
// {
//     Properties
//     {
//         _Color ("Color", Color) = (1,1,1,1)
//         _MainTex ("Albedo (RGB)", 2D) = "white" {}
//         _Mask ("MaskTex", 2D) = "white" {}
//         _FogAreaSize ("Fog Area Size (XZ)", Vector) = (200, 200, 0, 0)
//         _FogCenter ("Fog Center", Vector) = (0, 0, 0, 0)
//         _FogThickness ("Fog Thickness", Range(0, 1)) = 0.3
//         _FogDensity ("Fog Density (XYZ)", Vector) = (6, 4, 6)
        
//         // 新增两个参数
//         _SoftParticleDistance ("Soft Particle Distance", Range(0.1, 3)) = 0.8
//         _GroundFadeHeight ("Ground Fade Height", Range(-1, 3)) = 1.0
//     }
//     SubShader
//     {
//         Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
//         LOD 100
//         Cull off
//         ZWrite Off
//         Blend SrcAlpha OneMinusSrcAlpha

//         Pass
//         {
//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #pragma multi_compile_instancing
//             #pragma instancing_options procedural:setup

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

//             struct appdata
//             {
//                 float4 vertex : POSITION;
//                 float2 uv : TEXCOORD0;
//                 UNITY_VERTEX_INPUT_INSTANCE_ID
//             };

//             struct v2f
//             {
//                 float2 uv : TEXCOORD0;
//                 float2 maskUV : TEXCOORD1;
//                 float4 vertex : SV_POSITION;
//                 float4 screenPos : TEXCOORD2;   // 用于深度采样
//                 float3 worldPos : TEXCOORD3;   // 新增：世界坐标
//                 UNITY_VERTEX_INPUT_INSTANCE_ID
//             };

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);
//             TEXTURE2D(_Mask);
//             SAMPLER(sampler_Mask);

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _Color;
//                 float4 _FogAreaSize;
//                 float4 _FogCenter;
//                 float _FogThickness;
//                 float4 _FogDensity;
//                 float _SoftParticleDistance;
//                 float _GroundFadeHeight;
//             CBUFFER_END

//             #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
//                 StructuredBuffer<float3> PositionBuffer;
//                 StructuredBuffer<float4x4> RotationMatrixBuffer;
//             #endif

//             void setup()
//             {
//             #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
//                 float3 verts = PositionBuffer[unity_InstanceID];
//                 float x = verts.x;
//                 float y = verts.y;
//                 float z = verts.z;
//                 unity_ObjectToWorld._14_24_34_44 = float4(x, y, z, 1);
//             #endif
//             }

//             v2f vert(appdata v)
//             {
//                 v2f o;
//                 UNITY_SETUP_INSTANCE_ID(v);
//                 UNITY_TRANSFER_INSTANCE_ID(v, o);

//             #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
//                 float3 scale = _FogDensity.xyz;
//                 float4x4 scaleMat = float4x4(
//                     scale.x, 0, 0, 0,
//                     0, scale.y, 0, 0,
//                     0, 0, scale.z, 0,
//                     0, 0, 0, 1
//                 );

//                 float4x4 billboardMat = UNITY_MATRIX_V;
//                 billboardMat._m03 = billboardMat._m13 = billboardMat._m23 = billboardMat._m33 = 0;

//                 float4x4 rotMat = RotationMatrixBuffer[unity_InstanceID];
//                 float3 localPos = v.vertex.xyz;
//                 // 应用旋转
//                 localPos = mul(rotMat, float4(localPos, 1)).xyz;
//                 // 应用 billboard
//                 float3 billboarded = mul(localPos, (float3x3)billboardMat);
//                 // 应用缩放
//                 billboarded *= scale;
//                 // 得到世界坐标 = 实例位置 + 偏移
//                 float3 instancePos = PositionBuffer[unity_InstanceID];
//                 float3 worldPos = instancePos + billboarded;
//                 o.worldPos = worldPos;

//                 // 计算裁剪空间位置
//                 o.vertex = TransformWorldToHClip(worldPos);
//                 o.screenPos = ComputeScreenPos(o.vertex);
                
//                 // 遮罩 UV
//                 o.maskUV = (instancePos.xz - _FogCenter.xz) / _FogAreaSize.xy * 0.5 + 0.5;
//             #else
//                 o.vertex = TransformObjectToHClip(v.vertex.xyz);
//                 o.screenPos = ComputeScreenPos(o.vertex);
//                 o.maskUV = float2(0,0);
//                 o.worldPos = float3(0,0,0);
//             #endif
//                 o.uv = v.uv;
//                 return o;
//             }

//             float4 frag(v2f i) : SV_Target
//             {
//                  UNITY_SETUP_INSTANCE_ID(i);

//                 float4 res = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
//                 half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.maskUV).r;
//                 float alpha = res.a * _FogThickness * mask;

//                 // ===== 1. Y轴淡出（消除地面硬边）=====
//                 float groundHeight = _FogCenter.y;      // 雾区域中心Y坐标，可理解为地面高度（或雾底部）
//                 float fadeStart = groundHeight;         // 开始淡出的高度
//                 // float fadeEnd = groundHeight + 1.0;     // 完全可见的高度（单位：米）
//                 float fadeEnd = groundHeight + _GroundFadeHeight;
//                 float yFactor = saturate((i.worldPos.y - fadeStart) / (fadeEnd - fadeStart));
//                 alpha *= yFactor;

//                 // ===== 2. 软粒子（消除垂直物体硬边）=====
//                 float2 screenUV = i.screenPos.xy / i.screenPos.w;
//                 float sceneDepth = SampleSceneDepth(screenUV);
//                 float sceneLinear = LinearEyeDepth(sceneDepth, _ZBufferParams);
//                 float particleLinear = LinearEyeDepth(i.screenPos.z, _ZBufferParams);
//                 float depthDiff = sceneLinear - particleLinear;
//                 float softness = saturate(depthDiff / _SoftParticleDistance);
//                 alpha *= softness;

//                 return float4(res.rgb, alpha);
//             }
//             ENDHLSL
//         }
//     }
//     FallBack "Universal Render Pipeline/Unlit"
// }