using UnityEngine;

namespace ProtoVR
{
    [DisallowMultipleComponent]
    public sealed class FloorDistanceGrabbable : MonoBehaviour
    {
        [SerializeField] private float _floorHeight;
        [SerializeField, Min(0f)] private float _rotationSpeed = 90f;
        [SerializeField, Min(0f)] private float _selectionPadding = 0.03f;
        [SerializeField] private MeshFilter _movementPlane;

        private Renderer[] _renderers;

        public float FloorHeight => _floorHeight;
        public float RotationSpeed => _rotationSpeed;

        public void SetMovementPlane(MeshFilter plane)
        {
            _movementPlane = plane;
            SetFloorPosition(transform.position);
        }

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
            if (!TryConstrainPosition(ref worldPosition)) return;
            transform.position = worldPosition;
        }

        public void RotateAroundWorldUp(float degrees)
        {
            Vector3 euler = transform.eulerAngles;
            Quaternion previousRotation = transform.rotation;
            transform.rotation = Quaternion.Euler(0f, euler.y + degrees, 0f);
            Vector3 position = transform.position;
            if (TryConstrainPosition(ref position)) transform.position = position;
            else transform.rotation = previousRotation;
        }

        private bool TryConstrainPosition(ref Vector3 position)
        {
            if (_movementPlane == null || _movementPlane.sharedMesh == null) return true;
            if (_renderers == null) CacheRenderers();

            Transform plane = _movementPlane.transform;
            Vector3 pivot = plane.InverseTransformPoint(transform.position);
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is LineRenderer) continue;
                Bounds bounds = renderer.localBounds;
                Matrix4x4 matrix = plane.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    Vector3 offset = matrix.MultiplyPoint3x4(point) - pivot;
                    minimum = Vector3.Min(minimum, offset);
                    maximum = Vector3.Max(maximum, offset);
                }
            }

            Bounds area = _movementPlane.sharedMesh.bounds;
            float minX = area.min.x - minimum.x;
            float maxX = area.max.x - maximum.x;
            float minZ = area.min.z - minimum.z;
            float maxZ = area.max.z - maximum.z;
            if (minX > maxX || minZ > maxZ) return false;
            Vector3 local = plane.InverseTransformPoint(position);
            local.x = Mathf.Clamp(local.x, minX, maxX);
            local.z = Mathf.Clamp(local.z, minZ, maxZ);
            position = plane.TransformPoint(local);
            position.y = _floorHeight;
            return true;
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
