using UnityEngine;

namespace ProtoVR
{
    [DisallowMultipleComponent]
    public sealed class FloorDistanceGrabbable : MonoBehaviour
    {
        [SerializeField] private float _floorHeight;
        [SerializeField, Min(0f)] private float _rotationSpeed = 90f;
        [SerializeField, Min(0f)] private float _selectionPadding = 0.03f;

        private Renderer[] _renderers;

        public float FloorHeight => _floorHeight;
        public float RotationSpeed => _rotationSpeed;

        public void Configure(float floorHeight, float rotationSpeed = 90f, float selectionPadding = 0.03f)
        {
            _floorHeight = floorHeight;
            _rotationSpeed = Mathf.Max(0f, rotationSpeed);
            _selectionPadding = Mathf.Max(0f, selectionPadding);
            CacheRenderers();
        }

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnTransformChildrenChanged()
        {
            CacheRenderers();
        }

        public void SetFloorPosition(Vector3 worldPosition)
        {
            worldPosition.y = _floorHeight;
            transform.position = worldPosition;
        }

        public void RotateAroundWorldUp(float degrees)
        {
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y + degrees, 0f);
        }

        public bool TryRaycast(Ray worldRay, out float distance)
        {
            if (_renderers == null)
            {
                CacheRenderers();
            }

            distance = float.PositiveInfinity;
            bool hit = false;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || renderer is LineRenderer)
                {
                    continue;
                }

                Matrix4x4 worldToLocal = renderer.transform.worldToLocalMatrix;
                Ray localRay = new Ray(
                    worldToLocal.MultiplyPoint3x4(worldRay.origin),
                    worldToLocal.MultiplyVector(worldRay.direction));
                Bounds bounds = renderer.localBounds;
                bounds.Expand(_selectionPadding * 2f);

                if (bounds.IntersectRay(localRay, out float candidateDistance) && candidateDistance >= 0f)
                {
                    distance = Mathf.Min(distance, candidateDistance);
                    hit = true;
                }
            }

            return hit;
        }

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
