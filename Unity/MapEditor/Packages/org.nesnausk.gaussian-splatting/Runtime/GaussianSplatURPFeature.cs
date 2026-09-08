// SPDX-License-Identifier: MIT
#if GS_ENABLE_URP

#if !UNITY_6000_0_OR_NEWER
#error Unity Gaussian Splatting URP support only works in Unity 6 or later
#endif

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.XR;

namespace GaussianSplatting.Runtime
{
    // Note: I have no idea what is the purpose of ScriptableRendererFeature vs ScriptableRenderPass, which one of those
    // is supposed to do resource management vs logic, etc. etc. Code below "seems to work" but I'm just fumbling along,
    // without understanding any of it.
    //
    // ReSharper disable once InconsistentNaming
    class GaussianSplatURPFeature : ScriptableRendererFeature
    {
        class GSRenderPass : ScriptableRenderPass
        {
            const string GaussianSplatRTName = "_GaussianSplatRT";

            const string ProfilerTag = "GaussianSplatRenderGraph";
            static readonly ProfilingSampler s_profilingSampler = new(ProfilerTag);
            static readonly int s_gaussianSplatRT = Shader.PropertyToID(GaussianSplatRTName);

            class PassData
            {
                internal UniversalCameraData CameraData;
                internal TextureHandle SourceTexture;
                internal TextureHandle SourceDepth;
                internal TextureHandle GaussianSplatRT;
                internal bool IsStereo;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using var builder = renderGraph.AddUnsafePass(ProfilerTag, out PassData passData);

                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                bool isStereo = XRSettings.enabled && cameraData.camera.stereoEnabled &&
                                (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced ||
                                 XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassMultiview) &&
                                !Application.isEditor;
                RenderTextureDescriptor rtDesc = isStereo ? XRSettings.eyeTextureDesc : cameraData.cameraTargetDescriptor;
                rtDesc.depthBufferBits = 0;
                rtDesc.msaaSamples = 1;
                rtDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                var textureHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, rtDesc, GaussianSplatRTName, true);

                passData.CameraData = cameraData;
                passData.SourceTexture = resourceData.activeColorTexture;
                passData.SourceDepth = resourceData.activeDepthTexture;
                passData.GaussianSplatRT = textureHandle;
                passData.IsStereo = isStereo;

                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                builder.UseTexture(resourceData.activeDepthTexture);
                builder.UseTexture(textureHandle, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    var commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    using var _ = new ProfilingScope(commandBuffer, s_profilingSampler);
                    commandBuffer.SetGlobalTexture(s_gaussianSplatRT, data.GaussianSplatRT);

                    if (data.IsStereo)
                    {
                        // Quest/Vulkan does not reliably populate the eye index for procedural
                        // draws into texture arrays. Render and composite each array slice
                        // explicitly, while sharing sorting and view-data calculation.
                        var prepared = GaussianSplatRenderSystem.instance.PrepareSplats(data.CameraData.camera, commandBuffer);
                        if (prepared.CompositeMaterial == null)
                            return;

                        for (int eye = 0; eye < 2; ++eye)
                        {
                            CoreUtils.SetRenderTarget(commandBuffer, data.GaussianSplatRT, data.SourceDepth,
                                ClearFlag.Color, Color.clear, 0, CubemapFace.Unknown, eye);
                            GaussianSplatRenderSystem.instance.RenderPreparedSplats(commandBuffer, eye);
                        }

                        commandBuffer.BeginSample(GaussianSplatRenderSystem.s_ProfCompose);
                        for (int eye = 0; eye < 2; ++eye)
                        {
                            commandBuffer.SetRenderTarget(data.SourceTexture, 0, CubemapFace.Unknown, eye);
                            commandBuffer.SetGlobalInt("_CustomStereoEyeIndex", eye);
                            commandBuffer.DrawProcedural(Matrix4x4.identity, prepared.CompositeMaterial, 0,
                                MeshTopology.Triangles, 3, 1);
                        }
                        commandBuffer.EndSample(GaussianSplatRenderSystem.s_ProfCompose);
                        return;
                    }

                    // The splat target is a transient accumulation buffer.  Make the clear explicit as
                    // well as part of SetRenderTarget: on tiled mobile GPUs this prevents any colour or
                    // alpha left by a prior camera frame from being composited into the current view.
                    CoreUtils.SetRenderTarget(commandBuffer, data.GaussianSplatRT, data.SourceDepth, ClearFlag.Color, Color.clear);
                    commandBuffer.ClearRenderTarget(RTClearFlags.Color, Color.clear, 0.0f, 0);
                    Material matComposite = GaussianSplatRenderSystem.instance.SortAndRenderSplats(data.CameraData.camera, commandBuffer);
                    commandBuffer.BeginSample(GaussianSplatRenderSystem.s_ProfCompose);
                    Blitter.BlitCameraTexture(commandBuffer, data.GaussianSplatRT, data.SourceTexture, matComposite, 0);
                    commandBuffer.EndSample(GaussianSplatRenderSystem.s_ProfCompose);
                });
            }
        }

        GSRenderPass m_Pass;
        bool m_HasCamera;

        public override void Create()
        {
            m_Pass = new GSRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            m_HasCamera = false;
            var system = GaussianSplatRenderSystem.instance;
            if (!system.GatherSplatsForCamera(cameraData.camera))
                return;

            m_HasCamera = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!m_HasCamera)
                return;
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass = null;
        }
    }
}

#endif // #if GS_ENABLE_URP

