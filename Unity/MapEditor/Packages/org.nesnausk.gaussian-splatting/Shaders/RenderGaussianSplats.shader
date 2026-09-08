// SPDX-License-Identifier: MIT
Shader "Gaussian Splatting/Render Splats"
{
    Properties { _EnvironmentDepthBias("Environment Depth Bias", Float) = 0.06 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            ZWrite Off
            Blend OneMinusDstAlpha One
            Cull Off
            
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma require compute
#pragma use_dxc
#pragma multi_compile_local _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
#pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

#include "GaussianSplatting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

uint _EyeIndex;
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
// These draws target one texture-array slice at a time. Bind Meta's depth
// reprojection and TEXTURE2D_X sampling to the explicitly selected eye instead
// of Vulkan's read-only gl_ViewID.
#undef unity_StereoEyeIndex
#define unity_StereoEyeIndex _EyeIndex
#endif
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

StructuredBuffer<uint> _OrderBuffer;

struct v2f
{
    half4 col : COLOR0;
    float2 pos : TEXCOORD0;
    float4 vertex : SV_POSITION;
    float3 positionWS : TEXCOORD1;
};

StructuredBuffer<SplatViewData> _SplatViewData;
ByteAddressBuffer _SplatSelectedBits;
uint _SplatBitsValid;
float _SplatClipThreshold;
float _SplatEdgeSharpness;
int _SplatOpaqueMode;
uint _SplatCount;
float _EnvironmentDepthBias;
uint _IsStereo;
float4x4 _SplatViewProjection;

struct Attributes { uint vtxID : SV_VertexID; uint instanceID : SV_InstanceID; };
v2f vert (Attributes input)
{
    v2f o = (v2f)0;
	uint vtxID = input.vtxID;
	uint instID = input.instanceID;
	uint sortedIndex = _OrderBuffer[instID];
	if (sortedIndex >= _SplatCount)
	{
		o.vertex = asfloat(0x7fc00000);
		return o;
	}
	instID = sortedIndex;
	uint viewIndex = _IsStereo != 0 ? instID * 2 + _EyeIndex : instID;
	SplatViewData view = _SplatViewData[viewIndex];
	bool behindCam = view.pos.w <= 0;
	float4 centerClipPos = _IsStereo != 0
		? mul(_SplatViewProjection, float4(view.pos.xyz, 1))
		: TransformWorldToHClip(view.pos.xyz);
	if (behindCam)
	{
		o.vertex = asfloat(0x7fc00000); // NaN discards the primitive
	}
	else
	{
		o.col.r = f16tof32(view.color.x >> 16);
		o.col.g = f16tof32(view.color.x);
		o.col.b = f16tof32(view.color.y >> 16);
		o.col.a = f16tof32(view.color.y);

		uint idx = vtxID;
		float2 quadPos = float2(idx&1, (idx>>1)&1) * 2.0 - 1.0;
		quadPos *= 2;

		o.pos = quadPos;

		float2 deltaScreenPos = (quadPos.x * view.axis1 + quadPos.y * view.axis2) * 2 / _ScreenParams.xy;
		o.vertex = centerClipPos;
		o.vertex.xy += deltaScreenPos * centerClipPos.w;
		o.positionWS = view.pos.xyz;

		// is this splat selected?
		if (_SplatBitsValid)
		{
			uint wordIdx = instID / 32;
			uint bitIdx = instID & 31;
			uint selVal = _SplatSelectedBits.Load(wordIdx * 4);
			if (selVal & (1 << bitIdx))
			{
				o.col.a = -1;				
			}
		}
	}
	FlipProjectionIfBackbuffer(o.vertex);
    return o;
}

half4 frag (v2f i) : SV_Target
{
	float sceneDepth = SampleSceneDepth(i.vertex.xy / _ScaledScreenParams.xy);
#if UNITY_REVERSED_Z
	clip(i.vertex.z - sceneDepth);
#else
	clip(sceneDepth - i.vertex.z);
#endif

	float power = -dot(i.pos, i.pos);
	power *= max(_SplatEdgeSharpness, 0.0001);
	half alpha = exp(power);
	if (i.col.a >= 0)
	{
		alpha = saturate(alpha * i.col.a);
	}
	else
	{
		// "selected" splat: magenta outline, increase opacity, magenta tint
		half3 selectedColor = half3(1,0,1);
		if (alpha > 7.0/255.0)
		{
			if (alpha < 10.0/255.0)
			{
				alpha = 1;
				i.col.rgb = selectedColor;
			}
			alpha = saturate(alpha + 0.3);
		}
		i.col.rgb = lerp(i.col.rgb, selectedColor, 0.5);
	}
	
	clip(alpha - _SplatClipThreshold);

	if (_SplatOpaqueMode != 0)
		alpha = 1;

    half occlusion = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(i.positionWS, _EnvironmentDepthBias);
    half4 res = half4(i.col.rgb * alpha * occlusion, alpha * occlusion);
    return res;
}
ENDHLSL
        }
    }
}
