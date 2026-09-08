using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GaussianSplatting.Runtime;
using ProtoVR.Splats;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace ProtoVR.Diagnostics
{
    public sealed class ProtoVrStartupDiagnostics : MonoBehaviour
    {
        const string Prefix = "[ProtoVRDiag]";
        const string DisableSplatsMarker = "disable-splats.txt";

        readonly List<XRDisplaySubsystem> displays = new();
        int renderedCameraFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var diagnostics = new GameObject(nameof(ProtoVrStartupDiagnostics));
            DontDestroyOnLoad(diagnostics);
            diagnostics.AddComponent<ProtoVrStartupDiagnostics>();
        }

        void Awake()
        {
            Debug.Log($"{Prefix} BOOT version={Application.version} bundle={Application.identifier} unity={Application.unityVersion}");
            Debug.Log($"{Prefix} DEVICE model={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} systemMB={SystemInfo.systemMemorySize} graphicsMB={SystemInfo.graphicsMemorySize}");
            Debug.Log($"{Prefix} GPU type={SystemInfo.graphicsDeviceType} name={SystemInfo.graphicsDeviceName} version={SystemInfo.graphicsDeviceVersion} shaderLevel={SystemInfo.graphicsShaderLevel}");
            Debug.Log($"{Prefix} CAPABILITIES compute={SystemInfo.supportsComputeShaders} instancing={SystemInfo.supportsInstancing} textureArrays={SystemInfo.supports2DArrayTextures} asyncCompute={SystemInfo.supportsAsyncCompute}");
            Debug.Log($"{Prefix} XR initial enabled={XRSettings.enabled} active={XRSettings.isDeviceActive} stereo={XRSettings.stereoRenderingMode} eye={Describe(XRSettings.eyeTextureDesc)}");
            Debug.Log($"{Prefix} persistentDataPath={Application.persistentDataPath}");

            SceneManager.sceneLoaded += OnSceneLoaded;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            StartCoroutine(LogStartupState());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool disableSplats = File.Exists(Path.Combine(Application.persistentDataPath, DisableSplatsMarker));
            Debug.Log($"{Prefix} SCENE loaded={scene.path} mode={mode} roots={scene.rootCount} disableSplats={disableSplats}");

            var staticRenderers = FindObjectsByType<StaticQsfRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var staticRenderer in staticRenderers)
            {
                var source = staticRenderer.Source;
                var gaussian = staticRenderer.GetComponent<GaussianSplatRenderer>();
                Debug.Log($"{Prefix} SPLAT object={HierarchyPath(staticRenderer.transform)} active={staticRenderer.gameObject.activeInHierarchy} source={(source == null ? "<null>" : source.name)} count={(source == null ? 0 : source.SplatCount)} gaussian={(gaussian != null)} valid={(gaussian != null && gaussian.HasValidAsset)}");
                if (!disableSplats)
                    continue;

                staticRenderer.enabled = false;
                if (gaussian != null)
                    gaussian.enabled = false;
                Debug.Log($"{Prefix} SPLAT_DISABLED object={HierarchyPath(staticRenderer.transform)} marker={DisableSplatsMarker}");
            }

            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (disableSplats && camera.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.02f, 0.15f, 0.35f, 1f);
                }
                Debug.Log($"{Prefix} CAMERA object={HierarchyPath(camera.transform)} enabled={camera.enabled} stereoEnabled={camera.stereoEnabled} targetEye={camera.stereoTargetEye} mask=0x{camera.cullingMask:X8} near={camera.nearClipPlane} far={camera.farClipPlane}");
            }
        }

        IEnumerator LogStartupState()
        {
            for (int second = 1; second <= 8; second++)
            {
                yield return new WaitForSecondsRealtime(1f);
                displays.Clear();
                SubsystemManager.GetSubsystems(displays);
                string displayState = string.Join(",", displays.Select(display => $"{display.GetType().Name}:running={display.running}"));
                Debug.Log($"{Prefix} HEARTBEAT second={second} frame={Time.frameCount} xrEnabled={XRSettings.enabled} xrActive={XRSettings.isDeviceActive} displays={displayState} renderedCameraFrames={renderedCameraFrames}");
            }
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (renderedCameraFrames >= 3 || !camera.isActiveAndEnabled)
                return;
            renderedCameraFrames++;
            Debug.Log($"{Prefix} RENDER_BEGIN index={renderedCameraFrames} camera={HierarchyPath(camera.transform)} stereoEnabled={camera.stereoEnabled} pixel={camera.pixelWidth}x{camera.pixelHeight} xrEye={Describe(XRSettings.eyeTextureDesc)} pipeline={(GraphicsSettings.currentRenderPipeline == null ? "<builtin>" : GraphicsSettings.currentRenderPipeline.name)}");
        }

        static string Describe(RenderTextureDescriptor descriptor)
        {
            return $"{descriptor.width}x{descriptor.height}x{descriptor.volumeDepth}/{descriptor.dimension}/{descriptor.graphicsFormat}/msaa{descriptor.msaaSamples}";
        }

        static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
