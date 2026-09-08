// SPDX-License-Identifier: MIT
Shader "Hidden/Gaussian Splatting/Composite"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma require compute
#pragma use_dxc
#pragma multi_compile_local _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

struct v2f
{
    float4 vertex : SV_POSITION;
};

v2f vert (uint vtxID : SV_VertexID)
{
    v2f o = (v2f)0;
    float2 quadPos = float2(vtxID&1, (vtxID>>1)&1) * 4.0 - 1.0;
	o.vertex = float4(quadPos, 1, 1);
    return o;
}

TEXTURE2D_X(_GaussianSplatRT);
uint _CustomStereoEyeIndex;
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
// The composite is submitted once per array slice. Redirect TEXTURE2D_X to the
// requested source slice without assigning Vulkan's const gl_ViewID.
#undef unity_StereoEyeIndex
#define unity_StereoEyeIndex _CustomStereoEyeIndex
#endif

half4 frag (v2f i) : SV_Target
{
    half4 col = LOAD_TEXTURE2D_X(_GaussianSplatRT, int2(i.vertex.xy));

    // Empty pixels have zero alpha after the target clear. Dividing their RGB by
    // zero can produce NaNs, which mobile compositors may retain as view-locked
    // screen debris across frames. Keep transparent pixels exactly transparent.
    half alpha = saturate(col.a);
    if (alpha <= 0.00001h)
        return half4(0, 0, 0, 0);

    // QSF2's SH0 RGB payload is gamma-encoded. Convert once, after the
    // accumulation buffer has been un-premultiplied, to match the original
    // CH Launch playback path in this Linear-colour-space project.
    return half4(SRGBToLinear(col.rgb / alpha), alpha);
}
ENDHLSL
        }
    }
}
