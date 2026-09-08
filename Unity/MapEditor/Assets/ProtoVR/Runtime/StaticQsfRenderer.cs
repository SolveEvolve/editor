using GaussianSplatting.Runtime;
using UnityEngine;

namespace ProtoVR.Splats
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GaussianSplatRenderer))]
    public sealed class StaticQsfRenderer : MonoBehaviour
    {
        [SerializeField] StaticQsfAsset source;
        [SerializeField] GaussianSplatRenderer targetRenderer;

        GaussianSplatAsset runtimeAsset;

        public StaticQsfAsset Source => source;

        public void SetSource(StaticQsfAsset value)
        {
            if (source == value && runtimeAsset != null)
                return;
            source = value;
            Rebuild();
        }

        void OnEnable()
        {
            Rebuild();
        }

        void OnDisable()
        {
            ReleaseRuntimeAsset();
        }

        void OnValidate()
        {
            if (isActiveAndEnabled)
                Rebuild();
        }

        void Rebuild()
        {
            ReleaseRuntimeAsset();
            if (source == null)
                return;

            if (targetRenderer == null)
                targetRenderer = GetComponent<GaussianSplatRenderer>();
            if (targetRenderer == null)
                return;

            runtimeAsset = source.CreateRuntimeAsset();
            runtimeAsset.hideFlags = HideFlags.HideAndDontSave;
            targetRenderer.m_SHOrder = 0;
            targetRenderer.m_SHOnly = false;
            targetRenderer.m_SortMode = GaussianSplatRenderer.SortMode.OnTransformChange;
            targetRenderer.m_SortPositionThreshold = 0.03f;
            targetRenderer.m_SortAngleThreshold = 4f;
            targetRenderer.m_SortMaxFramesWithoutUpdate = 18;
            targetRenderer.m_Asset = runtimeAsset;
        }

        void ReleaseRuntimeAsset()
        {
            if (runtimeAsset == null)
                return;
            if (targetRenderer != null && targetRenderer.m_Asset == runtimeAsset)
                targetRenderer.m_Asset = null;

            if (Application.isPlaying)
                Destroy(runtimeAsset);
            else
                DestroyImmediate(runtimeAsset);
            runtimeAsset = null;
        }
    }
}
